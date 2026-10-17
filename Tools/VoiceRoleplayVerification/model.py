"""Comparable before/after text samples on the configured control; never certify microphone or gameplay."""
import argparse, importlib.util, json, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--before', type=Path)
    p.add_argument('--after', type=Path)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--samples', type=int, default=2)
    p.add_argument('--gpu-layers', type=int)
    args = p.parse_args()
    if args.before is None and args.after is None: raise ValueError('At least one explicit packet file required')
    out = args.out.resolve()
    if not out.is_relative_to(ROOT / 'outputs') or out.exists(): raise ValueError('Fresh workspace output required')
    out.mkdir(parents=True)
    sys.path.insert(0, str(ROOT / 'Tools/LLM8GBBenchmark'))
    spec = importlib.util.spec_from_file_location('owned_voice_benchmark', ROOT / 'Tools/LLM8GBBenchmark/run.py')
    bench = importlib.util.module_from_spec(spec); spec.loader.exec_module(bench); bench.OUT = out
    rows = []
    # note: Same local control, context, sampler and owned process. No shared production server, install, staging rotation or active saves.
    model = dict(id='voice-roleplay-control', localPath=r'C:\Ai\YourQuestRuntime\Models\Qwen3.5-4B-Q4_K_M.gguf')
    engine = Path(r'C:\Ai\YourQuestRuntime\Runtime\b10828-cuda13.3\llama-server.exe')
    try:
        with bench.Server(model, engine, context=12288, headroom=1536, port=11465, thinking='off',
                          reasoning_budget=0, kv_type='q8_0', cache=True, cache_ram_mib=256, checkpoints=2,
                          checkpoint_min_step=256, gpu_layers=args.gpu_layers) as server:
            if server.loaded['freeMiB'] < 1536:
                raise RuntimeError('Measured GPU reserve below 1536 MiB; no dialogue benchmark admitted')
            for label, packets in [('before', args.before), ('after', args.after)]:
                if packets is None: continue
                for case in json.loads(packets.read_text(encoding='utf-8-sig')):
                    for sample in range(args.samples):
                        payload = dict(model='local', messages=[dict(role='system', content='Speak only as the NPC identified in the supplied context. The player is a different person. Use accepted facts as facts; rumors, proposals, plans and unknowns remain uncertain. Do not invent observations, completed actions, player speech, history or promises. Follow the requested output contract.'), dict(role='user', content=case['prompt'])],
                            temperature=.28, top_p=.8, top_k=20, repeat_penalty=1, presence_penalty=0,
                            max_tokens=160, seed=100+sample, response_format=dict(type='json_schema', json_schema=dict(name='npc_reply', strict=True, schema=case['schema'])))
                        began=time.perf_counter()
                        try:
                            result=bench.request(server.base, '/v1/chat/completions', payload, timeout=60)
                            raw=result['choices'][0]['message']['content']; parsed=bench.strict_json(raw)
                            row=dict(label=label, id=case['id'], sample=sample, player=case['text'], raw=raw, parsed=parsed,
                                     seconds=time.perf_counter()-began, usage=result.get('usage'), expected_act=case['act'], expected_target=case['target'])
                        except Exception as error:
                            row=dict(label=label,id=case['id'],sample=sample,error=str(error),seconds=time.perf_counter()-began)
                        rows.append(row)
                        (out/'dialogue.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
                        print(json.dumps(row,ensure_ascii=False),flush=True)
            (out/'environment.json').write_text(json.dumps(dict(load_seconds=server.load_seconds,gpu_baseline=server.baseline,gpu_loaded=server.loaded,
                evidence='DETACHED_LOCAL_MODEL_TEXT_ONLY; Unity foreground state/frame time, real microphone, playback and action acceptance are NOT VERIFIED'),indent=2))
    except Exception as error:
        (out/'failure.json').write_text(json.dumps(dict(error=str(error),evidence='NO_MODEL_RESULT'),indent=2))
        raise

if __name__ == '__main__': main()
