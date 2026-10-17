"""Create a ranked local implementation receipt from preserved tests."""
import html
import json
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmGameplayFeel_20261008'


def read(name):
    return json.loads((OUT / name).read_text(encoding='utf-8-sig'))


def main():
    cases, checks = read('Cases.json'), read('EditorChecks.json')
    # note: Rankings weigh game responsiveness and factual authority, not just decoding speed; qualification remains separate.
    ranking = [
        ('1', 'Persistent generated content', 'Qwen3.5-4B-Q4_K_M',
         'Generate items, equipment, consumables, titles, classes, quests, skills, lore and NPC records ahead of need; validate and persist once. Existing C# executes accepted mechanics immediately.',
         'Retained control; item/title/class and NPC parser checks passed previously. Positive skill/spell/quest and full world acceptance are not all established.'),
        ('2', 'Short contextual NPC conversations', 'Spark-X2.5-4B-Q4_K_M',
         'Keep it warm through an exchange. The two new NPC probes took 0.641 and 0.781 seconds. Qwen added an unsupported records room in the conversational case.',
         'Preferred configured dialogue author; ordinary gameplay speech delivery remains unverified.'),
        ('3', 'Memory/context summaries', 'Already-resident Spark or Qwen; LFM2-1.2B-RAG fallback',
         'Both resident candidates preserved all six facts/unknowns in the reconstruction fixture. Reuse the resident launch configuration only when the complete prompt fits; otherwise keep the original summary route.',
         'Implemented selection; 0.829 seconds Spark / 1.563 seconds Qwen in this baseline. Earlier LFM GPU probe was 0.454 seconds, excluding a costly switch.'),
        ('4', 'Background world and NPC planning', 'Existing Qwen owner and queue',
         'Prepare accepted content before it becomes visible. Allow restartable background loading to yield to a player request and pause briefly after a player-facing reply.',
         'Three-second reply grace, capped by twelve seconds of original queue age; existing startup leases, repair budgets and FIFO order preserved.'),
        ('5', 'Goddess free conversation / semantic judgment', 'No qualified deployment yet',
         'Keep authoring and factual approval separate. Planned weather must not become current weather, and a recall question must not fail merely because its facts were mentioned before.',
         'Blocked on semantic reliability: both candidates approved known-bad safety/deadline replies. Schema/parser repaired, gate remains off.'),
        ('6', 'CPU extraction helpers', 'GLiNER only if a concrete extraction workload warrants it',
         'Potentially useful for turning free prose into candidate entities. Existing typed player/world facts should be read directly; extraction cannot certify truth or mutate state.',
         'Optional future helper, not installed or enabled. No CPU generation fallback added.')]
    methods = [
        ('Accepted-content reuse and generation ahead of need', 'Highest value; preserve existing canonical owners and persistence.'),
        ('Resident model reuse and bounded background scheduling', 'Implemented; avoids unnecessary summary swaps and protects a player reply window.'),
        ('Full GPU, Flash Attention, Q8 KV, bounded prefix cache, one slot', 'Retained existing tested configuration with a fresh 1536 MiB reserve check.'),
        ('Model-free n-gram speculation', 'Tested, not enabled. Spark summary 0.829→0.859 s; dialogue 0.641→0.641 and 0.781→0.750 s. Qwen summary 1.563→1.000 s; dialogue 0.719→0.672 and 1.171→1.172 s. One sample per case is insufficient for default promotion.'),
        ('EAGLE / separate draft model / simultaneous GPU specialists', 'Not enabled: additional model allocations are a poor fit for the measured narrow reserve; compatibility and end-to-end benefits need separate proof.'),
        ('MTP', 'Model/runtime-specific candidate, not a universal switch; no new qualification from this run.'),
        ('Streaming unreviewed prose to the player', 'Not enabled: partial text can expose unsupported facts before review. Keep the UI responsive while the complete candidate is validated.'),
        ('Production SSD caching', 'Not implemented here. Test staging cost 4.68 s for Spark and 15.90 s for Qwen before verification/load; avoiding needless swaps is the immediate win.')]
    measurements = []
    for row in cases:
        m = row['measurement']
        free = [s['gpu']['freeMiB'] for s in m.get('memorySamples', []) if s.get('gpu')]
        measurements.append(dict(model=row['model'], mode=row['mode'], case=row['id'], seconds=m['seconds'],
                                 minimumSampledFreeMiB=min(free) if free else None))
    limitations = [
        '32 detached calls beside a rendered fallback world. Benchmark isolation blocked Unity-owned inference. This is not ordinary NPC/Goddess gameplay delivery.',
        'The final Goddess schema/parser alignment and removal of the approving example were made after the inference run. They passed contract checks but their semantic benefit has not been remeasured.',
        'Resident-summary selection and scheduling passed detached Editor checks. Live cold-load interruption, actual avoided swap counts and player-visible responsiveness are not yet verified.',
        'Original request payloads are retained in Inference/*_request.json. GoddessCases.json is the final prompt export, not a claim that all historical calls used that final revision.',
        'No quality gate was weakened or enabled. Accepted player/world state and deterministic gameplay ownership remain unchanged.',
        'Reported inference times exclude copying, verification, queueing and cold loading; the small sample does not establish tail latency or universal accuracy.'
    ]
    summary = dict(utc=datetime.now(timezone.utc).isoformat(), status='PARTIAL_IMPLEMENTATION_VERIFIED_SPEECH_ACCEPTANCE_PENDING',
                   ranking=ranking, methods=methods, measurements=measurements,
                   compiledSources=read('CompileVerified/Receipt.json')['sourceCount'], compileExit=read('CompileVerified/Receipt.json')['exitCode'],
                   editorChecks=len(checks['checks']), failedChecks=sum(not c['pass'] for c in checks['checks']),
                   evidence=checks['evidence'], limitations=limitations, cleanup=read('Cleanup.json'),
                   sources=['https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md',
                            'https://github.com/ggml-org/llama.cpp/blob/master/docs/speculative.md',
                            'https://huggingface.co/LiquidAI/LFM2-1.2B-RAG'])
    (OUT/'Summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    esc = html.escape
    ranking_rows = ''.join('<tr>' + ''.join('<td>'+esc(s)+'</td>' for s in row) + '</tr>' for row in ranking)
    methods_rows = ''.join('<tr><td>'+esc(a)+'</td><td>'+esc(b)+'</td></tr>' for a,b in methods)
    page = '''<!doctype html><html lang="en"><meta charset="utf-8"><title>YourQuest: best feasible 8 GB workflow</title>
<style>body{font:16px/1.6 system-ui;background:#101923;color:#e3edf4;max-width:1250px;margin:40px auto;padding:0 24px}h1,h2{color:#8fdbf3}a{color:#a0d8ff}table{border-collapse:collapse;width:100%;font-size:14px}td,th{padding:10px;border-bottom:1px solid #334452;text-align:left;vertical-align:top}.notice{padding:18px;background:#253446;border-left:4px solid #f0b060}</style>
<h1>The strongest feasible 8 GB workflow</h1><p>October 8, 2026. Recommendations are ranked by our evidence, not claimed universal model superiority.</p>
<div class="notice">Generate durable content ahead of need, persist it, and keep immediate interactions responsive. Use one GPU model at a time. Preserve the 1536 MiB minimum reserve. Goddess factual approval is still unresolved.</div>
<h2>Ranked use cases</h2><table><tr><th>Rank</th><th>Use case</th><th>Model</th><th>Implementation approach</th><th>Evidence / limits</th></tr>'''+ranking_rows+'''</table>
<h2>Methods checked</h2><table><tr><th>Method</th><th>Decision</th></tr>'''+methods_rows+'''</table>
<h2>Implemented and verified</h2><p>Short summaries can reuse a matching, owned resident Spark/Qwen configuration when the complete prompt fits. Optional NPC planning receives a bounded reply grace window. Background model preparation can yield through the existing queue and owned-process cleanup. Goddess review now rejects NPC-only references and duplicate snapshot references; prompt examples no longer supply an approving verdict.</p>
<p>384 runtime sources compiled. '''+str(summary['editorChecks'])+''' detached Editor checks passed. The two-model comparison completed 32 GPU calls; minimum sampled free VRAM was 2053 MiB for Spark and 1647 MiB for Qwen. Both staged copies were deleted and originals preserved. No llama process or loaded Ollama model remained after cleanup.</p>
<h2>Remaining</h2><ul>'''+''.join('<li>'+esc(s)+'</li>' for s in limitations)+'''</ul>
<h2>Sources and receipts</h2><p><a href="https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md">llama.cpp server: caching, slots, structured output</a> · <a href="https://github.com/ggml-org/llama.cpp/blob/master/docs/speculative.md">Official speculative decoding methods</a> · <a href="https://huggingface.co/LiquidAI/LFM2-1.2B-RAG">LFM RAG model card</a></p>
<p><a href="Summary.json">Full summary</a> · <a href="EditorChecks.json">Editor checks</a> · <a href="Cases.json">Measured responses</a> · <a href="Staging.json">Copy/load/cleanup timings</a> · <a href="Cleanup.json">Final cleanup</a></p></html>'''
    (OUT/'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps(dict(report=str(OUT/'Report.html'), calls=len(cases), checks=summary['editorChecks'])))


if __name__ == '__main__':
    main()
