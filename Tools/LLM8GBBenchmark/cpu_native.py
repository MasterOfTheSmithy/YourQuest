"""Native installed Ollama CPU controls against the same detached cases."""
import argparse
import json
import re
from pathlib import Path

import psutil
import run as bench
from fixtures import build_cases
from methods import OwnedOllama


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',required=True)
    parser.add_argument('--models',default='goddess-current,summary-current')
    parser.add_argument('--extra-cases',required=True)
    parser.add_argument('--fixtures', default='')
    parser.add_argument('--phase', default='native_ollama')
    args=parser.parse_args();bench.OUT=Path(args.out).resolve()
    extra=Path(args.extra_cases).resolve()
    if bench.ROOT not in bench.OUT.parents or bench.ROOT not in extra.parents:
        raise ValueError('All diagnostic files must remain within workspace')
    manifest=json.loads(bench.MANIFEST.read_text(encoding='utf-8-sig'))
    models={m['id']:m for m in manifest['models']+manifest['controls']}
    extra_cases=json.loads(extra.read_text(encoding='utf-8-sig'))['cases']
    for case in extra_cases:case.update(max_tokens=650,temperature=.7)
    regular={c['id']:c for c in build_cases(bench.ROOT)}
    if not re.fullmatch('[a-z0-9_-]+', args.phase):
        raise ValueError('Phase must be a safe filename component')
    rows=[]
    for model_id in args.models.split(','):
        cases=extra_cases if model_id=='goddess-current' else [regular[k] for k in
            ['summary_preserve_uncertainty','reasoning_counter_and_unknowns','reconstruct_cross_profile']]
        if args.fixtures:
            # note: A selected subset bounds expensive CPU trials while retaining exact cases and controls.
            wanted=set(args.fixtures.split(','));cases=[c for c in cases if c['id'] in wanted]
            if len(cases)!=len(wanted):raise ValueError('Unknown CPU fixture requested')
        with OwnedOllama(models[model_id],label=args.phase+'_'+model_id) as server:
            server.preload(args.phase+'_'+model_id)
            for case in cases:
                label=args.phase+'_'+model_id+'_'+case['id']
                try:
                    row=server.infer(case,label)
                    process=psutil.Process(server.process.pid)
                    processes=[process]+process.children(recursive=True)
                    row.update(serverLabel=label,track=case['track'],evidence=case['evidence'],contextTokens=8192,
                        thinking='off',sample=0,engine='native_installed_ollama_'+server.version['version'],
                        ownedCombinedRssBytes=sum(p.memory_info().rss for p in processes),
                        manualReviewRequired=case['track']=='goddess_current_source',error=None)
                    rows.append(row)
                    with (bench.OUT/'measurements.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(row,ensure_ascii=False)+'\n')
                    bench.event('native-cpu-case',model=model_id,fixture=case['id'],seconds=row['seconds'],
                        success=row['success'],semanticPass=row['semanticPass'])
                except Exception as error:
                    bench.event('native-cpu-case-failed',model=model_id,fixture=case['id'],error=str(error))
            server.release()
    bench.save(bench.OUT/('NativeOllamaCpuResults.json' if args.phase=='native_ollama' else args.phase+'_CpuResults.json'),dict(rows=rows,
        evidence='Private owned Ollama service, installed native tags, num_gpu=0 proven by /api/ps, Unity closed.'))


if __name__=='__main__':main()
