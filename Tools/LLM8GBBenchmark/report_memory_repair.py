"""Summarize the bounded memory replays without treating them as gameplay passes."""
import html
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/LlmRequalification_20261008'
OUT = ROOT / 'outputs/UnityMemoryRepair_20261008'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def summarize(path):
    run = read(path)
    stages = []
    for capture in run['captures']:
        if 'host' not in capture:
            continue
        host = capture['host']
        process = next((p for p in host['processes'] if p['pid'] == run['ownerPid']), {})
        stamp = capture['utc'].replace('-', '').replace(':', '').replace('T', '_')[:15]
        candidates = sorted((BASE / 'MemoryCensus').glob(stamp + '*.assetsession.tsv'))
        counts = {}
        registries = []
        if candidates:
            for line in candidates[0].read_text(encoding='utf-8-sig').splitlines():
                cells = line.split('\t')
                if cells[0] == 'resourceTotal':
                    counts[cells[1]] = dict(count=int(cells[3]), estimatedBytes=int(cells[4]))
                elif cells[0] == 'loadedRegistry':
                    registries.append(cells[2])
        stages.append(dict(reason=capture['reason'], utc=capture['utc'], playing=capture['playing'],
                           totalGpuMiB=host['gpu']['usedMiB'], freeGpuMiB=host['gpu']['freeMiB'],
                           unityRssBytes=process.get('memory', {}).get('rss'),
                           unityAllocatedBytes=capture['allocatedBytes'], driverEstimateBytes=capture['driverBytes'],
                           resourceEstimates=counts, loadedRegistryCount=len(registries),
                           legacyPackRegistries=sum('/YQWorldAssetShards/' in p for p in registries),
                           granularRegistries=sum('/YQWorldAssetLeaves/' in p for p in registries)))
    return dict(pid=run['ownerPid'], status=run['status'], stages=stages, source=str(path.relative_to(ROOT)))


before = summarize(BASE / 'MemoryReproduction.json')
first = summarize(OUT / 'MemoryReproduction_FirstCleanup.json')
second_path = OUT / 'CleanupRetest/MemoryReproduction.json'
after = summarize(second_path) if second_path.exists() else first
verification = read(OUT / 'BindingConservation.json')
profiles = read(OUT / 'ProfilePreservation.json')
report = dict(utc=datetime.now(timezone.utc).isoformat(), before=before, after=after, firstRepairReplay=first,
              bindings=verification, profiles=profiles,
              changes=['Per-asset Resources bindings replace pack-wide runtime loading; existing pack assets and GUIDs preserved.',
                       'The existing registry drops its loaded-asset graph on cache release and collects unused Editor assets after Play teardown.',
                       'Runtime material copies have explicit ownership and cleanup; imported materials are preserved.'],
              preserved=['8174 complete serialized bindings', '29505 material overrides', '106 legacy registry shards',
                         'Existing asset GUIDs and model/texture files', 'World seed 0a24f727 and accepted V2 hash f2873abff6581898',
                         '646 pre-existing profile files; the benchmark uses its separate named profile'],
              limitations=['Assisted rendered-world memory replay; not ordinary player-input qualification.',
                           'The existing chunk (0,8) structural-materialization failure still blocks gameplay release.',
                           'No local LLM dispatched inference. No model coexistence or NPC/Goddess speech acceptance is claimed.',
                           'Long travel, a packaged player build, and visual equivalence have not been tested.',
                           'GPU totals include all processes; per-object Unity estimates must not be summed as physical VRAM.'])
(OUT / 'MemoryRepairReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


def stage(run, playing):
    return next((s for s in reversed(run['stages']) if s['playing'] == playing), None)


def cells(run, playing):
    s = stage(run, playing)
    if not s:
        return '<td colspan="3">Pending</td>'
    return f'<td>{s["totalGpuMiB"]/1024:.2f} GiB</td><td>{s["freeGpuMiB"]/1024:.2f} GiB</td><td>{s["unityRssBytes"]/2**30:.2f} GiB</td>'


rows = ''.join(f'<tr><th>{name}</th>{cells(run, playing)}</tr>' for name, run, playing in [
    ('Before: world under memory pressure', before, True), ('After: world settled', after, True),
    ('Before: Play stopped, Editor open', before, False), ('After: Play stopped, Editor open', after, False)])
sections = ''.join('<h2>' + title + '</h2><ul>' + ''.join('<li>' + html.escape(item) + '</li>' for item in report[key]) + '</ul>'
                   for title, key in [('What changed', 'changes'), ('Preserved', 'preserved'), ('Evidence limits', 'limitations')])
page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>YourQuest memory repair</title><style>body{margin:0;background:#101820;color:#e8f1f7;font:17px/1.55 system-ui,sans-serif}
main{max-width:1000px;margin:48px auto;padding:0 24px}h1{font-size:36px}h2{font-size:23px;margin-top:36px}
p,li{max-width:85ch;color:#c3d2de}table{width:100%;border-collapse:collapse;background:#162631}th,td{padding:14px;text-align:left;border-bottom:1px solid #365065}
thead{color:#7cd4ff}a{color:#7cd4ff}.note{border-left:4px solid #dcac62;padding:12px 18px;background:#24282c}
small{color:#9dafbd}@media(max-width:700px){table{font-size:13px}th,td{padding:8px}}</style><main>
<small>YOURQUEST · 8 GB GPU · NONDESTRUCTIVE REPAIR · 8 OCTOBER 2026</small>
<h1>Whole asset packs were being loaded for individual props.</h1>
<p>The runtime now resolves small asset records using the same original prefab and material references. End-of-Play cleanup also releases owned materials and registry references.</p>
<table><thead><tr><th>Measured stage</th><th>Total GPU used</th><th>GPU free</th><th>Unity RAM</th></tr></thead><tbody>''' + rows + '''</tbody></table>
<p class="note">The same accepted test-world plan was replayed with inference blocked. Its existing terrain-readiness failure remains. These are memory measurements, not a gameplay or LLM acceptance pass.</p>''' + sections + '''
<p>Cleanup uses Unity's reference-preserving <a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.UnloadUnusedAssetsImmediate.html">unused-asset collection</a>; it does not remove asset files.</p>
<small>Raw measurements, source preimages, compilation and conservation receipts are beside this report.</small></main></html>'''
(OUT / 'MemoryRepairReport.html').write_text(page, encoding='utf-8')
print(json.dumps(dict(report=str(OUT / 'MemoryRepairReport.html'), before=before, after=after), indent=2))
