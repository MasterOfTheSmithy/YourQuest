"""Bounded live test using production-source launch arguments and wire packets on private ports."""
import json
import re
import socket
import subprocess
import time
from pathlib import Path

import jsonschema
import run as bench
from methods import OwnedOllama
from fixtures import build_cases

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/LlmLatencyImplementation_20261007'
OUT = BASE / time.strftime('Live_%Y%m%d_%H%M%S')
ENGINE = Path('C:/Ai/llama.cpp/llama-server.exe')
DLL = BASE / 'Checks/bin/Release/net10.0/LatencyChecks.dll'
counter = 0


def source(operation, data):
    global counter
    counter += 1
    incoming, outgoing = OUT / f'source_{counter:03d}_in.json', OUT / f'source_{counter:03d}_out.json'
    bench.save(incoming, data)
    subprocess.run(['dotnet', str(DLL), operation, str(incoming), str(outgoing)], check=True, capture_output=True, timeout=30, creationflags=bench.FLAGS)
    return json.loads(outgoing.read_text(encoding='utf-8-sig'))


class ProductionServer(bench.Server):
    def __init__(self, model, arguments, label):
        super().__init__(model, ENGINE, port=11445, label=label)
        self.arguments = arguments

    def __enter__(self):
        with socket.socket() as check:
            check.bind(('127.0.0.1', self.port))
        self.baseline = bench.gpu()
        self.log = (OUT / (self.label + '.log')).open('w', encoding='utf-8')
        args = [str(ENGINE)] + [part[1:-1] if part.startswith('"') else part for part in re.findall(r'"[^"]*"|\S+', self.arguments)]
        started = time.perf_counter()
        self.process = subprocess.Popen(args, stdout=self.log, stderr=subprocess.STDOUT, creationflags=bench.FLAGS)
        bench.event('production-arguments-start', label=self.label, pid=self.process.pid, arguments=args)
        try:
            while time.perf_counter() - started < 120:
                if self.process.poll() is not None:
                    raise RuntimeError('Owned server exited: ' + self.label)
                try:
                    if bench.request(self.base, '/health', timeout=2).get('status') == 'ok':
                        self.load_seconds = time.perf_counter() - started
                        return self
                except Exception as error:
                    if '10013' in str(error):
                        raise
                time.sleep(.2)
            raise TimeoutError('Private owned server readiness budget exceeded')
        except BaseException:
            self.close()
            raise


def infer(base, endpoint, packet, case, label):
    bench.save(OUT / (label + '_request.json'), packet)
    start = time.perf_counter()
    reply = bench.request(base, endpoint, packet, timeout=180)
    seconds = time.perf_counter() - start
    if endpoint.endswith('/generate'):
        raw = reply.get('response', '')
        complete = reply.get('done') is True and reply.get('done_reason') == 'stop'
    else:
        choice = reply['choices'][0]
        raw = choice['message'].get('content', '')
        complete = choice.get('finish_reason') == 'stop'
    try:
        parsed = bench.strict_json(raw)
        jsonschema.validate(parsed, case['schema'])
        correct = parsed == case['expected']
    except Exception:
        correct = False
    row = dict(label=label,seconds=seconds,complete=complete,correct=correct,raw=raw,reply=reply)
    bench.save(OUT / (label + '_reply.json'), row)
    bench.event('live-call', label=label,seconds=round(seconds,3),correct=correct,complete=complete)
    return row


def main():
    OUT.mkdir(exist_ok=False)
    bench.OUT = OUT
    manifest = json.loads((ROOT / 'Docs/LLM_8GB_Download_Test_Manifest_2026-10-07.json').read_text())
    models = {m['id']:m for m in manifest['controls']}
    cases = {c['id']:c for c in build_cases(ROOT)}
    main_case, cpu_case = cases['reconstruct_long_context'], cases['summary_preserve_uncertainty']
    help_text = (BASE / 'ServerHelp.txt').read_text(encoding='utf-8-sig')
    base_config = dict(serverPort=11445,ollamaApiUrl='http://127.0.0.1:11446',contextSizeTokens=12288)
    def wire(case, enabled, cpu=False):
        return source('wire',dict(config=dict(base_config,enablePromptPrefixReuse=enabled),cpu=cpu,help=help_text,
            prompt=case['messages'][0]['content'],schema=case['schema'],
            options=dict(num_predict=case['max_tokens'],temperature=.05,top_p=.8,top_k=20,repeat_penalty=1.,presence_penalty=0.,num_ctx=8192)))
    wires = {False:wire(main_case,False),True:wire(main_case,True)}
    helper_wire = wire(cpu_case,True,True)['packet']
    rows, transitions = [], []
    with OwnedOllama(models['summary-current'],label='private_cpu_service') as cpu:
        for enabled in [False,True]:
            label = 'retained_cached' if enabled else 'serial_uncached'
            server = None
            try:
                server = ProductionServer(models['qwen35-current'],wires[enabled]['arguments'],label+'_start').__enter__()
                # note: Establish CPU placement before timing warm cycles. Cold startup is recorded separately, never hidden in a warm result.
                if enabled:
                    infer(cpu.base,'/api/generate',helper_wire,cpu_case,label+'_cpu_setup')
                    residency = bench.request(cpu.base,'/api/ps')
                    admission = source('admit',dict(residency=residency))
                    if not admission['allowed']:
                        raise RuntimeError('Actual CPU helper failed production memory/placement admission')
                    bench.save(OUT / 'CpuPlacement.json',dict(residency=residency,admission=admission))
                warmed = False
                for cycle in range(2):
                    start = time.perf_counter()
                    original_pid = server.process.pid
                    packet = dict(wires[enabled]['packet'],cache_prompt=enabled and warmed)
                    if enabled:packet['id_slot']=0
                    first = infer(server.base,'/v1/chat/completions',packet,main_case,f'{label}_{cycle}_main_a')
                    if not enabled:
                        server.close();server=None
                    middle = infer(cpu.base,'/api/generate',helper_wire,cpu_case,f'{label}_{cycle}_cpu')
                    if not enabled:
                        cpu.release()
                        server=ProductionServer(models['qwen35-current'],wires[enabled]['arguments'],f'{label}_{cycle}_reload').__enter__()
                    else:
                        residency=bench.request(cpu.base,'/api/ps')
                        if any(m.get('size_vram') != 0 for m in residency['models']):raise RuntimeError('CPU helper moved to GPU')
                    packet['cache_prompt']=enabled
                    last=infer(server.base,'/v1/chat/completions',packet,main_case,f'{label}_{cycle}_main_b')
                    row=dict(policy=label,cycle=cycle,seconds=time.perf_counter()-start,
                        allCorrect=all(r['correct'] and r['complete'] for r in [first,middle,last]),
                        mainPidBefore=original_pid,mainPidAfter=server.process.pid,requests=[first,middle,last])
                    rows.append(row);warmed=True
                    bench.save(OUT/'Results.json',dict(cycles=rows,transitions=transitions))
                    bench.event('live-cycle',policy=label,cycle=cycle,seconds=round(row['seconds'],3),allCorrect=row['allCorrect'],sameMainPid=original_pid==server.process.pid)
                if enabled:
                    # note: Exercise profile/context changes with the same retained process. The new scope explicitly starts uncached.
                    for index,case_id in enumerate(['reconstruct_cross_profile','reconstruct_cross_profile','reconstruct_long_context']):
                        case=cases[case_id];packet=wire(case,True)['packet'];packet.update(cache_prompt=index==1,id_slot=0)
                        transitions.append(infer(server.base,'/v1/chat/completions',packet,case,f'profile_transition_{index}'))
                cpu.release()
            finally:
                if server is not None:server.close()
    bench.save(OUT/'Results.json',dict(cycles=rows,transitions=transitions,
        evidence='Private live servers; production-source packets/launch arguments and CPU admission; coroutine lifecycle simulated separately; Unity closed',gpuAfterCleanup=bench.gpu()))
    if not all(r['allCorrect'] for r in rows) or not all(r['correct'] and r['complete'] for r in transitions):
        raise RuntimeError('An acceptance case failed; inspect preserved receipts')


if __name__ == '__main__':
    main()
