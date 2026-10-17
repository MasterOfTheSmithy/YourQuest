"""Summarize bounded runtime controls without conflating automatic and semantic passes."""
import argparse
import html
import json
import statistics
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('folder')
    args = parser.parse_args()
    folder = Path(args.folder)
    cases = json.loads((folder / 'Cases.json').read_text(encoding='utf-8'))
    review_path = folder / 'SemanticReview.json'
    reviews = json.loads(review_path.read_text(encoding='utf-8')) if review_path.exists() else {}
    groups = {}
    for row in cases:
        groups.setdefault((row['model'], row['condition']), []).append(row)
    summary = []
    for (model, condition), rows in groups.items():
        calls = [r['measurement'] for r in rows]
        samples = [s['gpu'] for c in calls for s in c.get('memorySamples', []) if s.get('gpu')]
        exact = [c for c in calls if c.get('track') in ('reconstruction', 'reasoning', 'summary') and c.get('semanticPass') is not None]
        voice = [r for r in rows if r['fixture'].startswith(('dialogue_', 'goddess_'))]
        visible = [c['firstVisibleSeconds'] for c in calls if c.get('firstVisibleSeconds') is not None]
        semantic = reviews.get(model, {}).get(condition, {})
        summary.append(dict(model=model, condition=condition, cases=len(rows),
            exactPasses=sum(c['semanticPass'] for c in exact), exactCases=len(exact),
            automaticVoicePasses=sum(r['acceptedByDetachedChecks'] for r in voice), voiceCases=len(voice),
            secondsMedian=statistics.median(c['seconds'] for c in calls),
            voiceSecondsMedian=statistics.median(r['measurement']['seconds'] for r in voice) if voice else None,
            firstVisibleMedian=statistics.median(visible) if visible else None,
            minimumFreeMiB=min(s['freeMiB'] for s in samples) if samples else None,
            semanticReview=semantic))
    result = dict(complete=(folder / 'Complete.json').exists(), profiles=summary,
        evidence='Single-seed first-attempt detached controls with Unity rendering. No repair calls. Compact profiles change context, KV precision and GPU placement together. Differences alone do not prove a cache defect. No ordinary in-game speech delivery claim.')
    (folder / 'Summary.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    esc = html.escape
    body = '<h1>Runtime and prompt controls</h1><p>' + esc(result['evidence']) + '</p>'
    body += '<table><tr><th>Model / profile</th><th>Exact checks</th><th>Voice contract checks</th><th>Voice median</th><th>Free VRAM minimum</th><th>Independent voice review</th></tr>'
    for row in summary:
        voice_seconds = f"{row['voiceSecondsMedian']:.2f}s" if row['voiceSecondsMedian'] is not None else 'No voice cases yet'
        body += '<tr><td>' + esc(row['model'] + ' / ' + row['condition']) + '</td>'
        body += f"<td>{row['exactPasses']}/{row['exactCases']}</td><td>{row['automaticVoicePasses']}/{row['voiceCases']}</td><td>{voice_seconds}</td><td>{row['minimumFreeMiB']} MiB</td>"
        body += '<td>' + esc(json.dumps(row['semanticReview'], ensure_ascii=False)) + '</td></tr>'
    body += '</table><h2>Saved voice responses</h2>'
    for row in cases:
        if not row['fixture'].startswith(('dialogue_', 'goddess_')):
            continue
        body += '<details><summary>' + esc(' / '.join((row['model'], row['condition'], row['fixture']))) + '</summary><pre>' + esc(row['measurement'].get('raw', '')) + '</pre></details>'
    page = '<!doctype html><meta charset="utf-8"><title>LLM runtime controls</title><style>body{font:15px system-ui;margin:32px;background:#101a25;color:#edf5fc}td,th{padding:12px;text-align:left;border-bottom:1px solid #456}table{border-collapse:collapse;width:100%}pre{white-space:pre-wrap}details{margin:12px 0}</style>' + body
    (folder / 'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
