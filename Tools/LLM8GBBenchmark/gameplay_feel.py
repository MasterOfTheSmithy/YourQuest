"""Compare model consolidation, current Goddess contracts, and model-free speculation."""
import copy
import re
import time
from pathlib import Path
import psutil
import run as bench
from coexistence import read, ready
from staged_model import staged_model
from schema_prompt import with_visible_schema

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmGameplayFeel_20261008'


def main():
    # note: Wait only for the explicit benchmark resume already requested by this task; never launch or restart Unity here.
    deadline = time.monotonic() + 300
    while True:
        try:
            ready()
            break
        except RuntimeError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(1)
    bench.OUT = OUT / 'Inference'
    bench.OUT.mkdir(exist_ok=True)
    inventory = read(ROOT / 'outputs/LlmRequalification_20261008/Inventory.json')
    fixtures = read(ROOT / 'outputs/LlmRequalification_20261008/FullSweep_20261008_172243/Fixtures.json')
    goddess = read(OUT / 'GoddessCases.json')
    roles = [f for f in fixtures if f['track'] in ('roleplay', 'summary')]
    rows, stages = [], []
    for model_id in ('Spark-X2.5-4B-Q4_K_M.gguf', 'Qwen3.5-4B-Q4_K_M.gguf'):
        ready()
        if any((p.info['name'] or '').lower() == 'llama-server.exe' for p in psutil.process_iter(['name'])):
            raise RuntimeError('Another llama server is resident')
        model = next(m for m in inventory if m['id'] == model_id)
        stage = dict(model=model_id)
        stages.append(stage)
        try:
            # note: Each model is copied once; both inference variants use that same verified file, then the context deletes it.
            with staged_model(model, OUT / 'SSDStage', stage) as staged:
                for mode in ('none', 'ngram-simple'):
                    cases = roles + goddess if mode == 'none' else roles
                    label = ('spark' if model_id.startswith('Spark') else 'qwen') + '_' + mode
                    with bench.Server(staged, Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                                      cpu=False, context=4096 if model_id.startswith('Spark') else 12288,
                                      headroom=1536, cache=True, gpu_layers=999, kv_type='q8_0',
                                      cache_ram_mib=256, checkpoints=2, checkpoint_min_step=256,
                                      verbosity=5, reasoning_budget=0, thinking='off', label=label, spec_type=mode) as server:
                        layers = re.findall(r'offloaded (\d+)/(\d+) layers to GPU', server.log_path.read_text(encoding='utf-8', errors='replace'))
                        if not layers or layers[-1][0] != layers[-1][1]:
                            raise RuntimeError('Full GPU layer placement unverified')
                        stage.setdefault('loads', []).append(dict(mode=mode, seconds=server.load_seconds, layers=layers[-1], gpu=bench.gpu()))
                        for fixture in cases:
                            before = ready()
                            if bench.gpu()['freeMiB'] < 1536:
                                raise RuntimeError('Reserve unavailable before inference')
                            case = with_visible_schema(copy.deepcopy(fixture))
                            if fixture['track'] == 'roleplay':
                                case['messages'].insert(0, dict(role='system', content=
                                    'Speak only as the NPC identified in the supplied context. The player is a different person. '
                                    'Use accepted facts as facts; rumors, proposals, plans and unknowns remain uncertain. '
                                    'Do not invent observations, completed actions, player speech, history or promises. Follow the requested output contract.'))
                            row = bench.call(server, case, sample=0, native_sampling=False,
                                             sampling_overrides=dict(temperature=0 if fixture['track'] in ('goddess_review', 'summary') else .2,
                                                                     top_p=.8, top_k=20, presence_penalty=0, repeat_penalty=1))
                            rows.append(dict(model=model_id, mode=mode, id=fixture['id'], measurement=row,
                                             expectedUnsupported=fixture.get('expectedUnsupported'), unity=ready(before['renderedFrames'])))
                            bench.save(OUT / 'Cases.json', rows)
                            if bench.gpu()['freeMiB'] < 1536:
                                raise RuntimeError('Reserve crossed during inference; ending residency')
            stage['status'] = 'COMPLETE'
        except Exception as error:
            stage.update(status='BLOCKED', error=str(error))
        bench.save(OUT / 'Staging.json', stages)
    bench.save(OUT / 'Complete.json', dict(stages=stages, calls=len(rows),
               evidence='Detached inference beside rendered Unity; no accepted state mutation, ordinary dialogue delivery or automatic qualification'))


if __name__ == '__main__':
    main()
