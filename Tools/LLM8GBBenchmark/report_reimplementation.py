"""Summarize retained evidence without promoting transport checks into semantic acceptance."""
import hashlib
import html
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmReimplementation_20261008'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    compile_receipt = read(OUT / 'CompileNpcContext/Receipt.json')
    changed = [
        'LLM/LLMClient.cs', 'LLM/LLMRuntimeConfig.cs', 'LLM/YQLlmRequest.cs',
        'Dialogue/DialogueThinkService.cs', 'Generated/YQGoddessSpeech.cs',
        'Generated/YQWorldGenerationService.cs', 'Gameplay/Systems/World/LLMThinkCycle.cs']
    hashes = {row['Path'].replace('\\', '/').lower(): row['Hash'].lower() for row in compile_receipt['sourceHashes']}
    source_checks = []
    for file in changed:
        path = ROOT / 'Assets/Assets/Scripts' / file
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        source_checks.append(dict(path=str(path), sha256=digest, matchesCompile=hashes.get(path.as_posix().lower()) == digest))
    checks = {}
    for name in ['RouteChecks', 'TransportChecks', 'WorldReactionChecks']:
        receipt = read(OUT / (name + '.json'))
        checks[name] = dict(utc=receipt['utc'], total=len(receipt['checks']),
                            failed=[c for c in receipt['checks'] if not c['pass']], evidence=receipt['evidence'])
    live = [dict(path=str(p), receipt=read(p)) for p in sorted(OUT.glob('Live_*/Receipt.json'))]
    summary = dict(utc=datetime.now(timezone.utc).isoformat(), status='PARTIAL_SPEECH_SEMANTIC_ACCEPTANCE_BLOCKED',
                   compile=dict(exitCode=compile_receipt['exitCode'], sourceCount=compile_receipt['sourceCount'], changedSources=source_checks),
                   checks=checks, live=live,
                   changes=[
                       'All 17 role routes use the owned full-GPU llama.cpp transport on this installed-model setup.',
                       'Qualified GGUF speech can pin SHA-256 identity; hashing runs off the Unity thread and replaced files invalidate the cache.',
                       'World plans get at most two corrections while retaining original seed, scaffold, schema, profile/world and CAS revision.',
                       'World reactions can queue repair while their own dispatch callback is busy; original context and exact event occurrences survive the retry.',
                       'World reaction owner retirement cancels its admitted repair; later events cannot be cleared by an earlier response.',
                       'Reasoning-only responses cannot become final speech or gameplay proposals.',
                       'NPC retries retain their original context; model profiles own normal sampling; local-fact invention instructions were removed.',
                       'The gameplay acceptance harness no longer forces the summary model onto CPU.',
                       'The Editor was restarted without benchmark isolation and actual game-owned inference was exercised.'],
                   limitations=[
                       'Goddess author and semantic reviewer remain unqualified and disabled. No Goddess delivery PASS.',
                       'Latest NPC factual review FAILED: the reply introduced an old gate absent from the frozen context and attributed trade-route safety work to an apothecary without support. Successful delivery is not semantic acceptance.',
                       'NPC callback delivery and schema checks do not establish general factual reliability or ordinary UI/transcript acceptance.',
                       'Reasoning and claims-first reviewer experiments failed negative or positive semantic controls; neither was promoted.',
                       'Existing accepted test-world records were reused. This run did not perform a new ordinary character/world generation or persistent progression offer acceptance.',
                       'World-plan correction was compiled but its injected-failure path was not exercised in ordinary gameplay.',
                       'World-reaction admission/cancellation was verified in a detached real-queue fixture, not a new committed gameplay delta.',
                       'Other content categories retain previous domain validators and model evidence; route verification is not new content-quality certification.'])
    for name in ['SpeechStage', 'ProtocolStage', 'Cleanup', 'NpcSemanticReview']:
        if (OUT / (name + '.json')).exists():
            summary[name] = read(OUT / (name + '.json'))
    (OUT / 'Summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    def ul(values):
        return '<ul>' + ''.join('<li>' + html.escape(str(v)) + '</li>' for v in values) + '</ul>'
    runs = []
    for run in live:
        rows = []
        for result in run['receipt']['results']:
            label = html.escape(result['label'])
            outcome = 'DELIVERY/CONTRACT PASS' if result.get('pass') is True else 'FAIL' if result.get('pass') is False else 'OBSERVATION'
            seconds = result.get('elapsedSeconds')
            rows.append(f'<tr><td>{label}</td><td>{outcome}</td><td>{seconds:.2f}s</td><td>{html.escape(result.get("text") or result.get("error") or "")}</td></tr>' if isinstance(seconds, (int, float)) else
                        f'<tr><td>{label}</td><td>{outcome}</td><td>—</td><td></td></tr>')
        runs.append('<h3>' + html.escape(run['receipt']['status']) + '</h3><table><tr><th>Case</th><th>Evidence</th><th>Elapsed</th><th>Output</th></tr>' + ''.join(rows) + '</table>')
    page = '<!doctype html><html><meta charset="utf-8"><title>LLM implementation and remaining blocker</title><style>body{font:16px system-ui;background:#101823;color:#e1ecfa;max-width:1150px;margin:40px auto;padding:20px}h1,h2{color:#8dcaff}li{margin:9px 0}table{border-collapse:collapse;width:100%}td,th{padding:10px;text-align:left;border-bottom:1px solid #35465b;vertical-align:top}.status{padding:18px;background:#462f21;border-radius:10px}</style>'
    page += '<h1>LLM integration: repaired, still partial</h1><p class="status">Goddess speech remains blocked by failed semantic qualification. NPC delivery works, but the latest factual review failed. This is not a fully working-system certificate.</p>'
    page += '<h2>Implemented</h2>' + ul(summary['changes'])
    page += '<h2>Fresh verification</h2>' + ul([f"Runtime compilation: {compile_receipt['sourceCount']} sources, exit {compile_receipt['exitCode']}."] + [f"{name}: {c['total']} checks, {len(c['failed'])} failures." for name, c in checks.items()])
    page += ''.join(runs) + '<h2>Remaining</h2>' + ul(summary['limitations']) + '<p>Detailed timestamps, source hashes, request payloads and raw replies are retained alongside this report.</p></html>'
    (OUT / 'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps(dict(status=summary['status'], compileSources=compile_receipt['sourceCount'], sourceHashesMatch=all(x['matchesCompile'] for x in source_checks), liveRuns=len(live))))


if __name__ == '__main__':
    main()
