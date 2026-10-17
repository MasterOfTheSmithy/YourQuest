"""Retain partial implementation evidence without equating model acceptance with correctness."""
import hashlib
import html
import json
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/SpeechPatch_20261008'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    checks = read(OUT / 'Checks.json')
    replay = read(OUT / 'ProductionReplay.json')
    compiled = read(OUT / 'CompileFinal/Receipt.json')
    paths = ['Dialogue/YQDialogueGrounding.cs', 'Dialogue/DialogueThinkService.cs',
             'Generated/YQGoddessGrounding.cs', 'Generated/YQGoddessSpeech.cs', 'LLM/LLMRuntimeConfig.cs']
    hashes = {r['Path'].replace('\\', '/').lower(): r['Hash'].lower() for r in compiled['sourceHashes']}
    source_match = all(hashes.get((ROOT / 'Assets/Assets/Scripts' / p).as_posix().lower()) ==
                       hashlib.sha256((ROOT / 'Assets/Assets/Scripts' / p).read_bytes()).hexdigest() for p in paths)
    runs = []
    for name, folder in [('Direct v1', OUT), ('Direct v2', OUT / 'Run2'), ('Reasoning 512', OUT / 'Run3')]:
        results = read(folder / 'Results.json')
        calls = [call for row in results for call in row['calls']]
        samples = [s['gpu']['freeMiB'] for c in calls for s in c['memorySamples'] if 'gpu' in s]
        runs.append(dict(name=name, cases=len(results), calls=len(calls),
                         medianCallSeconds=median(c['seconds'] for c in calls),
                         minimumSampledFreeMiB=min(samples), stage=read(folder / 'Stage.json'), results=results))
    summary = dict(status='IMPLEMENTED_GATED_SEMANTIC_QUALIFICATION_FAILED', runtimeCompileExit=compiled['exitCode'],
                   changedSourcesMatchCompile=source_match, structuralChecks=len(checks['checks']),
                   structuralFailures=[r for r in checks['checks'] if not r['pass']],
                   productionReplayCases=len(replay['rows']), replayMismatches=[r for r in replay['rows'] if not r['matchesRunner']],
                   active=False, runs=runs, limits=[
                       'Shared exact-span correction is wired into both existing qualified NPC and Goddess review paths.',
                       'At most two patch applications and three review calls; patched text must pass unchanged on a subsequent review.',
                       'Text outside selected spans is preserved exactly. Canonical player/world state is never modified by these presentation patches.',
                       'Invalid patches fail atomically. Existing voice, length, repetition, owner, profile, epoch and model-identity gates remain.',
                       'The model still flags valid text, substitutes incorrect details, or misses an unsupported claim. None of the three variants qualified.',
                       'Production qualification flags were not enabled. Ordinary unqualified NPC generation remains as before; Goddess remains withheld.',
                       'This turn ran detached local model controls and actual C# patch replay, not ordinary gameplay speech or transcript persistence.'])
    (OUT / 'Summary.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
    page = '<!doctype html><meta charset="utf-8"><title>Targeted speech repair</title><style>body{font:16px system-ui;background:#101823;color:#dfebfa;max-width:1100px;margin:40px auto;padding:20px}h1,h2{color:#9bd0ff}td,th{padding:9px;border-bottom:1px solid #344457;text-align:left}li{margin:10px 0}table{border-collapse:collapse;width:100%}</style>'
    page += '<h1>Targeted NPC and Goddess repair</h1><p><b>Implemented behind existing qualification gates. Model qualification failed; not activated.</b></p><ul>'
    page += ''.join('<li>' + html.escape(x) + '</li>' for x in summary['limits']) + '</ul>'
    page += f'<p>Runtime compile: {compiled["exitCode"]}; current changed-source hashes match: {source_match}. Structural checks: {len(checks["checks"])}. Production C# replay cases: {len(replay["rows"])}.</p>'
    for run in runs:
        page += f'<h2>{html.escape(run["name"])}</h2><p>Median check: {run["medianCallSeconds"]:.2f}s. Minimum sampled free GPU memory: {run["minimumSampledFreeMiB"]} MiB. Editor idle; not loaded-world headroom proof.</p>'
        page += '<table><tr><th>Case</th><th>Model accepted (not correctness)</th><th>Seconds</th><th>Last candidate, possibly rejected</th></tr>'
        for row in run['results']:
            page += '<tr><td>' + html.escape(row['id']) + '</td><td>' + str(row['accepted']) + f'</td><td>{row["seconds"]:.2f}</td><td>' + html.escape(row['final']) + '</td></tr>'
        page += '</table>'
    (OUT / 'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps({k: summary[k] for k in ['status', 'runtimeCompileExit', 'changedSourcesMatchCompile', 'structuralChecks', 'structuralFailures', 'productionReplayCases', 'replayMismatches']}))


if __name__ == '__main__':
    main()
