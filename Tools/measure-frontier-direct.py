"""Compare compact direct frontier briefs against the current-wire baseline, on the same approved model."""
import copy
import json
import sys
import threading
import measure_llm_response_times as benchmark


def main():
    production = '--production' in sys.argv
    if production:
        benchmark.DATA = json.loads((benchmark.OUT / 'Repaired/Cases.json').read_text(encoding='utf-8-sig'))
    baseline = benchmark.used_gpu()
    sampler = threading.Thread(target=benchmark.sample_gpu, daemon=True)
    sampler.start()
    try:
        for cpu in ([False] if production else [True, False]):
            base, prior = benchmark.llama_start(cpu)
            for original in benchmark.DATA['cases']:
                if not original['name'].startswith('Frontier '):
                    continue
                case = copy.deepcopy(original)
                payload = case['payload']
                payload['chat_template_kwargs'] = {'enable_thinking': False}
                # note: The exploratory hint stays within the accepted 2-6 range; production uses the exact exported prompt and unchanged requirements.
                if not production:
                    payload['messages'][0]['content'] = payload['messages'][0]['content'].replace(
                        'Keep prose compact, preferably 3-8 words per field.',
                        'Prefer 2-3 distinct residents in an initial settlement brief. Keep prose compact, preferably 3-8 words per field.')
                case['source'] = 'Exact repaired current production wire' if production else 'Compact direct comparison; same model, canonical schema, unchanged domain validators'
                benchmark.call(case, base, 'gpu-production' if production else ('cpu-direct' if cpu else 'gpu-direct'), 0)
            retiring_pid = benchmark.OWNER.pid
            benchmark.stop_owned(benchmark.OWNER)
            benchmark.OWNER = None
            benchmark.wait_clean(prior, 'direct CPU' if cpu else 'direct GPU', retiring_pid)
    finally:
        benchmark.stop_owned(benchmark.OWNER)
        benchmark.STOP.set()
        sampler.join(timeout=4)
        (benchmark.OUT / ('ProductionResults.json' if production else 'DirectResults.json')).write_text(json.dumps({'baselineMiB': baseline,
            'finalMiB': benchmark.used_gpu(), 'results': benchmark.RESULTS}, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
