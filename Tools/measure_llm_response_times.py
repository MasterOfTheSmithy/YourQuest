"""Isolated current-wire benchmark. Does not publish game content or alter model tags."""
import json
import base64
import os
import subprocess
import sys
import threading
import time
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'outputs' / 'LlmTiming_20261005'
OUT.mkdir(parents=True, exist_ok=True)
DATA = json.loads((OUT / 'Cases.json').read_text(encoding='utf-8-sig'))
EVENTS = OUT / 'Measurements.jsonl'
GPU = OUT / 'GpuSamples.jsonl'
STOP = threading.Event()
LOCK = threading.Lock()
RESULTS = []
OWNER = None
SERVICE = None
CURRENT_MODEL = None


def write(path, value):
    # note: Each observation survives interruption; no failed call disappears from averages or outcome counts.
    with LOCK, path.open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(value, ensure_ascii=False) + '\n')
        stream.flush()


def event(kind, **value):
    row = {'utc': datetime.now(timezone.utc).isoformat(), 'kind': kind, **value}
    write(EVENTS, row)
    print(json.dumps(row, ensure_ascii=False), flush=True)
    return row


def request(base, path, body=None, timeout=180):
    wire = None if body is None else json.dumps(body, ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(base + path, data=wire, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.load(response)


def used_gpu():
    result = subprocess.run(['nvidia-smi', '--query-gpu=memory.used', '--format=csv,noheader,nounits'],
                            capture_output=True, text=True, timeout=3, creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip())
    return int(result.stdout.strip().splitlines()[0])


def sample_gpu():
    while not STOP.is_set():
        try:
            write(GPU, {'utc': datetime.now(timezone.utc).isoformat(), 'usedMiB': used_gpu()})
        except Exception as error:
            write(GPU, {'error': str(error)})
        STOP.wait(1)


def wait_ready(base, path):
    started = time.monotonic()
    while time.monotonic() - started < 60:
        try:
            request(base, path, timeout=2)
            return time.monotonic() - started
        except Exception:
            if OWNER is not None and OWNER.poll() is not None:
                raise RuntimeError('Owned model server exited before readiness')
            time.sleep(.1)
    raise TimeoutError('Owned server readiness deadline exceeded')


def owned_gpu_snapshot(pid):
    # note: WDDM hides nvidia-smi process memory; Windows counters attribute bytes only to this Popen and its descendants.
    script = "$ErrorActionPreference='Stop'; $taskIds=@(" + str(int(pid)) + "); $taskProcesses=Get-CimInstance Win32_Process; do { $taskPrevious=$taskIds.Count; foreach($taskProcess in $taskProcesses) { if($taskIds -contains $taskProcess.ParentProcessId -and $taskIds -notcontains $taskProcess.ProcessId) { $taskIds+= [int]$taskProcess.ProcessId } } } while($taskIds.Count -ne $taskPrevious); $taskBytes=0L; foreach($taskCounter in (Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory)) { if($taskCounter.Name -match '^pid_(\\d+)_' -and $taskIds -contains [int]$Matches[1]) { $taskBytes += [long]$taskCounter.DedicatedUsage } }; @{pids=$taskIds;dedicatedBytes=$taskBytes} | ConvertTo-Json -Compress"
    encoded = base64.b64encode(script.encode('utf-16le')).decode('ascii')
    result = subprocess.run(['powershell', '-NoProfile', '-EncodedCommand', encoded], capture_output=True,
                            text=True, timeout=15, creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode:
        raise RuntimeError('Owned GPU attribution unavailable: ' + result.stderr.strip())
    return json.loads(result.stdout)


def wait_clean(baseline, label, owner_pid=None):
    # note: API residency/process exit is the ownership proof. NVIDIA readings separately verify driver allocations returned before reload.
    samples = []
    end = time.monotonic() + 8
    while time.monotonic() < end:
        value = used_gpu()
        samples.append(value)
        if len(samples) >= 2 and max(samples[-2:]) <= baseline + 128:
            event('cleanup-confirmed', label=label, baselineMiB=baseline, usedMiB=value, samplesMiB=samples)
            return
        time.sleep(.2)
    if owner_pid is not None:
        attribution = owned_gpu_snapshot(owner_pid)
        if attribution['dedicatedBytes'] <= 16 * 1024 * 1024:
            event('cleanup-confirmed-by-owner', label=label, baselineMiB=baseline, usedMiB=samples[-1], attribution=attribution,
                  note='Other applications changed total VRAM; owned process counters confirm allocation release')
            return
    event('cleanup-failed', label=label, baselineMiB=baseline, samplesMiB=samples)
    raise RuntimeError('GPU memory did not return within 128 MiB of isolated baseline before next model')


def stop_owned(process):
    if process is None:
        return
    # note: The PID comes only from a Popen created by this run; external game, Ollama and authoring processes are never targeted.
    if process.poll() is None:
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True,
                       timeout=10, creationflags=subprocess.CREATE_NO_WINDOW)
    process.wait(timeout=10)


def llama_start(cpu):
    global OWNER
    baseline = used_gpu()
    args = [DATA['llamaExecutable'], '--model', r'C:\Ai\Text Models\Qwen3.5-4B-Q4_K_M.gguf',
            '--host', '127.0.0.1', '--port', str(DATA['llamaPort']), '--ctx-size', '12288', '--parallel', '1',
            '--threads', '4', '--threads-batch', '4', '--batch-size', '128', '--ubatch-size', '32',
            '--prio', '-1', '--poll', '0', '--no-cache-prompt', '--cache-ram', '0', '--log-verbosity', '1',
            '--flash-attn', 'on', '--no-webui', '--reasoning', 'off']
    if cpu:
        args += ['--device', 'none', '--no-op-offload', '--n-gpu-layers', '0', '--no-kv-offload', '--fit', 'off']
    else:
        args += ['--fit', 'on', '--fit-target', '3072']
    log = (OUT / ('LlamaCpu.log' if cpu else 'LlamaGpu.log')).open('w', encoding='utf-8')
    started = time.monotonic()
    OWNER = subprocess.Popen(args, stdout=log, stderr=subprocess.STDOUT,
                             creationflags=subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS)
    base = f"http://127.0.0.1:{DATA['llamaPort']}"
    wait_ready(base, '/health')
    event('model-loaded', model='control', placement='CPU' if cpu else 'GPU startup', pid=OWNER.pid,
          readySeconds=time.monotonic() - started, baselineMiB=baseline, usedMiB=used_gpu(), arguments=args[1:])
    return base, baseline


def ollama_unload(model, baseline):
    base = f"http://127.0.0.1:{DATA['ollamaPort']}"
    started = time.monotonic()
    request(base, '/api/generate', {'model': model, 'keep_alive': 0, 'stream': False}, timeout=15)
    end = time.monotonic() + 10
    while time.monotonic() < end:
        models = request(base, '/api/ps', timeout=3).get('models', [])
        if not models:
            event('model-unloaded', model=model, elapsedSeconds=time.monotonic() - started, apiModels=models)
            wait_clean(baseline, model, SERVICE.pid)
            return
        time.sleep(.1)
    raise RuntimeError('Ollama still reports resident model after unload')


def call(case, base, placement, sample, payload=None):
    body = json.loads(json.dumps(payload if payload is not None else case['payload']))
    # note: Seed changes only sampling between repeated fixture calls, never a gameplay world seed or accepted record.
    started = time.monotonic()
    row = {'name': case['name'], 'role': case['role'], 'model': case['model'], 'placement': placement, 'sample': sample,
           'estimatedInputTokens': case['estimatedInputTokens'], 'reservedOutputTokens': case['reservedOutputTokens'],
           'diagnosticOnly': case['diagnosticOnly'], 'source': case['source']}
    try:
        reply = request(base, '/v1/chat/completions' if case['backend'] == 'LlamaCpp' else '/api/generate', body, timeout=180)
        text = reply['choices'][0]['message'].get('content', '') if case['backend'] == 'LlamaCpp' else reply.get('response', '')
        row.update(success=bool(text.strip()), seconds=time.monotonic() - started, textChars=len(text),
                   usage=reply.get('usage'), timings=reply.get('timings'),
                   loadSeconds=reply.get('load_duration', 0) / 1e9, promptTokens=reply.get('prompt_eval_count'),
                   outputTokens=reply.get('eval_count'), thinkingChars=len(reply.get('thinking') or ''),
                   finishReason=reply['choices'][0].get('finish_reason') if case['backend'] == 'LlamaCpp' else reply.get('done_reason'))
        if body.get('format') or body.get('response_format'):
            try:
                parsed = json.loads(text)
                row['jsonObject'] = isinstance(parsed, dict)
            except Exception:
                row['jsonObject'] = False
        safe = ''.join(char if char.isalnum() else '_' for char in case['name'])
        (OUT / f'{safe}_{placement}_{sample}.json').write_text(json.dumps(reply, ensure_ascii=False, indent=2), encoding='utf-8')
    except Exception as error:
        row.update(success=False, seconds=time.monotonic() - started, error=str(error))
    row['usedMiB'] = used_gpu()
    RESULTS.append(row)
    event('request-completed', **row)


def main():
    global OWNER, SERVICE, CURRENT_MODEL
    resume = '--ollama-only' in sys.argv
    if resume:
        RESULTS.extend(json.loads((OUT / 'Results.json').read_text(encoding='utf-8'))['results'])
    baseline = used_gpu()
    event('baseline', usedMiB=baseline, note='Unity closed; independent model timing, not gameplay frame-time certification')
    thread = threading.Thread(target=sample_gpu, daemon=True)
    thread.start()
    try:
        # note: Startup roles keep their approved GPU policy. Released-gameplay frontier/progression roles also get their existing CPU policy measured.
        for cpu in ([] if resume else [False, True]):
            base, prior = llama_start(cpu)
            for case in DATA['cases']:
                if case['backend'] != 'LlamaCpp':
                    continue
                if cpu and not (case['name'].startswith('Frontier ') or case['role'] in ['Progression', 'StructuredState', 'QuestGeneration']):
                    continue
                for sample in range(2):
                    call(case, base, 'cpu' if cpu else 'gpu', sample)
            retiring_pid = OWNER.pid
            stop_owned(OWNER)
            OWNER = None
            wait_clean(prior, 'control CPU' if cpu else 'control GPU', retiring_pid)
        environment = dict(os.environ, OLLAMA_HOST=f"127.0.0.1:{DATA['ollamaPort']}", OLLAMA_MODELS=DATA['ollamaModelDirectory'],
                           OLLAMA_MAX_LOADED_MODELS='1', OLLAMA_NUM_PARALLEL='1')
        SERVICE = subprocess.Popen([DATA['ollamaExecutable'], 'serve'], env=environment,
                                   stdout=(OUT / 'Ollama.log').open('w'), stderr=subprocess.STDOUT,
                                   creationflags=subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS)
        base = f"http://127.0.0.1:{DATA['ollamaPort']}"
        wait_ready(base, '/api/tags')
        tags = request(base, '/api/tags')
        (OUT / 'ModelInventory.json').write_text(json.dumps(tags, indent=2), encoding='utf-8')
        for case in DATA['cases']:
            if case['backend'] != 'Ollama':
                continue
            prior = used_gpu()
            CURRENT_MODEL = case['model']
            body = json.loads(json.dumps(case['payload']))
            body.setdefault('options', {})['num_gpu'] = 0
            body['keep_alive'] = '5m'
            for sample in range(2):
                call(case, base, 'cpu', sample, body)
            ollama_unload(CURRENT_MODEL, prior)
            CURRENT_MODEL = None
        # note: One GPU dialogue switch exercises an actual VRAM allocation/release on Ollama, independently of the CPU-qualified Goddess lane.
        case = next(row for row in DATA['cases'] if row['role'] == 'Dialogue')
        prior = used_gpu()
        CURRENT_MODEL = case['model']
        body = json.loads(json.dumps(case['payload']))
        # note: This diagnostic uses a bounded partial offload so external model-authoring allocations keep their memory.
        body.setdefault('options', {})['num_gpu'] = 8
        call(case, base, 'gpu-switch-probe', 0, body)
        ollama_unload(CURRENT_MODEL, prior)
        CURRENT_MODEL = None
    finally:
        if CURRENT_MODEL is not None and SERVICE is not None:
            try:
                ollama_unload(CURRENT_MODEL, baseline)
            except Exception as error:
                event('cleanup-exception', error=str(error))
        stop_owned(OWNER)
        stop_owned(SERVICE)
        STOP.set()
        thread.join(timeout=4)
        (OUT / 'Results.json').write_text(json.dumps({'evidence': DATA['evidence'], 'results': RESULTS,
            'baselineMiB': baseline, 'finalMiB': used_gpu()}, ensure_ascii=False, indent=2), encoding='utf-8')
        event('finished', requests=len(RESULTS), failures=sum(not row['success'] for row in RESULTS), usedMiB=used_gpu())


if __name__ == '__main__':
    main()
