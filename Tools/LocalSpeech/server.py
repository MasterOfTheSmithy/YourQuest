"""Owned, authenticated loopback speech worker. Never commits dialogue or opens a microphone."""
import argparse, base64, contextlib, hmac, json, math, os, struct, sys, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

VOICES = ('alba', 'anna', 'javert', 'marius')
MAX_BODY = 256 * 1024
MAX_RECORDING_SECONDS = 30

# note: Preserve the owned protocol while replacing only its CPU model adapters.
from initial_stack import Engine

class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.0'
    def log_message(self, *args):
        pass

    def json_reply(self, status, value):
        data = json.dumps(value, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def authorized(self):
        return hmac.compare_digest(self.headers.get('Authorization', ''), 'Bearer ' + self.server.token)

    def do_GET(self):
        if not self.authorized():
            self.json_reply(401, {'error': 'Unauthorized'})
            return
        if self.path != '/health':
            self.json_reply(404, {'error': 'Unknown route'})
            return
        e = self.server.engine
        e.last_activity = time.monotonic()
        self.json_reply(200, {'protocol': 1, 'device': 'cpu', 'ready': e.tts is not None and e.asr is not None,
                              'busy': e.work.locked(), 'capturing': e.session is not None, 'voices': VOICES,
                              'ort_single_thread': True, 'retrieval_ready': e.retriever is not None,
                              'models': ['MiniLM-v6', 'Kokoro-82M', 'Whisper Tiny', 'Silero VAD']})

    def do_POST(self):
        if not self.authorized():
            self.json_reply(401, {'error': 'Unauthorized'})
            return
        e = self.server.engine
        e.last_activity = time.monotonic()
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= MAX_BODY:
                raise ValueError('Invalid body length')
            self.connection.settimeout(10)
            body = json.loads(self.rfile.read(length))
            if not isinstance(body, dict):
                raise ValueError('Expected an object')
        except (ValueError, TimeoutError):
            self.json_reply(400, {'error': 'Invalid request'})
            return
        if self.path in ('/cancel', '/shutdown'):
            e.cancel()
            self.json_reply(200, {'ok': True})
            if self.path == '/shutdown':
                threading.Thread(target=self.server.shutdown, daemon=True).start()
            return
        if self.path not in ('/prepare', '/speak', '/asr/start', '/asr/chunk', '/asr/finish', '/retrieve'):
            self.json_reply(404, {'error': 'Unknown route'})
            return
        if not e.work.acquire(blocking=False):
            self.json_reply(409, {'error': 'Speech worker busy'})
            return
        e.work_started = time.monotonic()
        sent_audio = False
        try:
            # note: Model imports and SDK diagnostics go to stderr; stdout is only the owned-process handshake.
            with contextlib.redirect_stdout(sys.stderr):
                if self.path == '/prepare':
                    e.prepare()
                    self.json_reply(200, {'ok': True})
                elif self.path == '/retrieve':
                    if e.session is not None:
                        raise ValueError('Dictation owns speech worker')
                    self.json_reply(200, e.retrieve(body.get('query'), body.get('candidates'), body.get('limit', 4)))
                elif e.tts is None or e.asr is None:
                    self.json_reply(503, {'error': 'Speech worker not prepared'})
                elif self.path == '/asr/start':
                    e.start(body.get('session'), body.get('terms', []))
                    self.json_reply(200, {'ok': True})
                elif self.path == '/asr/chunk':
                    self.json_reply(200, e.chunk(body.get('session'), body.get('pcm'), body.get('rate')))
                elif self.path == '/asr/finish':
                    self.json_reply(200, {'text': e.finish(body.get('session'))})
                else:
                    text, voice = body.get('text'), body.get('voice')
                    speed = body.get('speed', 1)
                    # note: Only the installed pacing control is admitted; emotion never substitutes a voice or pitch.
                    if type(speed) not in (int, float) or not math.isfinite(speed) or not .9 <= speed <= 1.1:
                        raise ValueError('Invalid speech speed')
                    if not isinstance(text, str) or not text.strip() or len(text) > 1200 or voice not in VOICES:
                        raise ValueError('Invalid speech request')
                    if e.session is not None:
                        raise ValueError('Dictation owns speech worker')
                    generation = e.generation
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/octet-stream')
                    self.end_headers()
                    sent_audio = True
                    self.wfile.write(struct.pack('<4sII', b'YQSP', e.tts.sample_rate, 1))
                    samples = 0
                    for chunk in e.tts.generate_audio_stream(e.voices[voice], text, copy_state=True, speed=speed):
                        if not e.current(generation):
                            break
                        pcm = chunk.detach().cpu().numpy().reshape(-1).astype('<f4').tobytes()
                        samples += len(pcm) // 4
                        if samples > e.tts.sample_rate * 120:
                            raise ValueError('Speech output limit exceeded')
                        # note: Codec chunks can exceed one frame; keep the wire format bounded without rejecting valid audio.
                        for offset in range(0, len(pcm), 96000):
                            frame = pcm[offset:offset + 96000]
                            self.wfile.write(struct.pack('<I', len(frame)) + frame)
                            self.wfile.flush()
                    self.wfile.write(struct.pack('<I', 0))
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass
        except Exception as exc:
            if not sent_audio:
                self.json_reply(400 if isinstance(exc, (ValueError, TypeError)) else 500,
                                {'error': str(exc)[:240]})
            print(type(exc).__name__ + ': ' + str(exc)[:240], file=sys.stderr, flush=True)
        finally:
            e.work_started = None
            e.work.release()

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--cache', type=Path, required=True)
    p.add_argument('--port', type=int, default=0)
    p.add_argument('--parent-pid', type=int, default=0)
    p.add_argument('--provision', action='store_true')
    args = p.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    # note: Runtime never contacts model hosts. Setup explicitly provisions and pins the English checkpoint first.
    os.environ.update(CUDA_VISIBLE_DEVICES='', OMP_NUM_THREADS='2', MKL_NUM_THREADS='2',
                      MOONSHINE_ORT_SINGLE_THREAD='1', HF_HUB_DISABLE_TELEMETRY='1',
                      HF_HUB_CACHE=str(args.cache / 'huggingface'))
    if not args.provision:
        os.environ['HF_HUB_OFFLINE'] = '1'
    import psutil
    process = psutil.Process()
    allowed = process.cpu_affinity()
    count = min(2, max(0, len(allowed) - 4))
    if count == 0:
        raise RuntimeError('Insufficient CPU headroom')
    process.cpu_affinity(allowed[::2][:count])
    if sys.platform == 'win32':
        process.nice(psutil.BELOW_NORMAL_PRIORITY_CLASS)
    engine = Engine(args.cache, provision=args.provision)
    if args.provision:
        engine.prepare()
        print(json.dumps({'provisioned': True, 'voices': VOICES, 'device': 'cpu'}))
        return
    token = os.environ.get('YOURQUEST_SPEECH_TOKEN', '')
    if len(token) < 32:
        raise RuntimeError('Owned-process authentication token missing')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    server.daemon_threads = True
    server.token, server.engine = token, engine
    print(json.dumps({'protocol': 1, 'port': server.server_port, 'pid': os.getpid()}), flush=True)
    def watchdog():
        while True:
            time.sleep(2)
            # note: An exited Unity owner, idle session, or stuck native call retires this worker and all its memory.
            if (args.parent_pid and not psutil.pid_exists(args.parent_pid)) or \
               time.monotonic() - engine.last_activity > 90 or \
               (engine.work_started is not None and time.monotonic() - engine.work_started > 75):
                os._exit(0)
    threading.Thread(target=watchdog, daemon=True).start()
    try:
        server.serve_forever(poll_interval=.2)
    finally:
        server.server_close()

if __name__ == '__main__':
    main()
