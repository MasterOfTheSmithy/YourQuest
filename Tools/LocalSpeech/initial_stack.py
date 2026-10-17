"""CPU-only, offline adapters behind the existing owned speech protocol."""
import base64
import hashlib
import json
import math
import threading
import time
from pathlib import Path

VOICES = ('alba', 'anna', 'javert', 'marius')
VOICE_MAP = dict(alba='af_heart', anna='af_bella', javert='am_adam', marius='am_michael')


class KokoroAdapter:
    sample_rate = 24000

    def __init__(self, cache):
        import torch
        import espeakng_loader
        from phonemizer.backend.espeak.wrapper import EspeakWrapper
        from kokoro import KModel, KPipeline
        EspeakWrapper.set_library(espeakng_loader.get_library_path())
        # note: Explicit local weights and voice tensors prevent SDK downloads during gameplay.
        model = KModel(config=str(cache / 'kokoro/config.json'), model=str(cache / 'kokoro/kokoro-v1_0.pth')).to('cpu').eval()
        self.pipeline = KPipeline(lang_code='a', model=model, device='cpu')
        self.voices = {key: torch.load(cache / f'kokoro/voices/{value}.pt', map_location='cpu', weights_only=True)
                       for key, value in VOICE_MAP.items()}

    def generate_audio_stream(self, voice, text, copy_state=True, speed=1):
        # note: Installed Kokoro supports speed, not emotion labels or SSML. Keep the selected tensor unchanged.
        for result in self.pipeline(text, voice=voice, speed=speed):
            if result.audio is not None:
                yield result.audio


class Engine:
    def __init__(self, cache, provision=False):
        self.cache = Path(cache).resolve()
        self.provision = provision
        self.work, self.state = threading.Lock(), threading.Lock()
        self.generation, self.session = 0, None
        self.tts = self.asr = self.vad = self.retriever = None
        self.voices = {}
        self.recorded = 0
        self.last_activity = time.monotonic()
        self.work_started = None
        self._verified = False
        self._reset_capture()

    def _verify(self):
        if self._verified:
            return
        manifest = json.loads((self.cache / 'model_manifest.json').read_text(encoding='utf-8'))
        if manifest.get('schema') != 1:
            raise RuntimeError('Initial models must be provisioned first')
        # note: All runtime paths are confined to a hash-pinned manifest; no generated asset paths are accepted.
        for item in manifest['files']:
            path = (self.cache / item['path']).resolve()
            if not path.is_relative_to(self.cache) or not path.is_file() or path.is_symlink():
                raise RuntimeError('Invalid local model path')
            digest = hashlib.sha256()
            with path.open('rb') as source:
                while block := source.read(8 * 1024 * 1024):
                    digest.update(block)
            if path.stat().st_size != item['bytes'] or digest.hexdigest() != item['sha256']:
                raise RuntimeError('Local model identity mismatch: ' + item['path'])
        self._verified = True

    @staticmethod
    def _ort(path):
        import onnxruntime as ort
        options = ort.SessionOptions()
        options.intra_op_num_threads = 2
        options.inter_op_num_threads = 1
        return ort.InferenceSession(str(path), sess_options=options, providers=['CPUExecutionProvider'])

    def prepare_retrieval(self):
        if self.retriever is not None:
            return
        self._verify()
        from tokenizers import BertWordPieceTokenizer
        self.tokenizer = BertWordPieceTokenizer(str(self.cache / 'minilm/data/vocab.txt'), lowercase=True)
        self.tokenizer.enable_truncation(max_length=256)
        self.retriever = self._ort(self.cache / 'minilm/models/MiniLMv6.onnx')

    def _embed(self, text):
        import numpy as np
        encoded = self.tokenizer.encode(text)
        ids = np.asarray([encoded.ids], dtype=np.int64)
        mask = np.ones_like(ids)
        inputs = {'input_ids': ids, 'attention_mask': mask, 'token_type_ids': np.zeros_like(ids)}
        values = self.retriever.run(None, {item.name: inputs[item.name] for item in self.retriever.get_inputs()})
        vector = values[0]
        if vector.ndim == 3:
            vector = vector.mean(axis=1)
        vector = vector.reshape(-1).astype(np.float32)
        norm = np.linalg.norm(vector)
        if not np.isfinite(vector).all() or norm <= 0:
            raise RuntimeError('Invalid sentence embedding')
        return vector / norm

    def retrieve(self, query, candidates, limit):
        if not isinstance(query, str) or not query.strip() or len(query) > 2000 or \
                not isinstance(candidates, list) or not 1 <= len(candidates) <= 128 or \
                any(not isinstance(value, str) or not value.strip() or len(value) > 600 for value in candidates) or \
                type(limit) is not int or not 1 <= limit <= 4:
            raise ValueError('Invalid memory request')
        generation = self.generation
        self.prepare_retrieval()
        if not self.current(generation):
            raise ValueError('Memory request was retired')
        query_vector = self._embed(query)
        scores = []
        # note: Embeddings are request-local; one NPC/profile can never retrieve another's cached transcript.
        for index, value in enumerate(candidates):
            if not self.current(generation):
                raise ValueError('Memory request was retired')
            scores.append((float(query_vector @ self._embed(value)), index))
        scores.sort(key=lambda item: (-item[0], item[1]))
        if not self.current(generation):
            raise ValueError('Memory request was retired')
        selected = scores[:limit]
        return {'indices': [index for score, index in selected], 'scores': [score for score, index in selected]}

    def prepare(self):
        self._verify()
        import torch
        if torch.version.cuda is not None:
            raise RuntimeError('Install CPU-only PyTorch')
        if self.tts is None:
            torch.set_num_threads(2)
            torch.set_num_interop_threads(1)
            self.tts = KokoroAdapter(self.cache)
            self.voices = self.tts.voices
        if self.asr is None:
            from faster_whisper import WhisperModel
            self.asr = WhisperModel(str(self.cache / 'whisper'), device='cpu', compute_type='int8',
                                    cpu_threads=2, num_workers=1, local_files_only=True)
        if self.vad is None:
            self.vad = self._ort(self.cache / 'silero/silero_vad.onnx')
        self.prepare_retrieval()

    def cancel(self):
        # note: Retire inference results immediately; the work lock serializes the next native call.
        with self.state:
            self.generation += 1
            self.session = None

    def current(self, generation):
        with self.state:
            return generation == self.generation

    def require_session(self, session):
        if not isinstance(session, str) or len(session) != 32 or any(c not in '0123456789abcdef' for c in session):
            raise ValueError('Invalid capture identity')
        if session != self.session:
            raise ValueError('Capture was retired')

    def _reset_capture(self):
        import numpy as np
        self.audio = []
        self.pending = np.zeros(0, dtype=np.float32)
        self.vad_state = np.zeros((2, 1, 128), dtype=np.float32)
        self.vad_context = np.zeros((1, 64), dtype=np.float32)
        self.speech_frames = self.silent_frames = 0
        self.delivered = False

    def start(self, session, terms):
        if not isinstance(session, str) or len(session) != 32 or any(c not in '0123456789abcdef' for c in session):
            raise ValueError('Invalid capture identity')
        if not isinstance(terms, list) or len(terms) > 32 or any(not isinstance(v, str) or len(v) > 100 for v in terms):
            raise ValueError('Invalid keyterms')
        self.cancel()
        self._reset_capture()
        self.recorded, self.terms = 0, terms
        with self.state:
            self.session = session

    def _transcribe(self, session):
        import numpy as np
        self.require_session(session)
        generation = self.generation
        if self.speech_frames < 8 or not self.audio:
            return ''
        audio = np.concatenate(self.audio)
        segments, _ = self.asr.transcribe(audio, language='en', beam_size=3, temperature=0,
                                          vad_filter=False, condition_on_previous_text=False,
                                          hotwords=', '.join(self.terms))
        text = ' '.join(segment.text.strip() for segment in segments).strip()
        self.require_session(session)
        if not self.current(generation):
            raise ValueError('Capture was retired')
        return text

    def chunk(self, session, pcm, rate):
        import numpy as np
        from scipy.signal import resample_poly
        self.require_session(session)
        if type(rate) is not int or not 8000 <= rate <= 48000:
            raise ValueError('Unsupported sample rate')
        raw = base64.b64decode(pcm, validate=True)
        if not raw or len(raw) % 4 or len(raw) > rate * 4:
            raise ValueError('Invalid audio chunk')
        audio = np.frombuffer(raw, dtype='<f4')
        if not np.isfinite(audio).all() or np.max(np.abs(audio)) > 1.01:
            raise ValueError('Invalid audio samples')
        self.recorded += len(audio) / rate
        if self.recorded > 30.1:
            raise ValueError('Recording limit exceeded')
        divisor = math.gcd(rate, 16000)
        audio = resample_poly(audio, 16000 // divisor, rate // divisor).astype(np.float32) if rate != 16000 else audio
        if not self.delivered:
            self.audio.append(audio)
        self.pending = np.concatenate((self.pending, audio))
        while len(self.pending) >= 512:
            frame, self.pending = self.pending[:512], self.pending[512:]
            inputs = np.concatenate((self.vad_context, frame.reshape(1, -1)), axis=1)
            probability, self.vad_state = self.vad.run(None, {'input': inputs, 'state': self.vad_state,
                                                            'sr': np.array(16000, dtype=np.int64)})
            self.vad_context = inputs[:, -64:]
            if float(probability.reshape(-1)[0]) >= .5:
                self.speech_frames += 1
                self.silent_frames = 0
            elif self.speech_frames:
                self.silent_frames += 1
        self.require_session(session)
        if not self.delivered and self.speech_frames >= 8 and self.silent_frames >= 22:
            # note: Silence alone cannot generate a player message; a completed utterance is delivered only once.
            text = self._transcribe(session)
            self.delivered = True
            self.audio.clear()
            return {'text': text, 'complete': bool(text)}
        return {'text': '', 'complete': False}

    def finish(self, session):
        self.require_session(session)
        text = '' if self.delivered else self._transcribe(session)
        self.session = None
        self.audio.clear()
        return text
