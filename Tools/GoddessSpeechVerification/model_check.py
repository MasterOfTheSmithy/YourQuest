"""Fresh local-model qualification with detached current-source speech fixtures."""
from pathlib import Path
import json, os, subprocess, time, urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/GoddessSpeechRepair_20261007'
data = json.loads((OUT / 'Cases.json').read_text(encoding='utf-8-sig'))
BASE = 'http://127.0.0.1:11441'
MODEL = 'smaller-test:latest'
EXPECTED = '1dcf59c4b2d0b233363c689818a1c48dfd65e5c96f1594ba57f6d851e84f869e'

def request(path, body=None, timeout=180):
    payload = None if body is None else json.dumps(body, ensure_ascii=False).encode('utf-8')
    query = urllib.request.Request(BASE + path, data=payload, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(query, timeout=timeout) as response:
        return json.load(response)

rows = []
service = None
try:
    # note: This separate loopback server owns only disposable CPU fixtures; it does not change the game's endpoint or select any save.
    timing = json.loads((ROOT / 'outputs/LlmTiming_20261005/Cases.json').read_text(encoding='utf-8-sig'))
    env = dict(os.environ, OLLAMA_HOST='127.0.0.1:11441', OLLAMA_MODELS=timing['ollamaModelDirectory'], OLLAMA_NUM_PARALLEL='1', OLLAMA_MAX_LOADED_MODELS='1')
    service = subprocess.Popen([timing['ollamaExecutable'], 'serve'], env=env, stdout=(OUT / 'ModelServer.log').open('w'), stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS)
    for _ in range(200):
        if service.poll() is not None:
            raise RuntimeError('Owned model server exited before readiness')
        try:
            tags = request('/api/tags', timeout=2)
            break
        except OSError as error:
            last_readiness_error = str(error)
            time.sleep(.2)
    else:
        raise RuntimeError('Owned model server did not become ready: ' + last_readiness_error)
    digest = next(model['digest'] for model in tags['models'] if model['name'] == MODEL)
    assert digest == EXPECTED
    for case in data['cases']:
        body = case['payload']
        assert body['model'] == MODEL and body['options']['num_gpu'] == 0
        started = time.monotonic()
        try:
            result = request('/api/generate', body)
            row = dict(name=case['name'], raw=result.get('response', ''), digest=digest, error=None, seconds=round(time.monotonic() - started, 3), inputTokens=result.get('prompt_eval_count'), outputTokens=result.get('eval_count'))
        except Exception as error:
            row = dict(name=case['name'], raw='', digest=digest, error=str(error), seconds=round(time.monotonic() - started, 3))
        rows.append(row)
        (OUT / 'Replies.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps({key: row[key] for key in ['name', 'error', 'seconds']}), flush=True)
    request('/api/generate', {'model': MODEL, 'keep_alive': 0, 'stream': False}, timeout=30)
    for _ in range(100):
        if not request('/api/ps', timeout=5)['models']:
            break
        time.sleep(.1)
    else:
        raise RuntimeError('Owned model did not unload')
finally:
    if service is not None:
        service.terminate()
        try:
            service.wait(timeout=10)
        except subprocess.TimeoutExpired:
            service.kill()
            service.wait(timeout=5)
