"""Summarize the authorized practical speech mode and its exact runtime evidence."""
import hashlib
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/SpeechPractical_20261008'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    receipt_path = sorted(OUT.glob('Live_*/Receipt.json'))[-1]
    live = read(receipt_path)
    if live['status'] != 'COMPLETED_PRACTICAL_SPEECH':
        raise RuntimeError('The final live test has not completed')
    checks = read(OUT / 'Checks.json')
    compile_receipt = read(OUT / 'CompileHud/Receipt.json')
    paths = ['LLM/LLMRuntimeConfig.cs', 'LLM/LLMClient.cs', 'Dialogue/DialogueThinkService.cs',
             'Dialogue/YQDialogueGrounding.cs', 'Generated/YQGoddessGrounding.cs', 'Generated/YQGoddessSpeech.cs',
             'Generated/YQGoddessSpeechPlan.cs', 'Generated/YQGoddessGenerationDialogue.cs',
             'Tutorial/YQInvestorDirector.cs', 'Tutorial/YQStartupLoadingScreen.cs', 'Tutorial/YourQuestTutorialHud.cs']
    hashes = {r['Path'].replace('\\', '/').lower(): r['Hash'].lower() for r in compile_receipt['sourceHashes']}
    source_match = all(hashes.get((ROOT / 'Assets/Assets/Scripts' / p).as_posix().lower()) ==
                       hashlib.sha256((ROOT / 'Assets/Assets/Scripts' / p).read_bytes()).hexdigest() for p in paths)
    samples = read(OUT / 'GpuSamplesHud.json')
    summary = dict(mode='BEST_EFFORT_ENABLED_NOT_SEMANTIC_QUALIFICATION', compileExit=compile_receipt['exitCode'],
                   currentSourcesMatchCompile=source_match, checks=checks, live=live,
                   minimumSampledFreeMiB=min(int(r['gpuUsedFreeMiB'].split(',')[1]) for r in samples),
                   limits=[
                       'Heuristic speech checking can still miss an error. Strict canonical item, quest, world and player-state validators remain unchanged.',
                       'One resident full-GPU Qwen model authors and checks speech. Practical mode uses one local correction, not a repeated review loop.',
                       'A bad correction cannot replace a literal accepted class, skill, title or name. Invalid output uses marked uncertainty fallback.',
                       'Gameplay Goddess subtitles now observe delivered speech in the existing HUD, with startup/modal and profile ownership guards.',
                       'NPC tests used the ordinary transcript owner, but the assisted opening may fail its normal proximity/line-of-sight guard; see uiOpened per case.',
                       'HUD component visibility is observed in Play Mode; no human visual or player-build certification is claimed.',
                       'Only the disposable benchmark profile was used. Its normal dialogue/speech history was saved. Accepted gameplay records were not regenerated.',
                       'The earlier Live_20261009_021256 generic receipt said read-only; its explicit practical results include ordinary speech-history writes. That generic limit text was corrected in the final harness.'])
    if (OUT / 'Cleanup.json').exists():
        summary['cleanup'] = read(OUT / 'Cleanup.json')
    (OUT / 'Summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    page = '<!doctype html><meta charset="utf-8"><title>Practical local speech enabled</title><style>body{font:16px system-ui;background:#101823;color:#e1ecfa;max-width:1100px;margin:40px auto;padding:20px}h1,h2{color:#99ceff}li{margin:10px 0}table{width:100%;border-collapse:collapse}td,th{padding:10px;text-align:left;border-bottom:1px solid #38475a;vertical-align:top}.status{background:#234734;padding:18px;border-radius:10px}</style>'
    page += '<h1>Practical local speech is enabled</h1><p class="status">Best-effort dialogue with one bounded correction and explicit uncertainty fallback. This does not declare the model factually infallible.</p>'
    page += f'<p>Compile exit: {compile_receipt["exitCode"]}; current source hashes match: {source_match}. Focused checks: {len(checks["checks"])}. Minimum sampled free GPU memory: {summary["minimumSampledFreeMiB"]} MiB.</p>'
    page += '<h2>Final live results</h2><table><tr><th>Case</th><th>Delivery checks</th><th>Elapsed</th><th>Spoken text</th></tr>'
    for row in live['results']:
        if 'text' not in row:
            continue
        evidence = 'PASS' if row.get('pass') else 'FAIL'
        if row.get('isFallback'):
            evidence += ' — uncertainty fallback'
        page += '<tr><td>' + html.escape(row['label']) + '</td><td>' + evidence + f'</td><td>{row["elapsedSeconds"]:.2f}s</td><td>' + html.escape(row['text'] or '') + '</td></tr>'
    page += '</table><h2>Scope and limitations</h2><ul>' + ''.join('<li>' + html.escape(x) + '</li>' for x in summary['limits']) + '</ul>'
    (OUT / 'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps(dict(sourceHashesMatch=source_match, cases=[dict(label=r['label'], passed=r['pass']) for r in live['results'] if 'pass' in r], minimumSampledFreeMiB=summary['minimumSampledFreeMiB'])))


if __name__ == '__main__':
    main()
