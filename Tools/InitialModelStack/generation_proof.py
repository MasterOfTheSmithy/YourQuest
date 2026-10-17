"""Retained production generator, isolated fixtures; no saves or external processes are changed."""
import hashlib
import argparse
import json
import re
import sys
import time
from pathlib import Path
import jsonschema

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/LLM8GBBenchmark'))
import run as benchmark


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--fixture', choices=['attributed_memory', 'polite_offer_refusal'])
    selected = parser.parse_args().fixture
    out = ROOT / 'outputs/InitialModelStack/generation'
    out.mkdir(parents=True, exist_ok=True)
    benchmark.OUT = out
    model = Path(r'C:\Ai\YourQuestRuntime\Models\Qwen3.5-4B-Q4_K_M.gguf')
    engine = Path(r'C:\Ai\YourQuestRuntime\Runtime\b10828-cuda13.3\llama-server.exe')
    digest = hashlib.sha256()
    with model.open('rb') as source:
        while block := source.read(8 * 1024 * 1024):
            digest.update(block)
    assert digest.hexdigest() == '3874209241c9a397e2f62cd3f70f80fd2dfbf0dfccb6838416bdb48a714e8630'
    rows = []
    config = (ROOT / 'Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs').read_text(encoding='utf-8-sig')
    profile = re.search(r'GetProfile\(LLMGenerationCategory.Dialogue\)\.systemPrompt\s*=\s*(.*?);', config, re.S)
    assert profile
    system = ''.join(json.loads(value) for value in re.findall(r'"(?:[^"\\]|\\.)*"', profile.group(1)))
    # note: Match the retained context and GPU/KV configuration, using a private owned server and port.
    with benchmark.Server({'id': 'qwen35-retained', 'localPath': str(model)}, engine,
                          context=12288, headroom=1536, gpu_layers=999,
                          kv_type='q8_0', thinking='off', port=11475) as server:
        for fixture in json.loads((out / 'fixtures.json').read_text()):
            if selected and fixture['id'] != selected:
                continue
            began = time.perf_counter()
            payload = dict(messages=[{'role': 'system', 'content': system}, {'role': 'user', 'content': fixture['prompt']}],
                           max_tokens=160, temperature=.28, top_p=.8, top_k=20, repeat_penalty=1, presence_penalty=0, seed=42,
                           chat_template_kwargs=server.template_kwargs,
                           response_format={'type': 'json_schema', 'json_schema': {
                               'name': 'production_dialogue', 'strict': True, 'schema': fixture['schema']}})
            reply = benchmark.request(server.base, '/v1/chat/completions', payload, timeout=90)
            raw = reply['choices'][0]['message']['content']
            parsed = benchmark.strict_json(raw)
            jsonschema.validate(parsed, fixture['schema'])
            rows.append(dict(id=fixture['id'], raw=raw, expectedDecision=fixture['expectedDecision'],
                             seconds=time.perf_counter() - began))
            (out / 'responses.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
            assert parsed.get('offerDecision', 'none') == fixture['expectedDecision'], parsed
            if fixture['id'] == 'attributed_memory':
                assert 'chapel' in parsed['npcText'].lower() and 'altar' in parsed['npcText'].lower(), parsed
            print('PASS retained generator:', fixture['id'], raw, flush=True)
    receipt = dict(evidence='Detached generation fixtures using production prompt/schema. Not full semantic qualification, save acceptance or PlaySafe proof.',
                   model=str(model), model_sha256=digest.hexdigest(), rows=rows)
    (out / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    (out / 'responses.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
