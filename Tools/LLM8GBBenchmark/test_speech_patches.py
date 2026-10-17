"""Bounded semantic controls using production-exported prompts; never qualifies runtime config."""
import copy
import json
import sys
from pathlib import Path
import run as bench
from staged_model import staged_model

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/SpeechPatch_20261008'
OUT = BASE / sys.argv[1] if len(sys.argv) > 1 else BASE


def apply_patch(original, raw):
    # note: This runner mirrors only span application; the Editor replays receipts through the real C# parser afterwards.
    data = bench.strict_json(raw)
    if set(data) != {'edits'} or not isinstance(data['edits'], list) or len(data['edits']) > 6:
        raise ValueError('Invalid patch object')
    spans = []
    for edit in data['edits']:
        if set(edit) != {'issue', 'from', 'to'} or not all(isinstance(v, str) for v in edit.values()):
            raise ValueError('Invalid edit')
        before, after = edit['from'], edit['to']
        if not before or not edit['issue'].strip() or before == after or original.count(before) != 1:
            raise ValueError('Ambiguous or empty edit')
        start = original.index(before)
        if any(start < s + n and s < start + len(before) for s, n, _ in spans):
            raise ValueError('Overlapping edit')
        spans.append((start, len(before), after))
    patched = original
    for start, length, replacement in sorted(spans, reverse=True):
        patched = patched[:start] + replacement + patched[start + length:]
    return patched, bool(spans)


def main():
    bench.OUT = OUT / 'Inference'
    bench.OUT.mkdir(parents=True, exist_ok=True)
    inventory = json.loads((ROOT / 'outputs/LlmRequalification_20261008/Inventory.json').read_text(encoding='utf-8-sig'))
    cases = json.loads((BASE / 'Cases.json').read_text(encoding='utf-8-sig'))
    bench.save(OUT / 'Cases.json', cases)
    model = next(m for m in inventory if m['id'] == 'Qwen3.5-4B-Q4_K_M.gguf')
    stage, results = {}, []
    reasoning = 512 if len(sys.argv) > 2 and sys.argv[2] == 'reasoning' else 0
    try:
        with staged_model(model, OUT / 'SSDStage', stage) as staged:
            with bench.Server(staged, Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                              context=12288, headroom=1536, cache=True, gpu_layers=999, kv_type='q8_0',
                              reasoning_budget=reasoning, thinking='on' if reasoning else 'off', label='qwen_span_patch') as server:
                for case in cases:
                    text = case['original']
                    result = dict(id=case['id'], original=text, expectChange=case['expectChange'], calls=[], accepted=False)
                    for attempt in range(3):
                        if bench.gpu()['freeMiB'] < 1536:
                            raise RuntimeError('1536 MiB reserve unavailable')
                        request = copy.deepcopy(case)
                        request['max_tokens'] += reasoning
                        prompt, data = request['messages'][0]['content'].split('PATCH_DATA_JSON\n')
                        data = json.loads(data); data['candidate'] = text
                        request['messages'][0]['content'] = prompt + 'PATCH_DATA_JSON\n' + json.dumps(data, ensure_ascii=False, separators=(',', ':'))
                        row = bench.call(server, request, sample=0, sampling_overrides=dict(
                            temperature=0, top_p=.8, top_k=20, presence_penalty=0, repeat_penalty=1))
                        result['calls'].append(row)
                        if not row.get('success') or not row.get('schemaValid'):
                            break
                        try:
                            patched, changed = apply_patch(text, row['raw'])
                        except ValueError as error:
                            result['patchError'] = str(error); break
                        if not changed:
                            result['accepted'] = True; break
                        if attempt < 2:
                            text = patched
                    result.update(final=text, changed=text != case['original'],
                                  preservedControl=not case['keep'] or case['keep'] in text,
                                  seconds=sum(r['seconds'] for r in result['calls']))
                    results.append(result)
                    bench.save(OUT / 'Results.json', results)
                    print(json.dumps({k: result[k] for k in ['id', 'accepted', 'changed', 'final', 'seconds']}), flush=True)
    finally:
        bench.save(OUT / 'Stage.json', stage)


if __name__ == '__main__':
    main()
