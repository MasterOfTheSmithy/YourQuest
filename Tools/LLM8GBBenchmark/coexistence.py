"""One staged model, one residency, all roles plus bounded repairs alongside a ready Unity world."""
import argparse
import copy
import json
import re
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

import psutil
import run as bench
from category_cases import build_role_cases
from fixtures import build_cases
from schema_prompt import with_visible_schema
from staged_model import staged_model

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/LlmRequalification_20261008'
SOURCE_KINDS = dict(origin_current_schema_synthetic='origin', world_current_schema_synthetic='world',
                    frontier_settlement_compatible_replay='frontier', quest_empty_evidence_compatible_replay='quest')


def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def ready(after_frame=None):
    # note: A live, fresh, released and rendered world is mandatory before and after every inference episode.
    state = read(BASE / 'UnityBaseline/Current.json')
    # note: Subsecond inference can finish between two-second Editor receipts; wait for the next receipt, not an artificial frame failure.
    deadline = time.monotonic() + 6
    while after_frame is not None and state['renderedFrames'] <= after_frame and time.monotonic() < deadline:
        time.sleep(.15)
        state = read(BASE / 'UnityBaseline/Current.json')
    stamp = re.sub(r'(\.\d{6})\d+', r'\1', state['utc']).replace('Z', '+00:00')
    age = (datetime.now(timezone.utc) - datetime.fromisoformat(stamp)).total_seconds()
    if age > 15 or state['status'] != 'READY' or not all(state[k] for k in ['playMode','materialized','gameplayReleased','isolation']):
        raise RuntimeError('Unity is not a fresh, ready coexistence baseline')
    if state['inferenceDispatches'] != 0 or not psutil.pid_exists(state['processId']):
        raise RuntimeError('Unity ownership/isolation changed')
    if after_frame is not None and state['renderedFrames'] <= after_frame:
        raise RuntimeError('Unity rendering did not advance within the receipt window')
    return state


def source_check(case, raw, serial):
    goddess = case['id'].startswith('goddess_')
    if not goddess and case['id'] not in SOURCE_KINDS: return None
    name = 'GoddessSourceHarness' if goddess else 'StructuredSourceHarness'
    home = BASE / name
    folder = home / 'CoexistenceChecks'; folder.mkdir(exist_ok=True)
    incoming, outgoing = folder / (serial + '.txt'), folder / (serial + '.json')
    incoming.write_text(raw, encoding='utf-8')
    args = ['dotnet', str(home / f'bin/Release/net10.0/{name}.dll'), str(home), 'check',
            case['id'] if goddess else SOURCE_KINDS[case['id']], str(incoming), str(outgoing)]
    result = subprocess.run(args, capture_output=True, text=True, timeout=30, creationflags=bench.FLAGS)
    if result.returncode not in (0, 2) or not outgoing.exists():
        raise RuntimeError('Source validator failed: ' + result.stderr[:600])
    return read(outgoing)


def validate(case, row, check):
    if not row.get('success'): return False, 'The response was incomplete. Return a complete concise answer within the output budget.'
    if case.get('schema') and not row.get('schemaValid'): return False, row.get('validationError', 'Output schema mismatch.')
    if 'expected' in case and not row.get('semanticPass'):
        # note: Feedback identifies incorrect fields, never supplies held-out expected answers.
        return False, 'Re-read authoritative input; these fields disagree: ' + ', '.join(row.get('semanticDifferences', {}))
    if check:
        for key in ['candidateEnvelopeProseValid','currentTransportSchemaValid','rawDtoSourceChecksPass',
                    'sourceTokenValidatorPass','distinctNamesDiagnostic','historicalSeedPreserved','emptyEvidenceGoldenPass']:
            if key in check and not check[key]:
                diagnostic = '; '.join(str(v) for k,v in check.items() if 'diagnostic' in k.lower() and v)
                return False, diagnostic or key + ' rejected the proposal; re-read context and requirements.'
        if check.get('sourceSanitizerWouldRemoveRuntimeAuthority'):
            return False, 'Remove generated runtime authority fields; this is a content proposal only.'
    return True, ''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--models', nargs='+', required=True, help='Exact inventory filenames; one complete suite per model')
    args = parser.parse_args()
    out = BASE / ('Coexistence_' + time.strftime('%Y%m%d_%H%M%S'))
    out.mkdir(); bench.OUT = out
    inventory = {m['id']:m for m in read(BASE / 'Inventory.json')}
    models = [inventory[key] for key in args.models]
    # note: Only recipes actually reviewed for this first cohort are admitted; new packages require an explicit recipe review.
    recipes = {
        'LFM-2.5-1.2b-Instruct-roleplay-tuned-v2.Q8_0.gguf': ('off', dict(temperature=.1, top_k=50, top_p=1, repeat_penalty=1.05)),
        'LFM2.5-2.6B-Q4_K_M.gguf': ('on', dict(temperature=.1, top_k=50, top_p=1, repeat_penalty=1.1)),
        'Qwen3.5-4B-Q4_K_M.gguf': ('off', None),
        'Spark-X2.5-4B-Q4_K_M.gguf': ('off', None),
    }
    if any(m['id'] not in recipes for m in models): raise ValueError('Review this package recipe before running it')
    cases = [c for c in build_cases(ROOT) if c['track'] != 'exported_payload']
    cases += read(BASE / 'StructuredSourceHarness/RunnerCompatibleCases.json')['cases']
    cases += read(BASE / 'GoddessSourceHarness/CurrentSourceCases.json')['cases']
    cases += build_role_cases()
    for case in cases:
        case.setdefault('max_tokens', 650); case.setdefault('temperature', .55)
    cases = [with_visible_schema(c) for c in cases]
    bench.save(out / 'Fixtures.json', cases)
    bench.save(out / 'Plan.json', dict(models=models, recipes=recipes, cases=[c['id'] for c in cases],
        context=12288, freeVramTargetMiB=1024, evidence='Stationary rendered Unity coexistence; external inference and detached validators. Not Unity speech delivery or traversal acceptance.'))
    episodes, stages = [], []
    for ordinal, model in enumerate(models):
        before = ready()
        if any(p.info['name'].lower() == 'llama-server.exe' for p in psutil.process_iter(['name']) if p.info['name']):
            raise RuntimeError('An existing llama server would contaminate this test')
        try:
            if bench.request('http://127.0.0.1:11434', '/api/ps', timeout=3).get('models'):
                raise RuntimeError('Ollama has a resident model')
        except OSError: pass
        stage = dict(model=model['id']); stages.append(stage)
        server = None
        try:
            with staged_model(model, out / 'SSDStage', stage) as staged:
                thinking, sampling = recipes[model['id']]
                label = f'm{ordinal:02d}'
                with bench.Server(staged, Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                                  context=12288, headroom=1024, cache=True, thinking=thinking,
                                  reasoning_budget=768 if thinking == 'on' else -1, label=label) as server:
                    stage.update(serverPid=server.process.pid, loadSeconds=server.load_seconds, gpuLoaded=server.loaded)
                    for case_index, case in enumerate(cases):
                        episode = dict(modelId=model['id'], fixture=case['id'], track=case['track'], unityBefore=ready(), attempts=[])
                        current = copy.deepcopy(case)
                        for attempt in range(3):
                            row = bench.call(server, current, sample=attempt, native_sampling=True,
                                             output_extra=1024 if thinking == 'on' else 0, sampling_overrides=sampling)
                            serial = out.name + f'_{ordinal}_{case_index}_{attempt}'
                            check = source_check(case, row.get('raw',''), serial) if row.get('success') else None
                            accepted, diagnostic = validate(case, row, check)
                            episode['attempts'].append(dict(measurement=row, sourceCheck=check, acceptedByDetachedChecks=accepted, diagnostic=diagnostic))
                            if accepted: break
                            current = copy.deepcopy(case)
                            current['messages'] += [dict(role='assistant', content=row.get('raw','')),
                                dict(role='user', content='Repair the previous proposal using the original context and schema. '+diagnostic)]
                        episode.update(acceptedByDetachedChecks=accepted, unityAfter=ready(episode['unityBefore']['renderedFrames']),
                            independentSemanticReviewRequired='expected' not in case,
                            seconds=sum(a['measurement']['seconds'] for a in episode['attempts']))
                        episodes.append(episode); bench.save(out / 'Episodes.json', episodes)
                        bench.event('episode-finished', model=model['id'], fixture=case['id'], attempts=len(episode['attempts']), accepted=accepted)
                        if episode['unityAfter']['renderedFrames'] <= episode['unityBefore']['renderedFrames']:
                            raise RuntimeError('Unity rendering did not advance during the episode')
                if server.process.poll() is None: raise RuntimeError('Owned server did not stop')
            stage.update(gpuAfterClose=bench.gpu(), unityAfter=ready())
        finally:
            bench.save(out / 'Staging.json', stages)
        bench.event('model-suite-finished', model=model['id'], temporaryCopyDeleted=stage.get('temporaryCopyDeleted'))
    bench.save(out / 'Complete.json', dict(utc=bench.utc(), models=len(models), episodes=len(episodes), unity=ready(), stages=stages))


if __name__ == '__main__': main()
