"""Evidence-separated inventory and per-role receipts; never move or delete original weights."""
import argparse
import html
import json
import re
import statistics
from pathlib import Path
from coexistence import BASE, read

TRACKS = {
    'reconstruction':'Accepted state JSON reconstruction', 'reasoning':'Constraint reasoning',
    'summary':'Context and memory summaries', 'roleplay':'NPC dialogue',
    'goddess_current_source':'Goddess dialogue', 'origin_current_schema':'Origin generation',
    'world_current_schema':'World generation', 'frontier_current_schema':'Frontier settlement generation',
    'quest_current_schema':'Quest current schema', 'equipment':'Items and equipment',
    'consumable':'Consumables', 'skill_spell':'Skills and spells', 'title_class':'Titles and classes',
    'world_player_lore':'World and player lore', 'npc_identity':'NPC generation',
    'quest_proposal':'Quest proposals', 'native_tool':'Tool selection'
}
TRACKS.update(origin_current_schema_synthetic='Origin generation',world_current_schema_synthetic='World generation',
              frontier_settlement_compatible_replay='Frontier settlement generation',quest_empty_evidence_compatible_replay='Quest current schema')


def role_key(episode):
    return episode['fixture'] if episode['track'].startswith('structured_') else episode['track']


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--sweep',required=True);parser.add_argument('--native');parser.add_argument('--controls');parser.add_argument('--cache-reuse');parser.add_argument('--out',required=True)
    args=parser.parse_args();sweep=Path(args.sweep);out=Path(args.out);out.mkdir(parents=True,exist_ok=True)
    prior=BASE/'Coexistence_20261008_165749'
    inventory=read(BASE/'Inventory.json');stages=read(prior/'Staging.json')+read(sweep/'Staging.json')
    episodes=read(prior/'Episodes.json')+read(sweep/'Episodes.json')
    if (prior/'Complete.json').exists():
        for stage in stages[:len(read(prior/'Staging.json'))]:
            if sum(e['modelId']==stage['model'] for e in episodes)==24:
                stage.setdefault('status','SUITE_COMPLETE')
    reviews=read(sweep/'SemanticReview.json')['models'] if (sweep/'SemanticReview.json').exists() else {}
    # note: The prior cohort has explicitly reviewed voice failures, not automatically certified passes.
    for stage in read(prior/'Staging.json'):
        reviews.setdefault(stage['model'],dict(npc='FAIL',goddess='FAIL',reasons=['See first-cohort Report.json for detached semantic review.']))
    native=Path(args.native) if args.native else None
    native_reviews=read(native/'SemanticReview.json') if native and (native/'SemanticReview.json').exists() else {}
    if native and (native/'Staging.json').exists():stages+=read(native/'Staging.json')
    stage_by={s['model']:s for s in stages};hash_primary={s['sha256']:s['model'] for s in stages if 'sha256' in s}
    rows=[];category_rows={}
    for model in inventory:
        identifier=model['id'];primary=hash_primary.get(model['sha256'],identifier);stage=stage_by.get(primary,{})
        es=[e for e in episodes if e['modelId']==primary];review=reviews.get(primary,{})
        calls=[a['measurement'] for e in es for a in e['attempts']]+[a['critique'] for e in es for a in e['attempts'] if a.get('critique')]
        samples=[s for c in calls for s in c.get('memorySamples',[]) if s.get('gpu')]
        row=dict(model=identifier,source=model['localPath'],package=model['package'],url='https://huggingface.co/'+model['package'],
                 sha256=model['sha256'],sameWeightsAs=primary if primary!=identifier else None,status=stage.get('status','NOT_TESTED'),
                 episodes=len(es),requests=len(calls),automaticPasses=sum(bool(e['acceptedByDetachedChecks']) for e in es),
                 repairedEpisodes=sum(len(e['attempts'])>1 and bool(e['acceptedByDetachedChecks']) for e in es),
                 medianEpisodeSeconds=statistics.median(e['seconds'] for e in es) if es else None,
                 minimumFreeMiB=min(s['gpu']['freeMiB'] for s in samples) if samples else None,
                 loadSeconds=stage.get('loadSeconds'),copySeconds=stage.get('copySeconds'),temporaryCopyDeleted=stage.get('temporaryCopyDeleted'),
                 originalStillPresent=stage.get('originalStillPresent'),runtimeError=stage.get('error'),semanticReview=review,categories={})
        for track in dict.fromkeys(role_key(e) for e in es):
            role=TRACKS.get(track,track);selected=[e for e in es if role_key(e)==track]
            exact=[e for e in selected if e['track'] in ('reconstruction','reasoning','summary') and e['attempts'][-1]['measurement'].get('semanticPass') is not None]
            manual=review.get('npc' if track=='roleplay' else 'goddess' if track=='goddess_current_source' else 'categories',{})
            if isinstance(manual,dict):manual=manual.get(track)
            automatic=all(e['acceptedByDetachedChecks'] for e in selected)
            hard_failure=any(not e['acceptedByDetachedChecks'] and not e['attempts'][-1].get('critique') for e in selected)
            measured=dict(model=identifier,role=role,track=track,cases=len(selected),automaticPasses=sum(e['acceptedByDetachedChecks'] for e in selected),
                          exactCases=len(exact),exactPasses=sum(e['attempts'][-1]['measurement']['semanticPass'] for e in exact),
                          independentSemanticReview=manual or 'NOT_REVIEWED',secondsMedian=statistics.median(e['seconds'] for e in selected),
                          secondsWorst=max(e['seconds'] for e in selected),minimumFreeMiB=row['minimumFreeMiB'],
                          fixtures=[e['fixture'] for e in selected],source=model['localPath'],url=row['url'],sha256=model['sha256'])
            measured['reviewQuality']=review.get('quality',{}).get(track,3 if manual in ('PASS','PASS_FIXTURE') else None)
            if manual in ('PASS','PASS_FIXTURE') and not automatic:
                category='use case specific';reason='Independent review found the final response acceptable, but the tested acceptance pipeline rejected it. Inspect false-rejection diagnostics.'
            elif manual=='FAIL' or hard_failure:
                category='delete';reason='Failed this role in the tested configuration. Role exclusion only; not permission to delete a weight useful elsewhere.'
            elif not automatic:
                category='use case specific';reason='Only the fallible model-review gate rejected this role; independent semantic review is needed before accepting or excluding it.'
            elif manual=='PARTIAL':
                category='use case specific';reason='Independent review found partial suitability but unresolved content or task-following defects; not fully qualified.'
            elif (len(exact)==len(selected) and exact) or manual in ('PASS','PASS_FIXTURE'):
                if manual in ('PASS','PASS_FIXTURE') and measured['reviewQuality']<3:
                    category='use case specific';reason='Met the narrow fixture but prose, naming or design quality was weak; not a preferred creative generator.'
                elif (row['minimumFreeMiB'] or 0)>=1536:
                    category='Good';reason='Passed the available exact or independently reviewed fixtures with at least 1.5 GiB measured free VRAM. Limited screening evidence.'
                else:
                    category='use case specific';reason='Passed available fixtures but does not demonstrate the requested 1.5 GiB free VRAM reserve.'
            else:
                category='use case specific';reason='Automatic contract checks passed; independent semantic quality remains unverified. Do not enable as a qualified voice.'
            measured.update(category=category,reason=reason);row['categories'][role]=measured
            category_rows.setdefault(role,[]).append(measured)
        if native and stage.get('kind'):
            cases_path=native/stage['kind']/'Cases.json'
            native_cases=read(cases_path) if cases_path.exists() else []
            row.update(episodes=len(native_cases),requests=len(native_cases),automaticPasses=sum(c.get('exactPass') is True for c in native_cases),
                       medianEpisodeSeconds=statistics.median(c['seconds'] for c in native_cases) if native_cases else None,
                       minimumFreeMiB=min(c['gpu']['freeMiB'] for c in native_cases) if native_cases else None)
            groups={}
            for c in native_cases:
                prefix=c['case'].split('_')[0];groups.setdefault((prefix,c.get('device','cpu')),[]).append(c)
            for (prefix,device),group in groups.items():
                role={'route':'Prompt routing','decision':'Candidate selection','entities':'Entity extraction','structured':'Structured fact extraction',
                      'relations':'Relationship extraction','rag':'Source grounded answers'}.get(prefix,prefix)
                if prefix=='decision':role+=' ('+device.upper()+')'
                native_review=native_reviews.get(stage['kind'],{}).get(prefix,{})
                overrides=native_review.get('caseOverrides',{})
                all_pass=all(overrides.get(c['case'],c.get('exactPass')) is True for c in group)
                failed=any(c.get('exactPass') is False or c['status']!='TESTED' for c in group)
                measured=dict(model=identifier,role=role,track=prefix,cases=len(group),automaticPasses=sum(c.get('exactPass') is True for c in group),
                              independentSemanticReview=('PASS_FIXTURE' if overrides else 'EXACT_FIXTURES') if all_pass else native_review.get('verdict','REVIEW_REQUIRED'),
                              semanticNotes=native_review.get('notes',[]),reviewedPasses=sum(overrides.get(c['case'],c.get('exactPass')) is True for c in group),
                              secondsMedian=statistics.median(c['seconds'] for c in group),secondsWorst=max(c['seconds'] for c in group),
                              minimumFreeMiB=min(c['gpu']['freeMiB'] for c in group),fixtures=[c['case'] for c in group],source=model['localPath'],url=row['url'],sha256=model['sha256'],
                              category='Good' if all_pass else 'use case specific',
                              reason='Passed all exact specialist fixtures on CPU or shared GPU.' if all_pass else 'Partial or semantically unreviewed specialist results; inspect native case receipts before use.')
                # note: A partial classifier may still help as a non-authoritative hint; never label it an exact state authority.
                if failed and not all_pass:measured['reason']='Specialist missed at least one exact fixture or encountered a case runtime error; limited advisory use only. '+ ' '.join(native_review.get('notes',[]))
                if all_pass and measured['minimumFreeMiB']<1536:
                    measured.update(category='use case specific',reason='Passed exact specialist fixtures but measured free VRAM fell below 1.5 GiB.')
                row['categories'][role]=measured;category_rows.setdefault(role,[]).append(measured)
        rows.append(row)
    # note: Best means fastest among fully passing exact/reviewed screening entries, not absolute production superiority.
    for role,entries in category_rows.items():
        qualifying=[e for e in entries if e['category']=='Good' and (e.get('exactCases')==e['cases'] or e.get('independentSemanticReview')=='EXACT_FIXTURES' or (e.get('reviewQuality') or 0)>=4)]
        if qualifying:
            best=min(qualifying,key=lambda e:(-(e.get('reviewQuality') or 0),e['secondsMedian']));best['category']='Best'
            best['reason']='Preferred observed result among passing exact or reviewed fixtures with measured reserve; quality precedes latency. Wider production acceptance remains required.'
        for category in ('Best','Good','use case specific','delete'):(out/'By use case'/role/category).mkdir(parents=True,exist_ok=True)
        for entry in entries:
            name=re.sub(r'[^A-Za-z0-9._-]+','_',entry['model'])+'.json'
            (out/'By use case'/role/entry['category']/name).write_text(json.dumps(entry,indent=2),encoding='utf-8')
    coverage=dict(files=len(inventory),uniqueWeights=len({m['sha256'] for m in inventory}),ggufEpisodes=len(episodes),
                  uniqueSuitesComplete=len({r['sha256'] for r in rows if r['status']=='SUITE_COMPLETE'}),
                  nativeCases=sum(r['episodes'] for r in rows if stage_by.get(r['sameWeightsAs'] or r['model'],{}).get('kind')),
                  ggufRequests=sum(len(e['attempts'])+sum(bool(a.get('critique')) for a in e['attempts']) for e in episodes),
                  statuses={s:sum(r['status']==s for r in rows) for s in sorted({r['status'] for r in rows})},
                  ggufComplete=(sweep/'Complete.json').exists(),nativeComplete=bool(native and (native/'Complete.json').exists()),
                  methodology=['Unity generated-world fixture rendered throughout; inference dispatched directly by detached harness.',
                  'One hash-verified SSD copy and one owned model process at a time; originals preserved.',
                  'Up to two repair calls after failure. Full sweep additionally uses fallible same-model critique on selected prose.',
                  'First-cohort reserve and repair methodology differ. Do not treat timing differences as a controlled comparison.',
                  'Synthetic category probes, source-bound validators and independent prose review are separately labeled.',
                  'Single-sample screening per scenario is not statistical reliability, ordinary in-game dialogue delivery, or final production acceptance.',
                  'Native specialists use CPU except OpenJev, which has separate GPU and CPU trials. Native memory samples are post-case snapshots, not continuously sampled peaks.',
                  'Native OpenJev rssBytes measures the Python decision worker, not its separate llama-server process; it must not be presented as total model RAM.',
                  'delete folders contain role-exclusion receipts only, never original weights. No model-file deletion has been performed.'])
    controls=read(Path(args.controls)/'Summary.json') if args.controls else None
    reuse=read(Path(args.cache_reuse)/'Summary.json') if args.cache_reuse else None
    # note: Keep changed prompt/runtime controls separate from baseline categories and repair-inclusive timings.
    result=dict(coverage=coverage,models=rows,categories=category_rows,runtimeControls=controls,cacheReuse=reuse)
    (out/'Results.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    escape=html.escape
    body='<h1>YourQuest local LLM screening</h1><p>'+escape(str(coverage['files']))+' files / '+str(coverage['uniqueWeights'])+' unique weights. Results distinguish automatic checks from factual review.</p>'
    body+='<p><strong>Decision: no tested configuration has qualified both NPC and Goddess speech.</strong> Spark with explicit speaker instructions passed the two narrow NPC probes, with generic prose. Granite remains close on Goddess grounding, but invents prior observations. These are next-stage candidates, not production approval.</p>'
    body+='<ul>'+''.join('<li>'+escape(t)+'</li>' for t in coverage['methodology'])+'</ul>'
    if controls:
        body+='<h2>Runtime and prompt follow-up</h2><p>'+escape(controls['evidence'])+'</p><p><a href="RuntimeControls.html">Open all controlled profiles and saved voice replies</a></p>'
        (out/'RuntimeControls.html').write_text((Path(args.controls)/'Report.html').read_text(encoding='utf-8'),encoding='utf-8')
    if reuse:
        body+='<h2>Verified bounded cache reuse</h2><p>'+escape(reuse['evidence'])+'</p>'
        body+='<p>Median first request: '+str(reuse['coldMedianSeconds'])+' s. Median exact repeat: '+str(reuse['warmMedianSeconds'])+' s. Minimum free VRAM: '+str(reuse['minimumFreeMiB'])+' MiB. Replies remained identical, including factual errors.</p>'
        body+='<p>Test settings: 4,096 context tokens, 24 GPU layers, Q8 KV cache, 256 MiB cache allowance, two checkpoints, 256-token minimum checkpoint spacing. This modifies the detached test runtime only.</p>'
        body+='<table><tr><th>Scenario</th><th>First / repeat</th><th>Reused tokens</th><th>Repeat first visible token</th></tr>'
        for pair in reuse['pairs']:
            body+='<tr><td>'+escape(pair['fixture'])+'</td><td>'+f"{pair['firstSeconds']:.2f}s / {pair['repeatSeconds']:.2f}s"+'</td><td>'+str(pair['reusedTokens'])+'</td><td>'+f"{pair['repeatFirstVisibleSeconds']:.3f}s"+'</td></tr>'
        body+='</table><p>The earlier cache-on/off comparison reused zero tokens in both conditions; it is not a warm-cache speed comparison.</p>'
    body+='<h2>Practical next steps</h2><ul><li>Keep task routing, accepted IDs, counters, costs and persisted state in their existing code authorities. A model classifier is optional for ambiguous player text, not needed when the caller already knows the generation category.</li><li>GLiNER2 is the strongest tested CPU routing/extraction lead: 12/12 route probes and the two inventory and two relation probes passed review. GLiClass and the LFM router each missed one of 12 routes. These are small synthetic samples.</li><li>OpenJev passed 40 candidate choices (10 scenarios, two candidate orders, GPU and CPU). GPU decisions were about 0.2 seconds but left insufficient reserve; CPU was about 3.2 seconds. Do not add it as an always-resident orchestrator solely for tasks ordinary code can resolve.</li><li>Preserve a stable factual prompt prefix and put changing questions at the end, then verify real cache hits with changed questions. Exact-repeat reuse is demonstrated; that production prompt arrangement is a proposed repair, not implemented here.</li><li>Keep one selected generator resident during a conversation or a batch, release it on actual model switch/idle budget/Play exit, and verify process exit before the next load. Repeated cold start/close per utterance discards useful reuse. Existing ownership and persistence boundaries must remain intact.</li><li>Repair planned-versus-observed facts and speaker-role prompting before enabling Goddess speech. Test normal on-screen NPC/Goddess delivery after detached qualification; this run does not establish that delivery.</li></ul>'
    body+='<h2>Deletion recommendations</h2><p>No original weights were deleted. These are recommendations for the tested workflow, not claims that a model is useless for every other task.</p><ul><li>Bonsai-8B.gguf is byte-identical to Bonsai-8B-Q1_0.gguf; retain one copy if keeping this model.</li><li>nsfw-6b-q2_k.gguf: remove from this game candidate pool. It produced incoherent content and unsolicited insults, and left little GPU reserve.</li><li>LFM2-350M-Extract Q4 and Q8: low priority to retain for this pipeline; both failed all six current state/reasoning/summary probes. This does not test every possible simple extraction application.</li><li>Other files should be judged by their role and runtime profile below; a voice failure alone is not grounds to delete a useful background generator.</li></ul>'
    body+='<input id="filter" placeholder="Filter model or role" oninput="filterRows()"><table><thead><tr>'+''.join('<th>'+s+'</th>' for s in ['Model','Status','Checks','Requests','Median episode','Min free VRAM','NPC / Goddess review'])+'</tr></thead><tbody>'
    for row in rows:
        sec=f"{row['medianEpisodeSeconds']:.2f}s" if row['medianEpisodeSeconds'] is not None else '—'
        voice_review=' / '.join(row['semanticReview'].get(key,'unreviewed') if role in row['categories'] else 'not applicable' for key,role in (('npc','NPC dialogue'),('goddess','Goddess dialogue')))
        cells=[f'<a href="{escape(row["url"])}">{escape(row["model"])}</a>',escape(row['status']),f"{row['automaticPasses']}/{row['episodes']}",str(row['requests']),sec,str(row['minimumFreeMiB'])+' MiB',escape(voice_review)]
        body+='<tr>'+''.join('<td>'+c+'</td>' for c in cells)+'</tr>'
    body+='</tbody></table>'
    body+='<h2>Independent review notes</h2>'
    for row in rows:
        notes=row['semanticReview'].get('reasons',[])
        if notes:
            body+='<details><summary>'+escape(row['model'])+'</summary><ul>'+''.join('<li>'+escape(n)+'</li>' for n in notes)+'</ul></details>'
    for role,entries in category_rows.items():
        body+='<h2>'+escape(role)+'</h2><table><tr><th>Model</th><th>Category</th><th>Evidence</th><th>Median / worst</th></tr>'
        for entry in sorted(entries,key=lambda e:(e['category'],e['secondsMedian'])):
            detail=entry['reason']+' Review: '+str(entry['independentSemanticReview'])+'. Automatic checks: '+str(entry['automaticPasses'])+'/'+str(entry['cases'])+'.'
            if entry.get('semanticNotes'):detail+=' '+' '.join(entry['semanticNotes'])
            body+='<tr><td>'+escape(entry['model'])+'</td><td>'+escape(entry['category'])+'</td><td>'+escape(detail)+'</td><td>'+f"{entry['secondsMedian']:.2f}s / {entry['secondsWorst']:.2f}s"+'</td></tr>'
        body+='</table>'
    page='<!doctype html><meta charset="utf-8"><title>YourQuest LLM screening</title><style>body{font:15px system-ui;background:#101a25;color:#e4edf7;margin:36px}table{border-collapse:collapse;width:100%;margin:20px 0}td,th{padding:10px;border-bottom:1px solid #33445a;text-align:left}th{color:#8fcfff}a{color:#9fd1ff}input{padding:12px;width:70%;font:inherit}li{margin:6px 0}</style>'+body+'<script>function filterRows(){let q=document.getElementById("filter").value.toLowerCase();document.querySelectorAll("tr").forEach(r=>{if(r.querySelector("td"))r.hidden=!r.innerText.toLowerCase().includes(q)})}</script>'
    (out/'Report.html').write_text(page,encoding='utf-8');print(json.dumps(coverage,indent=2))


if __name__=='__main__':main()
