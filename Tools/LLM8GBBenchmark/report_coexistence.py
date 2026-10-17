"""Summarize this completed first cohort without promoting format checks to speech acceptance."""
import hashlib
import html
import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
folder = (ROOT / sys.argv[1]).resolve()
if not folder.is_relative_to(ROOT / 'outputs'): raise ValueError('Report must remain in workspace outputs')
read = lambda name: json.loads((folder / name).read_text(encoding='utf-8-sig'))
episodes, stages = read('Episodes.json'), read('Staging.json')
complete = read('Complete.json')
reviews = {
    'LFM-2.5-1.2b-Instruct-roleplay-tuned-v2.Q8_0.gguf': dict(
        recommendation='Use-case-specific candidate for literal reconstruction only; not qualified for dialogue or general generation.',
        npc='FAIL: conversation confuses who rescued the miner and invents a relationship between the missing ledger and the ferry.',
        goddess='FAIL: guidance changes the player\'s wish to protect family into a request that the Goddess do it; deadline also fails source prose checks after repairs.',
        other='Equipment is a storage chest in a body slot; consumable contains a dried mug of water; biography invents Old Tom, daily work and river history. These are semantic failures despite valid JSON.'),
    'LFM2.5-2.6B-Q4_K_M.gguf': dict(
        recommendation='Good next-round candidate for exact context/reasoning; not qualified as the universal voice model.',
        npc='FAIL: conversation invents arrival timing and rescue-log contents for the missing ledger.',
        goddess='FAIL: claims to have watched the player, turns a world-plan premise into a live flood deadline, and claims the player\'s title as the Goddess\'s duty.',
        other='Title/class and new NPC concept are coherent screening outputs. Equipment puts a shield in a body slot; factual biography invents daily bridge patrols. Native reasoning contributes several seconds before visible text.'),
    'Qwen3.5-4B-Q4_K_M.gguf': dict(
        recommendation='Retain as control and candidate for constrained content proposals; tested GPU configuration has insufficient reserve.',
        npc='FAIL: conversation invents a rescue-specific ledger and ferry departure uncertainty, although checking the chapel healer is sensible.',
        goddess='FAIL: guidance places the player at Reed Ford and declares live rain/submerged crossings; deadline invents when the ferry closes. Source prose acceptance is not factual approval.',
        other='Equipment and title/class concepts are plausible. Consumable reduces friction to prevent slipping; skill promises complete blocking despite no-invulnerability instruction; factual biography invents guild recognition.'),
}
rows = []
for stage in stages:
    selected = [e for e in episodes if e['modelId'] == stage['model']]
    exact = [e for e in selected if e['attempts'][0]['measurement'].get('semanticPass') is not None]
    calls = [a['measurement'] for e in selected for a in e['attempts']]
    samples = [s['gpu'] for c in calls for s in c['memorySamples'] if 'gpu' in s]
    speech = [e for e in selected if e['track'] in ('roleplay', 'goddess_current_source')]
    row = dict(model=stage['model'], episodes=len(selected), requests=len(calls),
        exactContextPass=sum(e['acceptedByDetachedChecks'] for e in exact), exactContextTotal=len(exact),
        automaticChecksPass=sum(e['acceptedByDetachedChecks'] for e in selected),
        repairedAutomaticPass=sum(len(e['attempts']) > 1 and e['acceptedByDetachedChecks'] for e in selected),
        speechSecondsRange=[min(e['seconds'] for e in speech), max(e['seconds'] for e in speech)],
        medianRequestSeconds=statistics.median(c['seconds'] for c in calls),
        minFreeVramMiB=min(s['freeMiB'] for s in samples), peakTotalGpuUsedMiB=max(s['usedMiB'] for s in samples),
        unityFrameP95MedianMs=statistics.median(e['unityAfter']['frameP95Ms'] for e in selected),
        copySeconds=stage['copySeconds'], loadSeconds=stage['loadSeconds'],
        temporaryCopyDeleted=stage['temporaryCopyDeleted'], originalStillPresent=stage['originalStillPresent'],
        qualitativeReview=reviews[stage['model']])
    rows.append(row)
sources = [ROOT / 'Tools/LLM8GBBenchmark' / name for name in
           ['coexistence.py','staged_model.py','run.py','fixtures.py','category_cases.py','schema_prompt.py']]
report = dict(status='FIRST_COHORT_COMPLETE_NOT_FULL_INVENTORY_QUALIFICATION', episodes=len(episodes),
    requests=sum(r['requests'] for r in rows), models=rows, unity=complete['unity'],
    evidence='Stationary rendered isolated Unity world plus external inference. Three weights, one full suite each. No player traversal or in-game speech delivery certification.',
    limitations=['One deterministic sample per fixture, with up to two validation-directed repairs; not a reliability estimate.',
                 'Prose semantic review was performed after generation by the coding assistant and was not blind or human user acceptance.',
                 'Repair triggers covered exact-answer, schema and linked source checks. Post-hoc prose failures were not automatically retried in this cohort.',
                 'Synthetic extra role schemas are screening probes, not production mechanics/identity/persistence acceptance.',
                 'GPU measurements are whole-device totals, including other applications; Unity remained rendered and READY.',
                 'Copy and verification warm the OS file cache. Model load is not a cold-disk benchmark.',
                 'Two preflight/harness interruptions preceded the complete cohort; their outputs remain separate and excluded.',
                 'Remaining downloaded models and production NPC/Goddess delivery are still unqualified. No original weight deletion is recommended from this small cohort.'],
    sourceHashesAtReport={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources})
(folder / 'Report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
esc = html.escape
body = '<!doctype html><meta charset="utf-8"><title>YourQuest: first coexistence cohort</title><style>body{font:17px system-ui;max-width:1150px;margin:40px auto;padding:0 24px;color:#dae5f1;background:#121c29}table{width:100%;border-collapse:collapse}td,th{padding:14px 10px;border-bottom:1px solid #40516a;text-align:left}h1,h2{color:#95d1ff}small{color:#afbdce}a{color:#95d1ff}</style>'
body += '<h1>Generated world ready. First model cohort complete.</h1><p>72 fixture episodes / '+str(report['requests'])+' model requests. All temporary SSD copies deleted; original weights preserved. Each model completed every category and applicable automatic retry before unloading.</p>'
body += '<p><strong>No model earned an overall NPC + Goddess speech pass.</strong> Exact-answer accuracy, format acceptance and prose grounding are different results.</p><table><tr><th>Model</th><th>Exact checks</th><th>Speech episode seconds</th><th>Minimum free VRAM</th><th>Copy / load seconds</th></tr>'
for row in rows:
    low, high = row['speechSecondsRange']
    body += f'<tr><td>{esc(row["model"])}</td><td>{row["exactContextPass"]}/{row["exactContextTotal"]}</td><td>{low:.2f}–{high:.2f}</td><td>{row["minFreeVramMiB"]} MiB</td><td>{row["copySeconds"]:.2f} / {row["loadSeconds"]:.2f}</td></tr>'
body += '</table><p>Speech times include applicable automatic retries; they are not accepted-speech delivery latency. The requested 1,024 MiB fitting reserve was not reliably maintained by the larger configurations.</p>'
for row in rows:
    review = row['qualitativeReview']
    body += '<h2>'+esc(row['model'])+'</h2>' + ''.join('<p>'+esc(review[k])+'</p>' for k in ['recommendation','npc','goddess','other'])
body += '<h2>What this verifies</h2><p>The production world readiness gate released in Unity after the road materializer repair, with the same accepted seed and V2 plan. Current-source detached validators were rebuilt and checked. This is assisted stationary coexistence evidence.</p><ul>' + ''.join('<li>'+esc(s)+'</li>' for s in report['limitations']) + '</ul>'
body += '<p>Detailed evidence: <a href="Report.json">summary</a> · <a href="Episodes.json">all outputs and retries</a> · <a href="Staging.json">copy, server and cleanup receipts</a></p>'
(folder / 'Report.html').write_text(body, encoding='utf-8')
print(json.dumps(dict(report=str(folder / 'Report.html'), models=rows), indent=2))
