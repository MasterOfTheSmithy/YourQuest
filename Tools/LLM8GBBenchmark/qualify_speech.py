"""Bounded source-export speech tests; never publishes a qualification or changes saves."""
import copy
import json
from pathlib import Path
import psutil
import run as bench
from staged_model import staged_model
from schema_prompt import with_visible_schema

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmReimplementation_20261008'


def main():
    # note: This is detached model evidence. The Editor must remain isolated until normal runtime delivery tests explicitly begin.
    if any((p.info['name'] or '').lower() == 'llama-server.exe' for p in psutil.process_iter(['name'])):
        raise RuntimeError('Another llama server is already running')
    bench.OUT = OUT / 'SpeechInference'
    bench.OUT.mkdir(parents=True, exist_ok=True)
    inventory = json.loads((ROOT / 'outputs/LlmRequalification_20261008/Inventory.json').read_text(encoding='utf-8-sig'))
    cases = json.loads((ROOT / 'outputs/LlmGameplayFeel_20261008/GoddessCases.json').read_text(encoding='utf-8-sig'))
    model = next(m for m in inventory if m['id'] == 'Qwen3.5-4B-Q4_K_M.gguf')
    rows, stage = [], {}
    with staged_model(model, OUT / 'SSDStage', stage) as staged:
        with bench.Server(staged, Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                          context=12288, headroom=1536, cache=True, gpu_layers=999, kv_type='q8_0',
                          cache_ram_mib=256, checkpoints=2, checkpoint_min_step=256,
                          reasoning_budget=1024, thinking='on', label='qwen_speech_reasoning') as server:
            for fixture in cases:
                if bench.gpu()['freeMiB'] < 1536:
                    raise RuntimeError('1.5 GiB reserve unavailable')
                case = with_visible_schema(copy.deepcopy(fixture))
                # note: Exercise author direct mode and reviewer reasoning on one server to measure consolidation without reloading weights.
                review = fixture['track'] == 'goddess_review'
                server.template_kwargs = {'enable_thinking': review}
                row = bench.call(server, case, native_sampling=False, output_extra=1024 if review else 0,
                                 sampling_overrides=dict(temperature=.2 if not review else .6, top_p=.95,
                                                         top_k=20, presence_penalty=0, repeat_penalty=1))
                rows.append(dict(id=fixture['id'], track=fixture['track'], measurement=row,
                                 expectedUnsupported=fixture.get('expectedUnsupported')))
                bench.save(OUT / 'SpeechCases.json', rows)
                print(fixture['id'], row.get('raw'), flush=True)
    bench.save(OUT / 'SpeechStage.json', stage)
    bench.save(OUT / 'SpeechCleanup.json', dict(gpu=bench.gpu(),
               servers=[p.info for p in psutil.process_iter(['name', 'pid']) if (p.info['name'] or '').lower() == 'llama-server.exe']))


if __name__ == '__main__':
    main()
