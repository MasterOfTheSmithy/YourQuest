"""Compare the bounded memory passes; retain their gameplay and GPU attribution limits."""
import html
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/UnityMemoryHeadroom_20261008'
BASE = ROOT / 'outputs/LlmRequalification_20261008'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def summarize(run):
    # note: Report whole-device telemetry separately from Unity's allocation estimates.
    result = []
    for c in run['captures']:
        if 'host' not in c:
            continue
        host = c['host']
        process = next(p for p in host['processes'] if p['pid'] == run['ownerPid'])
        stamp = c['utc'].replace('-', '').replace(':', '').replace('T', '_')[:15]
        paths = sorted((BASE / 'MemoryCensus').glob(stamp + '*.assetsession.tsv'))
        counts, registries = {}, []
        if paths:
            for line in paths[0].read_text(encoding='utf-8-sig').splitlines():
                cells = line.split('\t')
                if cells[0] == 'resourceTotal':
                    counts[cells[1]] = {'count': int(cells[3]), 'estimatedBytes': int(cells[4])}
                elif cells[0] == 'loadedRegistry':
                    registries.append(cells[2])
        result.append(dict(reason=c['reason'], utc=c['utc'], playing=c['playing'],
                           gpuUsedMiB=host['gpu']['usedMiB'], gpuFreeMiB=host['gpu']['freeMiB'],
                           unityRssBytes=process['memory']['rss'], unityDriverEstimateBytes=c['driverBytes'],
                           resourceEstimates=counts, loadedRegistries=registries,
                           inferenceDispatches=host['unity'].get('inferenceDispatches'),
                           profileId=host['unity'].get('profileId'), worldSeed=host['unity'].get('worldSeed')))
    return result


run = read(OUT / 'MemoryReproduction.json')
assert run['status'] == 'captured', 'Wait for the bounded replay to finish.'
current = summarize(run)
prior = summarize(read(ROOT / 'outputs/UnityMemoryRepair_20261008/CleanupRetest/MemoryReproduction.json'))
original = summarize(read(BASE / 'MemoryReproduction.json'))
checks = read(OUT / 'MaterialSearchVerification.json')
profiles = read(OUT / 'ProfilePreservation.json')
identity = read(OUT / 'FinalSourceIdentity.json')
cleanup = read(OUT / 'LifecycleAfterEvidence.json')
assert checks['status'] == 'PASS' and not profiles['changed'] and identity['runtimeCompileSourceMatches']
assert cleanup['status'] == 'PASS'


def stage(rows, playing):
    return next(r for r in reversed(rows) if r['playing'] == playing)


report = dict(utc=datetime.now(timezone.utc).isoformat(), original=original, previousRepair=prior,
              current=current, verification=checks, profiles=profiles, sourceIdentity=identity, automaticCleanup=cleanup,
              beforeLifecycleFix=summarize(read(OUT / 'MemoryReproduction_BeforeLifecycleFix.json')),
              beforeRetainedCacheFix=summarize(read(OUT / 'MemoryReproduction_BeforeCacheFix.json')),
              freshIdleHost=read(OUT / 'FreshIdleHost.json'),
              retainedCacheComparison=read(OUT / 'RetainedCacheComparison.json'),
              interpretation='No material additional in-world GPU reduction was demonstrated in this replay. The new search prevents project-wide asset discovery from loading every material dependency as the library grows.',
              changes=[
                  'Material discovery reads names and paths, rather than loading all material assets and their textures.',
                  'Existing selection scores and original-order tie breaking are preserved; only candidates that could win are loaded.',
                  'Nearby texture selection ranks metadata before loading candidate textures.',
                  'Editor search metadata is invalidated when project assets change and at session cleanup.',
                  'Play-exit collection remains pending until the Editor is idle; import/compilation and a domain reload cannot silently discard the request.',
                  'Prepared building manifests, site catalog, 2D art registry, tutorial prefab recovery and Blueglass sprite caches now release their session references on Play exit.',
                  'Owned generated road/water geometry is disposed after scene teardown. Generated terrain layers and water materials no longer opt out of unused-asset collection.'
              ], limitations=[
                  'All GPU values are whole-device usage, not Unity-only VRAM. Unity allocation estimates are not physical VRAM.',
                  'The two repaired passes settled at the same stationary test world; capture timing differs. Small differences are not proof of a performance improvement.',
                  'The original pressured capture was at an earlier world-loading stage; it is not an identical completed scene comparison.',
                  'The existing chunk (0,8) structural-materialization failure still blocks gameplay release.',
                  'No LLM inference ran. Goddess/NPC speech, model coexistence, travel, visual equivalence and player builds remain unverified.',
                  'Selection equivalence covers 14 bounded material queries, not every asset or nearby-texture query.',
                  'Opaque asset formats retain conservative candidate loading; metadata indexing still has a CPU/IO cost.',
                  'Post-Play graphics allocation still exceeds the fresh idle Editor. The remaining difference is not fully attributed; an exact cold-baseline return is not claimed.'
              ])
(OUT / 'MemoryHeadroomReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
idle = report['freshIdleHost']
rows = f'<tr><th>Fresh idle Editor</th><td>{idle["gpuUsedMiB"]/1024:.2f} GiB</td><td>{idle["gpuFreeMiB"]/1024:.2f} GiB</td><td>{idle["unityRssBytes"]/2**30:.2f} GiB</td></tr>'
for label, data, playing in [
        ('Original: world under pressure', original, True),
        ('Previous repair: settled world', prior, True),
        ('Current: settled world', current, True),
        ('Previous repair: after Play', prior, False),
        ('Before final cache repair: after Play', report['beforeRetainedCacheFix'], False),
        ('Current: after Play', current, False)]:
    r = stage(data, playing)
    rows += f'<tr><th>{html.escape(label)}</th><td>{r["gpuUsedMiB"]/1024:.2f} GiB</td><td>{r["gpuFreeMiB"]/1024:.2f} GiB</td><td>{r["unityRssBytes"]/2**30:.2f} GiB</td></tr>'
sections = ''.join('<h2>' + title + '</h2><ul>' + ''.join('<li>' + html.escape(s) + '</li>' for s in report[key]) + '</ul>'
                   for title, key in [('Changed', 'changes'), ('Evidence limits', 'limitations')])
page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>YourQuest asset-growth memory repair</title><style>body{margin:0;background:#101820;color:#e8f1f7;font:17px/1.55 system-ui,sans-serif}
main{max-width:1040px;margin:42px auto;padding:0 24px}h1{font-size:32px}h2{font-size:23px;margin-top:32px}
p,li{color:#c3d2de}table{width:100%;border-collapse:collapse;background:#162631}th,td{padding:12px;text-align:left;border-bottom:1px solid #365065}
a,thead{color:#7cd4ff}.note{border-left:4px solid #dcac62;padding:12px 18px;background:#24282c}small{color:#9dafbd}
@media(max-width:700px){table{font-size:13px}th,td{padding:8px}}</style><main>
<small>YOURQUEST · RTX 5060 8 GB · 8 OCTOBER 2026</small><h1>Releasing game assets when their session ends.</h1>
<p>Building and UI caches retained imported assets after Play; generated terrain and water copies also prevented unused-asset unloading. Their owners now release those session references. Material searches use metadata first, keeping the original selection rules without loading the entire library.</p>
<p class="note">''' + html.escape(report['interpretation']) + '''</p>
<table><thead><tr><th>Measured stage</th><th>GPU used, all apps</th><th>GPU free</th><th>Unity RAM</th></tr></thead><tbody>''' + rows + '''</tbody></table>
<p>Verification: 20,163 candidates indexed with zero newly loaded project textures or meshes; 14 bounded material-selection comparisons passed; 646 pre-existing profile files unchanged. Runtime compilation and the Editor verification succeeded. Automatic cleanup released the world binding leaves, prepared manifests and UI registry cache; one empty metadata registry remains loaded. No asset files or import quality settings were changed by this pass.</p>''' + sections + '''
<p>Unity's own importer inspector provides the material-name metadata pattern used for model assets: <a href="https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Modules/AssetPipelineEditor/ImportSettings/ModelImporterMaterialEditor.cs">official Unity source</a>.</p>
<small>Raw replay, source identity, compilation, selection and preservation receipts accompany this report.</small></main></html>'''
(OUT / 'MemoryHeadroomReport.html').write_text(page, encoding='utf-8')
print(json.dumps({'report': str(OUT / 'MemoryHeadroomReport.html'), 'currentStages': [
    {k: r[k] for k in ('reason', 'gpuUsedMiB', 'gpuFreeMiB', 'unityRssBytes')} for r in current]}, indent=2))
