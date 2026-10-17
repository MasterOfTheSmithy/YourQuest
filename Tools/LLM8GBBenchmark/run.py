"""Owned, loopback-only LLM benchmark. Production servers, settings and saves are untouched."""
import argparse
import copy
import hashlib
import json
import os
import re
import socket
import struct
import subprocess
import threading
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

import jsonschema
import psutil
from fixtures import build_cases

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'Docs/LLM_8GB_Download_Test_Manifest_2026-10-07.json'
FLAGS = subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS
OUT = None


def utc():
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def strict_json(text):
    # note: Model proposals use strict JSON: duplicate keys and non-finite constants are rejection, never last-value wins.
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError('Duplicate JSON key: ' + key)
            result[key] = value
        return result
    def constant(value):
        raise ValueError('Nonstandard JSON constant: ' + value)
    return json.loads(text, object_pairs_hook=pairs, parse_constant=constant)


def event(kind, **fields):
    row = dict(utc=utc(), kind=kind, **fields)
    with (OUT / 'events.jsonl').open('a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False), flush=True)
    return row


def gpu():
    r = subprocess.run(['nvidia-smi', '--query-gpu=memory.used,memory.free,memory.total',
        '--format=csv,noheader,nounits'], capture_output=True, text=True, timeout=5, creationflags=FLAGS)
    if r.returncode:
        raise RuntimeError(r.stderr)
    used, free, total = [int(s.strip()) for s in r.stdout.splitlines()[0].split(',')]
    return dict(usedMiB=used, freeMiB=free, totalMiB=total)


def request(base, path, payload=None, timeout=10):
    data = None if payload is None else json.dumps(payload, ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(base + path, data=data, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            return json.load(response)
    except urllib.error.HTTPError as e:
        raise RuntimeError(f'HTTP {e.code}: {e.read().decode("utf-8", errors="replace")[:1800]}') from e


def metadata(path):
    # note: Read GGUF metadata only; large tokenizer arrays are skipped, never printed or treated as instructions.
    formats = {0:'B', 1:'b', 2:'H', 3:'h', 4:'I', 5:'i', 6:'f', 7:'?', 10:'Q', 11:'q', 12:'d'}
    result = {}
    with Path(path).open('rb') as f:
        def number(fmt):
            return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
        def string(keep=True):
            length = number('Q')
            if keep:
                return f.read(length).decode('utf-8', errors='replace')
            f.seek(length, 1)
        def value(kind, keep):
            if kind in formats:
                v = number(formats[kind]); return v if keep else None
            if kind == 8:
                return string(keep)
            if kind == 9:
                subtype, count = number('I'), number('Q')
                if not keep and subtype in formats:
                    f.seek(struct.calcsize('<' + formats[subtype]) * count, 1); return None
                if keep:
                    return [value(subtype, True) for _ in range(count)]
                for _ in range(count):
                    value(subtype, False)
                return None
            raise ValueError(f'Unknown GGUF type {kind}')
        if f.read(4) != b'GGUF':
            raise ValueError('Not GGUF')
        result['ggufVersion'], result['tensorCount'], count = number('I'), number('Q'), number('Q')
        for _ in range(count):
            key, kind = string(), number('I')
            keep = (key.startswith('general.') or key == 'tokenizer.chat_template' or
                    re.search(r'context_length|block_count|expert_count|expert_used_count|nextn|mtp|head_count', key)) and kind != 9
            v = value(kind, bool(keep))
            if keep:
                result[key] = v
    return result


class Server:
    def __init__(self, model, engine, cpu=False, context=12288, headroom=768, cache=False, mtp=False, port=11445, label=None, thinking='off', reasoning_budget=-1, kv_type='f16', verbosity=3, gpu_layers=None, cache_ram_mib=0, checkpoints=None, checkpoint_min_step=None, spec_type='none'):
        self.model, self.engine, self.cpu = model, engine, cpu
        self.context, self.headroom, self.cache, self.mtp, self.port = context, headroom, cache, mtp, port
        self.thinking = thinking
        self.reasoning_budget, self.kv_type = reasoning_budget, kv_type
        self.verbosity = verbosity
        self.gpu_layers = gpu_layers
        # note: This bounded experiment adds no draft model and never changes the production runtime's defaults.
        if spec_type not in ('none', 'ngram-simple') or (mtp and spec_type != 'none'):
            raise ValueError('Unsupported or conflicting speculative test mode')
        self.spec_type = spec_type
        self.cache_ram_mib, self.checkpoints, self.checkpoint_min_step = cache_ram_mib, checkpoints, checkpoint_min_step
        self.metadata = metadata(model['localPath'])
        template = self.metadata.get('tokenizer.chat_template', '')
        self.template_kwargs = {'enable_thinking': thinking == 'on'} if thinking != 'auto' and 'enable_thinking' in template else {}
        self.label = label or (model['id'] + ('_cpu' if cpu else '_gpu') + ('_mtp' if mtp else ''))
        self.base = f'http://127.0.0.1:{port}'
        self.process, self.log, self.props = None, None, None

    def __enter__(self):
        # note: A free-port check prevents the harness from using another task's server by accident.
        with socket.socket() as check:
            check.bind(('127.0.0.1', self.port))
        self.baseline = gpu()
        args = [str(self.engine), '--model', self.model['localPath'], '--host', '127.0.0.1',
            '--port', str(self.port), '--ctx-size', str(self.context), '--parallel', '1',
            '--threads', '4', '--threads-batch', '4', '--batch-size', '128', '--ubatch-size', '32',
            '--prio', '-1', '--poll', '0', '--flash-attn', 'on', '--no-webui', '--metrics',
            '-lv', str(self.verbosity),
            '--reasoning', self.thinking, '--cache-ram', str(self.cache_ram_mib), '--cache-prompt' if self.cache else '--no-cache-prompt']
        # note: Hybrid models need usable short-context checkpoints before a requested cache can demonstrate reuse.
        if self.checkpoints is not None:
            args += ['--ctx-checkpoints', str(self.checkpoints)]
        if self.checkpoint_min_step is not None:
            args += ['--checkpoint-min-step', str(self.checkpoint_min_step)]
        if self.reasoning_budget >= 0:
            args += ['--reasoning-budget', str(self.reasoning_budget)]
        if self.kv_type != 'f16':
            args += ['--cache-type-k', self.kv_type, '--cache-type-v', self.kv_type]
        if self.cpu:
            args += ['--device', 'none', '--no-op-offload', '--n-gpu-layers', '0', '--no-kv-offload', '--fit', 'off']
        elif self.gpu_layers is not None:
            # note: Explicit offload is a separate measured profile when auto-fit misses the reserve on a shared GPU.
            args += ['--n-gpu-layers', str(self.gpu_layers), '--fit', 'off']
        else:
            args += ['--fit', 'on', '--fit-target', str(self.headroom), '--fit-ctx', str(self.context)]
        if self.mtp:
            args += ['--spec-type', 'draft-mtp', '--spec-draft-n-max', '3']
        elif self.spec_type != 'none':
            args += ['--spec-type', self.spec_type, '--spec-ngram-simple-size-n', '8', '--spec-ngram-simple-size-m', '8']
        self.log_path = OUT / (self.label + '.log')
        self.log = self.log_path.open('w', encoding='utf-8')
        started = time.monotonic()
        self.process = subprocess.Popen(args, stdout=self.log, stderr=subprocess.STDOUT, creationflags=FLAGS)
        event('server-start', label=self.label, pid=self.process.pid, arguments=args, baseline=self.baseline)
        try:
            while time.monotonic() - started < 120:
                if self.process.poll() is not None:
                    raise RuntimeError(f'Server exited {self.process.returncode}; see {self.log_path.name}')
                try:
                    health = request(self.base, '/health', timeout=2)
                    if health.get('status') == 'ok':
                        break
                except Exception as error:
                    if '10013' in str(error):
                        raise RuntimeError('Sandbox denied loopback access; launch this isolated harness with approved local access') from error
                    time.sleep(.2)
            else:
                raise TimeoutError('Server readiness exceeded 120 seconds')
            self.load_seconds = time.monotonic() - started
            self.props = request(self.base, '/props')
            save(OUT / (self.label + '_props.json'), self.props)
            self.loaded = gpu()
            event('server-ready', label=self.label, loadSeconds=round(self.load_seconds,3), gpu=self.loaded,
                  context=self.context, cpu=self.cpu)
            return self
        except BaseException:
            self.close()
            raise

    def close(self):
        # note: Termination targets only Popen handles created by this instance; no external process is killed.
        started = time.monotonic()
        if self.process is not None:
            if self.process.poll() is None:
                descendants = psutil.Process(self.process.pid).children(recursive=True)
                self.process.terminate()
                try:
                    self.process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    self.process.kill(); self.process.wait(timeout=10)
                for child in descendants:
                    try:
                        if child.is_running():
                            child.terminate()
                    except psutil.NoSuchProcess:
                        pass
                _, remaining = psutil.wait_procs(descendants, timeout=5)
                for child in remaining:
                    child.kill()
                psutil.wait_procs(remaining, timeout=5)
            if self.log:
                self.log.close()
            clean = gpu()
            event('server-closed', label=self.label, pid=self.process.pid, unloadSeconds=round(time.monotonic()-started,3), gpu=clean)

    def __exit__(self, *_):
        self.close()


def call(server, case, sample=0, greedy=False, output_extra=0, native_sampling=False, sampling_overrides=None):
    row = dict(utc=utc(), modelId=server.model['id'], engine=str(server.engine), serverLabel=server.label,
               fixture=case['id'], track=case['track'], sample=sample, cpu=server.cpu, cache=server.cache,
               mtp=server.mtp, headroomMiB=server.headroom, thinking=server.thinking,
               gpuLayers=server.gpu_layers, autoFit=not server.cpu and server.gpu_layers is None,
               cacheRamMiB=server.cache_ram_mib,checkpoints=server.checkpoints,checkpointMinStep=server.checkpoint_min_step,
               templateKwargs=server.template_kwargs, contextTokens=server.context,
               reasoningTokenBudget=server.reasoning_budget,kvCacheType=server.kv_type,evidence=case['evidence'])
    messages = copy.deepcopy(case['messages'])
    payload = dict(messages=messages, stream=True, stream_options={'include_usage': True},
        max_tokens=case['max_tokens']+output_extra, temperature=0 if greedy else case['temperature'], top_p=.9,
        top_k=20, repeat_penalty=1.05, presence_penalty=0, seed=42+sample,
        cache_prompt=server.cache, chat_template_kwargs=server.template_kwargs, timings=True)
    # note: The official LFM weights always reason; their published native sampling is a separate test condition.
    sampling_policy='standardized_project_like'
    if native_sampling and server.model['id'] in ['lfm25-qad','lfm25-q6']:
        payload.update(temperature=0 if greedy else .1, top_k=50, top_p=1, repeat_penalty=1.1)
        sampling_policy='published_LiquidAI_LFM2.5'
    elif native_sampling and server.metadata.get('general.architecture')=='qwen35':
        payload.update(temperature=0 if greedy else (1 if server.thinking=='on' or case['track']=='reasoning' else .7),
            top_p=.95 if server.thinking=='on' else (1 if case['track']=='reasoning' else .8),
            top_k=40 if server.thinking!='on' and case['track']=='reasoning' else 20,
            presence_penalty=2 if server.thinking!='on' and case['track']=='reasoning' else 1.5,
            repeat_penalty=1,min_p=0)
        sampling_policy='published_Qwen_base_best_practices_applied_to_these_weights'
    elif native_sampling and server.metadata.get('general.architecture')=='gemma4':
        payload.update(temperature=0 if greedy else 1,top_p=.95,top_k=64,repeat_penalty=1,min_p=0)
        sampling_policy='published_Google_Gemma4'
    elif native_sampling and server.metadata.get('general.architecture')=='spark2_5':
        payload.update(temperature=0 if greedy else 1,top_p=.95,top_k=0,repeat_penalty=1,min_p=0)
        sampling_policy='published_Spark_base_top_k_unlimited_mapped_to_llamacpp_zero'
    # note: Reviewed per-package recipes can override sampling without replacing the model's native chat template.
    if sampling_overrides:
        if set(sampling_overrides) - {'temperature', 'top_p', 'top_k', 'repeat_penalty', 'presence_penalty', 'min_p'}:
            raise ValueError('Unsupported sampling override')
        payload.update(sampling_overrides)
        sampling_policy = 'reviewed_package_recipe'
    row['outputTokenBudget'] = payload['max_tokens']
    row['nativeSamplingRequested'] = native_sampling
    row['samplingPolicy'] = sampling_policy
    row['samplingParameters'] = {k:payload[k] for k in ['temperature','top_p','top_k','repeat_penalty','presence_penalty']}
    if case.get('schema'):
        payload['response_format'] = {'type':'json_schema', 'json_schema':{'name':'detached_case','strict':True,'schema':case['schema']}}
    elif case.get('response_format'):
        payload['response_format'] = case['response_format']
    prefix = f"{server.label}_{case['id']}_{sample}"
    save(OUT / (prefix + '_request.json'), payload)
    started, content, reasoning, chunks = time.monotonic(), [], [], []
    first_content, first_any, finish, usage, timings = None, None, None, None, None
    stream_done = False
    samples, monitor_stop = [], threading.Event()
    def monitor():
        while not monitor_stop.is_set():
            try:
                samples.append(dict(seconds=time.monotonic()-started, gpu=gpu(),
                    rssBytes=psutil.Process(server.process.pid).memory_info().rss))
            except Exception as e:
                samples.append({'error':str(e)})
            monitor_stop.wait(1)
    thread = threading.Thread(target=monitor, daemon=True)
    thread.start()
    try:
        rendered = request(server.base, '/apply-template', dict(messages=messages, chat_template_kwargs=server.template_kwargs))
        tokens = request(server.base, '/tokenize', {'content':rendered['prompt'], 'add_special':False})
        row['inputTokensChecked'] = len(tokens['tokens'])
        if row['inputTokensChecked'] + payload['max_tokens'] + 256 > server.context:
            raise ValueError('Required prompt/output/safety budget exceeds preserved context; fixture rejected whole')
        wire = urllib.request.Request(server.base + '/v1/chat/completions', data=json.dumps(payload,ensure_ascii=False).encode('utf-8'),
            headers={'Content-Type':'application/json'})
        row['preflightSeconds'] = round(time.monotonic()-started,3)
        started = time.monotonic()
        with urllib.request.urlopen(wire, timeout=90) as response:
            for raw in response:
                if time.monotonic()-started > 240:
                    raise TimeoutError('Fixture wall-time budget exceeded')
                if not raw.startswith(b'data:'):
                    continue
                raw = raw[5:].strip()
                if raw == b'[DONE]':
                    stream_done = True
                    break
                chunk = json.loads(raw)
                chunks.append(chunk)
                if 'error' in chunk:
                    raise RuntimeError(str(chunk['error']))
                choice = (chunk.get('choices') or [{}])[0]
                delta = choice.get('delta',{})
                text, think = delta.get('content') or '', delta.get('reasoning_content') or ''
                if (text or think) and first_any is None:
                    first_any = time.monotonic()-started
                if text and first_content is None:
                    first_content = time.monotonic()-started
                content.append(text); reasoning.append(think)
                finish = choice.get('finish_reason') or finish
                usage = chunk.get('usage') or usage
                timings = chunk.get('timings') or timings
        text = ''.join(content)
        completed = bool(text.strip()) and stream_done and finish in ['stop','eos']
        row.update(success=completed, transportSuccess=completed, streamDone=stream_done,
            acceptedResult=None, raw=text, reasoning=''.join(reasoning),
            seconds=round(time.monotonic()-started,3), firstVisibleSeconds=first_content, firstAnySeconds=first_any,
            finishReason=finish, usage=usage, timings=timings, error=None)
        if case.get('schema') or case.get('response_format'):
            try:
                parsed = strict_json(text)
                row['jsonValid'] = isinstance(parsed,dict)
                if case.get('schema'):
                    try:
                        jsonschema.validate(parsed,case['schema']); row['schemaValid'] = True
                    except jsonschema.ValidationError as error:
                        row.update(schemaValid=False, validationError=str(error)[:1500])
                if 'expected' in case:
                    row['semanticPass'] = parsed == case['expected']
                    row['semanticDifferences'] = {k:{'expected':v,'actual':parsed.get(k)} for k,v in case['expected'].items() if parsed.get(k)!=v}
                    row['acceptedResult'] = completed and row.get('schemaValid',False) and row['semanticPass']
            except Exception as e:
                row.update(jsonValid=False, schemaValid=False, semanticPass=False, validationError=str(e)[:1500])
                row['acceptedResult'] = False
        if case['track'] in ['roleplay', 'goddess_current_source']:
            row['manualReviewRequired'] = True
            row['presentationLeak'] = bool(re.search(r'\b(schema|JSON|language model|system prompt|snapshot)\b',text,re.I))
    except Exception as e:
        details = e.read().decode('utf-8',errors='replace')[:1800] if isinstance(e,urllib.error.HTTPError) else str(e)
        row.update(success=False, raw=''.join(content), reasoning=''.join(reasoning), seconds=round(time.monotonic()-started,3), error=details)
    finally:
        monitor_stop.set(); thread.join(timeout=6)
        row['memorySamples'] = samples
        row['peakGpuUsedMiB'] = max((v['gpu']['usedMiB'] for v in samples if 'gpu' in v),default=None)
        row['peakRssBytes'] = max((v['rssBytes'] for v in samples if 'rssBytes' in v),default=None)
        save(OUT / (prefix + '_reply.json'), dict(measurement=row, chunks=chunks, streamDone=stream_done))
        with (OUT / 'measurements.jsonl').open('a',encoding='utf-8') as f:
            f.write(json.dumps(row,ensure_ascii=False)+'\n')
        event('fixture-finished', model=row['modelId'], fixture=case['id'], sample=sample,
              seconds=row['seconds'], success=row['success'], semanticPass=row.get('semanticPass'),
              error=row['error'], firstVisibleSeconds=row.get('firstVisibleSeconds'), peakGpuUsedMiB=row['peakGpuUsedMiB'])
    return row


def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--out',required=True)
    parser.add_argument('--phase',default='probe')
    parser.add_argument('--models',default='qwen35-current')
    parser.add_argument('--fixtures',default='reconstruct_conflicting_prose,reasoning_route_constraints,dialogue_vey_uncertainty')
    parser.add_argument('--samples',type=int,default=1)
    parser.add_argument('--cpu',action='store_true')
    parser.add_argument('--context',type=int,default=12288)
    parser.add_argument('--headroom',type=int,default=768)
    parser.add_argument('--cache',action='store_true')
    parser.add_argument('--mtp',action='store_true')
    parser.add_argument('--greedy',action='store_true')
    parser.add_argument('--production-engine',action='store_true')
    parser.add_argument('--thinking',choices=['off','on','auto'],default='off')
    parser.add_argument('--output-extra',type=int,default=0)
    parser.add_argument('--native-sampling',action='store_true')
    parser.add_argument('--extra-cases')
    parser.add_argument('--reasoning-budget',type=int,default=-1)
    parser.add_argument('--kv-type',choices=['f16','q8_0','q4_0'],default='f16')
    parser.add_argument('--verbosity', type=int, default=3)
    args=parser.parse_args()
    if not re.fullmatch('[a-z0-9_-]+', args.phase) or args.output_extra < 0:
        raise ValueError('Phase must be a safe filename component and extra output budget nonnegative')
    OUT=Path(args.out).resolve()
    if ROOT not in OUT.parents:
        raise ValueError('Output must stay inside workspace')
    OUT.mkdir(parents=True,exist_ok=True)
    manifest=json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
    all_models={m['id']:m for m in manifest['controls']+manifest['models']}
    models=[all_models[key] for key in args.models.split(',')]
    cases=build_cases(ROOT)
    if args.extra_cases:
        extra_path = Path(args.extra_cases).resolve()
        if ROOT not in extra_path.parents:
            raise ValueError('Extra detached fixtures must be inside workspace')
        extra = json.loads(extra_path.read_text(encoding='utf-8-sig'))
        extra_cases = extra['cases'] if isinstance(extra,dict) else extra
        for case in extra_cases:
            case.setdefault('max_tokens',650)
            case.setdefault('temperature',.7)
        cases += extra_cases
    save(OUT/('fixtures_'+args.phase+'.json'),cases)
    source_identity = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in [Path(__file__).resolve(),Path(__file__).with_name('fixtures.py'),MANIFEST]}
    save(OUT/('source_identity_'+str(time.time_ns())+'.json'),source_identity)
    requested=set(args.fixtures.split(','))
    selected=[c for c in cases if c['id'] in requested]
    if len(selected)!=len(requested):
        raise ValueError('Unknown fixture requested')
    engine=Path(manifest['runtime']['productionExecutable'] if args.production_engine else manifest['runtime']['destinationDirectory']+'/llama-server.exe')
    inventory=[]
    for model in models:
        path=Path(model['localPath'])
        if path.stat().st_size!=model['sizeBytes']:
            raise ValueError('Model size changed: '+model['id'])
        meta=metadata(path)
        inventory.append(dict(id=model['id'],path=str(path),sizeBytes=path.stat().st_size,metadata=meta))
    save(OUT/('inventory_'+args.phase+'_'+str(int(time.time()))+'.json'),inventory)
    event('phase-start',phase=args.phase,models=[m['id'] for m in models],fixtures=[c['id'] for c in selected],
          cpu=args.cpu,headroom=args.headroom,context=args.context,thinking=args.thinking,
          outputExtra=args.output_extra,baseline=gpu(),
          unityRunning=any(p.info['name']=='Unity.exe' for p in psutil.process_iter(['name'])),
          evidence='Standalone detached inference. Reserved headroom is a diagnostic, not Unity gameplay/frame-time proof.')
    if args.phase=='preflight':
        return
    for model in models:
        label=args.phase+'_'+model['id']+('_cpu' if args.cpu else '_gpu')+('_production' if args.production_engine else '')+('_mtp' if args.mtp else '')+('_cache' if args.cache else '')+f'_h{args.headroom}'
        try:
            with Server(model,engine,args.cpu,args.context,args.headroom,args.cache,args.mtp,label=label,
                        thinking=args.thinking,reasoning_budget=args.reasoning_budget,kv_type=args.kv_type,verbosity=args.verbosity) as server:
                for case in selected:
                    for sample in range(args.samples):
                        call(server,case,sample,args.greedy,args.output_extra,args.native_sampling)
        except Exception as e:
            event('model-failed',model=model['id'],error=str(e))
    event('phase-finished',phase=args.phase,gpu=gpu())


if __name__=='__main__':
    main()
