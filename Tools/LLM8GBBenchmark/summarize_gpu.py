"""Summarize retained GPU receipts without rerunning inference or changing qualification."""
import html
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / 'outputs/LlmGpuOnly_20261008'


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))


def main():
    # note: Keep raw responses and evidence labels separate from speed; parser acceptance is not semantic qualification.
    calls = []
    for folder in ('', 'FourBProbe/', 'TitleProbeRetry/', 'Control1536/', 'GoddessSpark/'):
        for row in read(folder + 'Cases.json'):
            m = row['measurement']
            free = [s['gpu']['freeMiB'] for s in m.get('memorySamples', []) if s.get('gpu') and s['gpu'].get('freeMiB') is not None]
            calls.append(dict(model=m['modelId'], fixture=row['id'], seconds=m.get('seconds'),
                              fullGpuRequested=m['gpuLayers'] == 999 and not m['cpu'] and not m['autoFit'],
                              minimumSampledFreeMiB=min(free) if free else None,
                              transportSuccess=m.get('transportSuccess'), receipt=folder + 'Cases.json'))
    before, after, exited = (read(p) for p in ('MemoryBefore.json', 'MemoryAfter.json', 'MemoryAfterExit.json'))
    checks = read('EditorChecks.json')
    report = dict(
        utc=datetime.now(timezone.utc).isoformat(), status='GPU_POLICY_IMPLEMENTED_GAMEPLAY_SPEECH_NOT_QUALIFIED',
        policy=dict(minimumFreeMiB=1536, placement='Full GPU; no automatic CPU or partial offload',
                    checks='Pre-load estimate, observed post-load reserve, fresh reserve before each warm request',
                    lowMemoryAction='Reject inference and unload the owned model; do not lower the reserve',
                    cpuExceptions='Explicit identity-bound CPU suitable helpers only; none enabled'),
        routes=dict(structured='Qwen3.5-4B-Q4_K_M.gguf', npcDialogue='Spark-X2.5-4B-Q4_K_M.gguf',
                    summarization='LFM2-1.2B-RAG-Q4_K_M.gguf', goddess='Still gated; semantic qualification incomplete'),
        textures=dict(beforeBytes=before['textureMemoryBytes'], afterBytes=after['textureMemoryBytes'],
                      playMipLimit=after['textureMipLimit'], restoredEditorMipLimit=exited['textureMipLimit'],
                      evidence='Same assisted stationary rendered world; source textures unchanged; visual quality not reviewed'),
        verification=dict(compile=read('CompileAcceptedRoutes/Receipt.json')['exitCode'],
                          compiledSources=read('CompileAcceptedRoutes/Receipt.json')['sourceCount'],
                          editorChecks=len(checks['checks']), failedChecks=sum(not c['pass'] for c in checks['checks']),
                          editorRuntimeMvid=checks['runtimeMvid'], productionEvaluations=read('ProductionEvaluations.json'),
                          npcParser=read('NpcEvaluation.json')),
        goddessReview=dict(automatedSourcePasses=5, total=5, productionQualified=False,
                           findings=[
                               'Safety and deadline replies treated planned climate/pressure as current conditions.',
                               'Guidance and safety factKeys did not identify the facts actually used.',
                               'Second-profile identity and unlocked class/skill recall were grounded in their supplied fixtures.',
                               'No repair was triggered by the automated validators; the five probes do not prove a working factual repair chain.']),
        decisions=[
            'Keep Qwen for titles/classes: approximately 2.1/2.3 seconds versus LFM approximately 5.9/5.8 seconds.',
            'Do not route consumable/lore to MiniCPM after its semantic failures; retain the control route.',
            'Preserve strict player-evidence validation: skill/spell probes with empty behavior counters must be rejected.'],
        limitations=[
            'Detached inference beside an assisted fallback world, with Unity-owned dispatch disabled; not ordinary gameplay speech acceptance.',
            'Durations exclude SSD copy, hash verification, cold model loading and queue/swap latency. Production still reads original E: paths.',
            'Qwen reached approximately 1576 MiB sampled free VRAM: only 40 MiB above the chosen floor. Future rendering growth can block inference.',
            'The reserve is checked before requests, not guaranteed throughout arbitrary concurrent Unity allocation growth.',
            'Positive skill/spell/quest acceptance, factual repair/reviewer qualification and visible NPC/Goddess delivery remain outstanding.',
            'No GLiNER helper service was installed or enabled.'],
        cleanup=read('Cleanup.json'), calls=calls)
    (ROOT / 'Summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    esc = html.escape
    rows = ''.join('<tr><td>' + esc(c['model']) + '</td><td>' + esc(c['fixture']) + '</td><td>' +
                   str(round(c['seconds'], 3)) + '</td><td>' + str(c['minimumSampledFreeMiB']) + '</td></tr>' for c in calls)
    # note: This is a local evidence report, not an enabled-model approval list.
    page = '''<!doctype html><html lang="en"><meta charset="utf-8"><title>YourQuest full-GPU integration</title>
<style>body{font:16px/1.6 system-ui;background:#101923;color:#e3edf4;max-width:1160px;margin:40px auto;padding:0 24px}h1,h2{color:#8fdbf3}a{color:#a0d8ff}table{border-collapse:collapse;width:100%;font-size:14px}td,th{padding:8px;border-bottom:1px solid #334452;text-align:left}.notice{padding:18px;background:#253446;border-left:4px solid #f0b060}</style>
<h1>YourQuest: full-GPU integration</h1><p>October 8, 2026 local session. The 1.5 GiB reserve policy is implemented; gameplay speech acceptance is unfinished.</p>
<div class="notice">Full GPU only. At least 1536 MiB free must be observed before inference, including reuse of a resident model. Low memory rejects the request and unloads the owned model.</div>
<h2>What changed</h2><p>Qwen handles structured generation, Spark handles the configured NPC dialogue route, and LFM RAG handles summaries. MiniCPM was removed from automatic consumable/lore routing after semantic failures. Goddess qualification remains disabled.</p>
<p>The runtime texture budget reduced measured texture memory from 3.27 GB to 0.88 GB in the same rendered fixture. Texture width and height are halved on this 8 GB setup; source assets are preserved and the Editor mip limit restored to zero after Play. Visual quality still needs review.</p>
<h2>Verification</h2><p>384 runtime sources compiled; ''' + str(report['verification']['editorChecks']) + ''' Editor checks passed. The production evaluator accepted item, title and class proposals, rejected unsupported skill/spell offers, and accepted the quest abstention. All six player snapshots remained unchanged. The NPC parser accepted 3/3 records; parser success alone does not establish content quality.</p>
<h2>Unresolved accuracy</h2><p>Five Goddess replies passed the automated source validators, but manual review found two replies treating planned climate as current conditions, plus incorrect fact identifiers. This exposes an incomplete factual verifier/repair path. The Goddess route is not qualified.</p>
<h2>Measured calls</h2><p>Inference only; copy, verification, cold loading and model swaps are excluded. These are detached calls beside an assisted rendered world, not the ordinary gameplay flow.</p>
<table><thead><tr><th>Model</th><th>Case</th><th>Seconds</th><th>Minimum sampled free MiB</th></tr></thead><tbody>''' + rows + '''</tbody></table>
<h2>Remaining and cleanup</h2><ul>''' + ''.join('<li>' + esc(v) + '</li>' for v in report['limitations']) + '''</ul>
<p>Play Mode ended. No llama-server remained, Ollama had zero loaded models, and all temporary SSD weight copies were deleted. Original weights remain intact.</p>
<p><a href="Summary.json">Complete evidence summary</a> · <a href="EditorChecks.json">Editor checks</a> · <a href="ProductionEvaluations.json">Production evaluator results</a> · <a href="Cleanup.json">Cleanup receipt</a></p></html>'''
    (ROOT / 'Report.html').write_text(page, encoding='utf-8')
    print(json.dumps(dict(calls=len(calls), editorChecks=report['verification']['editorChecks'], report=str(ROOT/'Report.html'))))


if __name__ == '__main__':
    main()
