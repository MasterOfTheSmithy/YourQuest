"""Explicit online provisioning; pin revisions and hash every offline model file."""
import hashlib
import json
import sys
import urllib.request
from pathlib import Path
from huggingface_hub import snapshot_download

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / 'outputs/InitialModelStack/cache'


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    sources = []
    specs = [
        ('unity/inference-engine-minilm-v6', 'c43101c43cf8ffa8a77d500d39f56fd33ce0447c', 'minilm', ['models/MiniLMv6.onnx', 'data/vocab.txt', 'README.md']),
        ('hexgrad/Kokoro-82M', 'f3ff3571791e39611d31c381e3a41a3af07b4987', 'kokoro', ['config.json', 'kokoro-v1_0.pth', 'voices/af_heart.pt',
                                       'voices/af_bella.pt', 'voices/am_adam.pt', 'voices/am_michael.pt', 'README.md']),
        ('Systran/faster-whisper-tiny', 'd90ca5fe260221311c53c58e660288d3deb8d356', 'whisper', ['config.json', 'model.bin', 'tokenizer.json', 'vocabulary.*', 'README.md']),
    ]
    # note: Reprovisioning preserves the reviewed revisions rather than silently upgrading gameplay models.
    for repo, revision, folder, files in specs:
        print('Provisioning', repo, revision, flush=True)
        snapshot_download(repo, revision=revision, local_dir=CACHE / folder, allow_patterns=files, max_workers=2)
        sources.append({'repository': repo, 'revision': revision, 'folder': folder})
    revision = '1e261b036686cd0017d500ee96acd1c4ba572a9d'
    folder = CACHE / 'silero'
    folder.mkdir(exist_ok=True)
    for source, destination in [('src/silero_vad/data/silero_vad.onnx', 'silero_vad.onnx'), ('LICENSE', 'LICENSE')]:
        with urllib.request.urlopen(f'https://raw.githubusercontent.com/snakers4/silero-vad/{revision}/{source}', timeout=60) as response:
            (folder / destination).write_bytes(response.read())
    sources.append({'repository': 'snakers4/silero-vad', 'revision': revision, 'folder': 'silero'})
    # note: SDK cache metadata is not a model; runtime verifies only the actual pinned files.
    records = []
    for path in sorted(CACHE.rglob('*')):
        if not path.is_file() or '.cache' in path.parts or path.name == 'model_manifest.json':
            continue
        records.append({'path': path.relative_to(CACHE).as_posix(), 'bytes': path.stat().st_size,
                        'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    manifest = {'schema': 1, 'device': 'cpu', 'sources': sources, 'files': records,
                'asr_note': 'Whisper Tiny converted by SYSTRAN to CTranslate2; not the Unity ONNX export.',
                'voices': {'alba': 'af_heart', 'anna': 'af_bella', 'javert': 'am_adam', 'marius': 'am_michael'}}
    (CACHE / 'model_manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    sys.path.insert(0, str(ROOT / 'Tools/LocalSpeech'))
    from initial_stack import Engine
    engine = Engine(CACHE)
    engine.prepare()
    print(json.dumps({'prepared': True, 'device': 'cpu', 'models': len(sources), 'files': len(records),
                      'embedding_inputs': [v.name for v in engine.retriever.get_inputs()],
                      'embedding_outputs': [v.name for v in engine.retriever.get_outputs()]}), flush=True)


if __name__ == '__main__':
    main()
