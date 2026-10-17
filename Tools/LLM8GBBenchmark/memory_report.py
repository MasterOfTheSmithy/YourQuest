"""Summarize measured Editor residency without promoting estimates to physical VRAM."""
import csv
import html
import json
import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmRequalification_20261008'
CENSUS = OUT / 'MemoryCensus'
GIB = 2**30
samples = [json.loads(line) for line in (OUT / 'HostTelemetry.jsonl').read_text(encoding='utf-8').splitlines() if line.strip()]

def parse_time(value):
    # note: Unity emits seven fractional digits; Python 3.10 accepts at most six.
    return datetime.fromisoformat(re.sub(r'(\.\d{6})\d+', r'\1', value).replace('Z', '+00:00'))

snapshots = []
for path in sorted(CENSUS.glob('20*.json')):
    raw = json.loads(path.read_text(encoding='utf-8'))
    totals, top_meshes, cached = {}, [], []
    tsv = path.with_suffix('.assetsession.tsv')
    lines = [line for line in tsv.read_text(encoding='utf-8-sig').splitlines() if not line.startswith('#')]
    for row in csv.DictReader(lines, delimiter='\t'):
        if row['kind'] == 'resourceTotal':
            totals[row['owner']] = dict(count=int(row['count']), estimatedBytes=int(row['estimatedBytes']))
        elif row['kind'] == 'cachedShard':
            cached.append(row['assetPath'])
        elif row['kind'] == 'resource' and row['assetPath'].lower().endswith('.fbx'):
            top_meshes.append(row)
    # note: Telemetry timestamps are ISO UTC; match snapshots by parsed time, and preserve physical GPU readings separately from estimates.
    moment = parse_time(raw['utc'])
    nearby = min(samples, key=lambda item: abs((parse_time(item['utc']) - moment).total_seconds()))
    process = next((item for item in nearby.get('processes', []) if item['pid'] == raw['processId']), {})
    snapshots.append(dict(file=path.name, utc=raw['utc'], pid=raw['processId'], playing=raw['playing'],
        released=raw['gameplayReleased'], isolation=raw['isolation'], totals=totals, cachedShards=cached,
        allocatedBytes=raw['allocatedBytes'], graphicsDriverEstimateBytes=raw['graphicsDriverAllocatedBytes'],
        textureMemoryBytes=raw['textureMemoryBytes'], gpu=nearby.get('gpu'), process=process,
        telemetrySecondsApart=abs((parse_time(nearby['utc'])-moment).total_seconds()), topMeshes=top_meshes))
settled_cleanup = None
for index, snapshot in enumerate(snapshots):
    if index and not snapshot['playing'] and not snapshot['cachedShards'] and snapshots[index-1]['cachedShards'] and snapshot['pid'] == snapshots[index-1]['pid']:
        moment = parse_time(snapshot['utc'])
        later = [sample for sample in samples if 30 <= (parse_time(sample['utc'])-moment).total_seconds() <= 90
                 and any(process['pid'] == snapshot['pid'] for process in sample.get('processes', []))]
        if later:
            observed = min(later, key=lambda sample: abs((parse_time(sample['utc'])-moment).total_seconds()-60))
            settled_cleanup = dict(utc=observed['utc'], gpu=observed['gpu'],
                process=next(process for process in observed['processes'] if process['pid'] == snapshot['pid']),
                note='Host reading about sixty seconds after cleanup; resources were not censused again at this exact time.')
report = dict(status='MODEL_BENCHMARK_BLOCKED_BY_UNITY_BASELINE', snapshots=snapshots, settledAfterCleanup=settled_cleanup,
    findings=[
        'The model-free assisted world replay reached GPU memory pressure before gameplay readiness. It is not a valid completed-world benchmark.',
        'After Play stopped, 15 registry shards, 5742 meshes and 1239 textures remained loaded; Unity dispatched zero inference requests and Ollama reported no loaded models.',
        'The runtime registry maps each asset to a publisher/pack shard. Serialized prefab/material references and their dependencies load at pack granularity.',
        'The builder preloads every palette slot across the accepted world plan, rather than only assets demanded near the current player.',
        'Texture mip streaming is disabled in project settings and observed runtime quality settings.',
        'An explicit idle cache reset and unused-asset cleanup after diagnostic script reload removed the registry shards and reduced meshes to nine. GPU usage declined but remained above the fresh-Editor baseline.',
        'The cleanup includes a script/domain reload, so its measured improvement cannot be attributed solely to one cache. Remaining graphics allocations have not been fully attributed.'
    ],
    necessaryWork=[
        'Replace broad asset-pack residency with demand loading through the existing registry owner; keep accepted asset identities, materials, GUIDs and deterministic choices.',
        'Give loaded dependencies a clear lifetime tied to actual world/chunk consumers, with bounded prefetch and release after the last consumer.',
        'Review owned runtime material/mesh disposal on Play exit and assembly reload. DontSave objects require explicit owner disposal.',
        'Measure texture streaming and LOD budgets after reducing unnecessary residency; verify appearance before accepting quality changes.',
        'Resolve the existing fallback world readiness failure in streamed chunk (0,8), without weakening the readiness gate.',
        'Repeat a complete rendered-world baseline, then resume model qualification with measured Unity headroom.'
    ],
    scope='This run added benchmark isolation and diagnostic tooling. No asset deletion, quality/import change, world-authority replacement, or model-ranking claim.',
    sources=[
        {'path':'Assets/Assets/Scripts/Generated/YQRuntimeWorldAssetRegistry.cs','lines':[729,875,945,1333]},
        {'path':'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs','lines':[1998,4743]},
        {'path':'ProjectSettings/QualitySettings.asset','lines':[40]},
        {'url':'https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.UnloadUnusedAssets.html'},
        {'url':'https://docs.unity3d.com/6000.0/Documentation/ScriptReference/HideFlags.DontSave.html'}])
(OUT / 'MemoryFindings.json').write_text(json.dumps(report, indent=2), encoding='utf-8')

rows = []
for snap in snapshots:
    phase = 'World loading' if snap['playing'] else 'Editor, retained world' if snap['cachedShards'] else 'Editor, no cached world packs'
    valid_host = snap['telemetrySecondsApart'] <= 10
    gpu = f"{snap['gpu']['usedMiB']/1024:.2f}" if snap.get('gpu') and valid_host else 'not sampled nearby'
    rss = f"{snap['process']['memory']['rss']/GIB:.2f}" if snap['process'].get('memory') and valid_host else 'not sampled nearby'
    rows.append(f"<tr><td>{html.escape(snap['utc'][11:19])}</td><td>{phase}</td><td>{gpu}</td><td>{rss}</td><td>{snap['totals']['Mesh']['count']:,}</td><td>{snap['totals']['Texture']['count']:,}</td><td>{len(snap['cachedShards'])}</td></tr>")
if settled_cleanup:
    rows.append(f"<tr><td>{settled_cleanup['utc'][11:19]}</td><td>Cleanup, about 60 seconds later</td><td>{settled_cleanup['gpu']['usedMiB']/1024:.2f}</td><td>{settled_cleanup['process']['memory']['rss']/GIB:.2f}</td><td colspan='3'>Objects not resampled at this time</td></tr>")
items = lambda values: ''.join('<li>'+html.escape(value)+'</li>' for value in values)
document = '''<!doctype html><meta charset="utf-8"><title>YourQuest memory diagnosis</title>
<style>body{background:#101827;color:#e8eef7;font:16px/1.65 system-ui;margin:38px auto;max-width:1100px;padding:0 24px}h1{font-size:32px;line-height:1.2}h2{font-size:22px;margin-top:32px}p,li{max-width:960px}li{margin-bottom:10px}.tag{background:#543b15;padding:8px 14px;display:inline-block;border-radius:5px}table{border-collapse:collapse;width:100%;font-size:14px}th,td{padding:10px;border-bottom:1px solid #38455b;text-align:left}th{color:#9fbcdf}a{color:#a0ceff}.muted{color:#b7c2d2}</style>
<h1>Why Unity kept most of the GPU memory after Play stopped</h1>
<p class="tag">Reproduced with inference disabled · Model scoring held</p>
<p>The world loads asset packs containing thousands of meshes and textures, then retains those packs after Play exits. Releasing unused content recovered substantial memory. Some graphics allocations remained, so this is a diagnosis and partial recovery, not a completed memory repair.</p>
<h2>Measured snapshots</h2><p class="muted">GPU is total device usage from NVIDIA; RAM is the Unity process resident set. Neither is the sum of Unity's per-object allocation estimates. Times are UTC.</p>
<table><tr><th>Time</th><th>State</th><th>GPU GiB</th><th>Unity RAM GiB</th><th>Loaded meshes</th><th>Loaded textures</th><th>Cached packs</th></tr>'''+''.join(rows)+'''</table>
<h2>What the evidence establishes</h2><ul>'''+items(report['findings'])+'''</ul>
<h2>Repair sequence</h2><ol>'''+items(report['necessaryWork'])+'''</ol>
<p class="muted">'''+html.escape(report['scope'])+'''</p>
<p>Unity references: <a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.UnloadUnusedAssets.html">unused-asset collection follows references</a>; <a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/HideFlags.DontSave.html">DontSave objects require explicit cleanup</a>.</p>
<p><a href="MemoryFindings.json">Detailed receipt</a> · <a href="MemoryReproduction.json">Controlled reproduction</a> · <a href="MemoryCensus/">Raw censuses</a></p>'''
(OUT / 'MemoryReport.html').write_text(document, encoding='utf-8')
print(json.dumps(dict(snapshots=len(snapshots), report=str(OUT / 'MemoryReport.html'))))
