"""Recheck saved proposals independently and summarize conditions without discarding failures."""
import argparse
import collections
import json
import statistics
from pathlib import Path

import run as bench
from fixtures import build_cases


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',required=True)
    args=parser.parse_args();out=Path(args.out).resolve()
    if bench.ROOT not in out.parents:raise ValueError('Output must stay inside workspace')
    cases={c['id']:c for c in build_cases(bench.ROOT)}
    for path in [out/'GoddessSourceHarness/CurrentSourceCases.json',
        out/'StructuredSourceHarness/RunnerCompatibleCases.json',out/'MethodCacheFixtures.json']:
        if path.exists():
            value=json.loads(path.read_text(encoding='utf-8-sig'))
            for case in value['cases'] if isinstance(value,dict) else value:cases[case['id']]=case
    measurements=[json.loads(line) for line in (out/'measurements.jsonl').read_text(encoding='utf-8').splitlines()]
    events=[json.loads(line) for line in (out/'events.jsonl').read_text(encoding='utf-8').splitlines()]
    baselines={e['label']:e['baseline'] for e in events if e['kind']=='server-start'}
    audited=[];groups=collections.defaultdict(list)
    # note: Golden meaning, JSON shape and terminal transport are separate gates; early missing DONE evidence remains unknown.
    for row in measurements:
        case=cases.get(row['fixture'])
        if case is None:raise ValueError('No independently recorded fixture for '+row['fixture'])
        item=dict(modelId=row['modelId'],condition=row['serverLabel'],fixture=row['fixture'],sample=row.get('sample'),
            cpu=row.get('cpu'),seconds=row['seconds'],firstVisibleSeconds=row.get('firstVisibleSeconds'),
            finishReason=row.get('finishReason') or row.get('completionMetadata',{}).get('done_reason'),
            terminalDoneEvidence=row.get('streamDone'),error=row.get('error'),raw=row.get('raw',''))
        if row.get('backend')=='private_owned_ollama':
            item['terminalDoneEvidence']=row.get('completionMetadata',{}).get('done')
        item['terminalStopEvidence']=item['finishReason'] in ['stop','eos']
        item['jsonValid']=None;item['schemaValid']=None;item['semanticPass']=None
        if case.get('schema') or case.get('response_format'):
            try:
                parsed=bench.strict_json(item['raw']);item['jsonValid']=isinstance(parsed,dict)
                if case.get('schema'):
                    try:bench.jsonschema.validate(parsed,case['schema']);item['schemaValid']=True
                    except bench.jsonschema.ValidationError as error:item.update(schemaValid=False,validationError=str(error)[:1200])
                if 'expected' in case:
                    item['semanticPass']=parsed==case['expected']
                    item['semanticDifferences']={k:dict(expected=v,actual=parsed.get(k)) for k,v in case['expected'].items() if parsed.get(k)!=v}
            except Exception as error:item.update(jsonValid=False,schemaValid=False,semanticPass=False,validationError=str(error)[:1200])
        item['strictGoldenDeliveryPass']=(item['semanticPass'] is True and item['schemaValid'] is True and
            item['terminalDoneEvidence'] is True and item['terminalStopEvidence'])
        if row.get('peakGpuUsedMiB') is not None:
            item['peakGpuUsedMiB']=row['peakGpuUsedMiB']
            base=baselines.get(row['serverLabel'])
            item['peakGpuDeltaMiB']=row['peakGpuUsedMiB']-base['usedMiB'] if base else None
        item['timings']=row.get('timings');item['usage']=row.get('usage');item['track']=row.get('track')
        audited.append(item);groups[item['condition']].append(item)
    summary=[]
    for key,rows in groups.items():
        golden=[r for r in rows if 'expected' in cases[r['fixture']]]
        delta=[r['peakGpuDeltaMiB'] for r in rows if r.get('peakGpuDeltaMiB') is not None]
        summary.append(dict(condition=key,modelId=rows[0]['modelId'],requests=len(rows),
            terminalStopCount=sum(r['terminalStopEvidence'] for r in rows),
            doneRecordedCount=sum(r['terminalDoneEvidence'] is True for r in rows),
            goldenCount=len(golden),goldenContentPassCount=sum(r['semanticPass'] is True and r['schemaValid'] is True for r in golden),
            strictGoldenDeliveryPassCount=sum(r['strictGoldenDeliveryPass'] for r in golden),
            peakGpuDeltaMiB=max(delta) if delta else None,
            byFixture={f:dict(seconds=[r['seconds'] for r in rs],
                medianSeconds=statistics.median(r['seconds'] for r in rs),
                semanticPass=[r['semanticPass'] for r in rs],terminalDone=[r['terminalDoneEvidence'] for r in rs])
                for f,rs in ((f,[r for r in rows if r['fixture']==f]) for f in dict.fromkeys(r['fixture'] for r in rows))}))
    bench.save(out/'StrictResponseAudit.json',dict(rows=audited,
        evidence='Independent strict parsing/schema/golden re-audit. Original raw measurements remain unchanged. Early SSE DONE markers were not persisted and are not retrospectively invented.'))
    bench.save(out/'ConditionSummary.json',summary)
    print(json.dumps(dict(requests=len(audited),conditions=len(summary),strictGolden=sum(r['strictGoldenDeliveryPass'] for r in audited),
        modelFailures=[e for e in events if e['kind']=='model-failed'])))


if __name__=='__main__':main()
