"""Bind the water-clearance repair to fresh evidence without promoting fixture results to gameplay acceptance."""
import argparse
import difflib
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'outputs/TerrainRefactor_20261011'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def current(binding):
    assert all(sha(ROOT / name) == expected for name, expected in binding['sourceSha256'].items()), 'Stale source evidence'
    assert sha(ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll') == binding['runtimeAssemblySha256'], 'Stale Unity assembly'


parser = argparse.ArgumentParser()
parser.add_argument('--basins', required=True)
parser.add_argument('--profiles', required=True)
parser.add_argument('--compile', required=True)
parser.add_argument('--regressions', required=True)
parser.add_argument('--suite', required=True)
args = parser.parse_args()

# note: Require source-bound completed receipts and retain the expected failed before measurement as a before result, never a passing gate.
basins = read(EVIDENCE / args.basins / 'Receipt.json')
profiles = read(EVIDENCE / args.profiles / 'Receipt.json')
compiled = read(EVIDENCE / args.compile / 'receipt.json')
identity = read(EVIDENCE / args.regressions / 'identity.json')
suite = read(EVIDENCE / args.suite / 'Receipt.json')
before = read(EVIDENCE / '20261011_153352_568_water_profiles/Receipt.json')
for binding in (basins, profiles, suite):
    current(binding)
assert basins['passed'] and profiles['passed'] and profiles['inputsUnchanged']
assert compiled['status'] == 'PASS' and compiled['source_hashes_unchanged_at_completion']
assert all(sha(ROOT / name) == expected for name, expected in compiled['source_sha256'].items())
assert identity['inputs_unchanged'] and identity['compiler_exit'] == 0 and identity['run_exit'] == 0, 'Detached checks failed or changed inputs'
regressions = read(EVIDENCE / args.regressions / 'drainage-receipt.json')
parity = read(EVIDENCE / args.regressions / 'unity-artifact-hash.json')
assert regressions['passed'] and len(regressions['checks']) == 50 and parity['passed'], 'Detached regressions or Unity artifact parity failed'
assert identity['compiled_source_receipt'] == str((EVIDENCE / args.compile / 'receipt.json').resolve()), 'Regressions used a different compile'
assert all(sha(Path(name)) == expected for name, expected in identity['input_sha256'].items()), 'Detached inputs are stale'
assert suite['checks'] == 79 and suite['failures'] == 2 and suite['inputsUnchanged'] and suite['baselineFailuresUnchanged']
assert before['profilePassed'] and before['missedWetClearance'] == 132 and not before['clearancePassed']
assert before['inputSha256'] == profiles['inputSha256'], 'Before/after accepted artifact differs'
assert sha(ROOT / profiles['input']) == profiles['inputSha256']

relative = 'Assets/Assets/Scripts/Generated/'
files = ('YQConnectedBasinDomainV2.cs', 'YQSpatialBlueprintTerrainSamplerV2.cs',
         'YQSpatialMaterializationV2.cs', 'YQContinuousWorldFeatureAuthority.cs',
         'Editor/YQTerrainRefactorBasinVerification.cs')
patch = []
for file in files:
    preimage = EVIDENCE / 'water_handoff_preimages' / Path(file).name
    if '/Editor/' not in relative + file:
        assert sha(preimage) == before['sourceSha256'][relative + file], 'Before runtime differs from captured preimage'
    patch.extend(difflib.unified_diff(preimage.read_text(encoding='utf-8').splitlines(True),
        (ROOT / relative / file).read_text(encoding='utf-8').splitlines(True),
        fromfile='captured-domain-7-preimage/' + relative + file, tofile=relative + file))
(EVIDENCE / 'WaterClearancePatch.diff').write_text(''.join(patch), encoding='utf-8')

paths = dict(before=EVIDENCE / '20261011_153352_568_water_profiles/Receipt.json',
    after=EVIDENCE / args.profiles / 'Receipt.json', basins=EVIDENCE / args.basins / 'Receipt.json',
    compile=EVIDENCE / args.compile / 'receipt.json', regressions=EVIDENCE / args.regressions / 'drainage-receipt.json',
    identity=EVIDENCE / args.regressions / 'identity.json', suite=EVIDENCE / args.suite / 'Receipt.json')
result = dict(utc=datetime.now(timezone.utc).isoformat(), status='INCOMPLETE',
    repair='Version-7 finite bank clearance consumes accepted ribbon quads outside native joins and the exact native wet polygons inside them; old accepted versions and terminal rays remain pinned.',
    receipts={name: path.relative_to(ROOT).as_posix() for name, path in paths.items()},
    receiptSha256={name: sha(path) for name, path in paths.items()},
    sourceSha256=profiles['sourceSha256'], runtimeAssemblySha256=profiles['runtimeAssemblySha256'],
    inputSha256=profiles['inputSha256'], beforeMissedWetPoints=132,
    afterMeasurements={name: profiles[name] for name in ('bankProbes', 'missedWetClearance', 'probes', 'missing',
        'uphill', 'maskMismatch', 'maximumRiseMetres', 'maximumMaskDeltaMetres', 'distanceProbes', 'distanceMismatch', 'maximumDistanceErrorMetres')},
    detachedCompile=dict(status=compiled['status'], runtimeSources=compiled['runtime_sources'], editorSources=compiled['editor_sources'], elapsedSeconds=compiled['elapsed_seconds']),
    focusedEditorChecks=len(basins['checks']), detachedChecks=len(regressions['checks']), artifactHashParity=parity,
    existingContractSuite=dict(checks=suite['checks'], failures=suite['failures'], reasons=suite['reasons'], baselineFailuresUnchanged=True),
    inputFidelityCorrection='The first sparse-control profile fixture had 17 level disagreements; matching the production 0.35m subdivision removed them. This was a diagnostic input error, not a repaired production profile defect.',
    limits=['Editor disposable preview meshes only; no final graded terrain, colliders, waves, exact patch-edge sweep, mixed LOD or ordinary travel proof.',
            'No standalone or target-hardware performance certification; index sorting/allocations occur during immutable preparation.',
            'Native overlapping-join sampler ownership remains a separate outstanding check; clearance clips earlier patches in render replacement order.'])
(EVIDENCE / 'WaterClearanceVerification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')

# note: Keep all fifteen full gates open and historical receipts visible; this bounded repair does not complete representative geography or sustained runtime quality.
path = ROOT / 'Docs/TerrainRefactor_2026-10-11/Checkpoint.json'
checkpoint = read(path)
checkpoint['observedOn'] = result['utc']
checkpoint['status'] = 'INCOMPLETE'
checkpoint['receipts']['waterClearance'] = 'outputs/TerrainRefactor_20261011/WaterClearanceVerification.json'
checkpoint['freshVerification']['waterClearance'] = dict(aggregateReceipt=checkpoint['receipts']['waterClearance'],
    utc=result['utc'], sourceSha256=result['sourceSha256'], runtimeAssemblySha256=result['runtimeAssemblySha256'],
    measurements=result['afterMeasurements'], focusedEditorChecks=result['focusedEditorChecks'], detachedChecks=result['detachedChecks'],
    existingContractSuite=result['existingContractSuite'], limits=result['limits'])
checkpoint['source']['connectedDomainVersion'] = 'New drafts use version 7: shared bounded native lake/channel polygons, canonical bank frames and exact finite placement-clearance geometry. Accepted versions 1-6 retain their geometry, routing and hashes.'
checkpoint['source']['runtimeAccess'] = 'AUTHORIZED. Current source: clean detached compile; 50 detached and 16 focused Unity checks pass. Disposable pre-wave meshes pass 1,886 profile probes, 53,466 wet-clearance probes and 78 distance comparisons. Wider 79-check suite retains two unchanged baseline failures. Ordinary version-7 publication, final grading/colliders, travel, waves and player-build performance remain unverified.'
checkpoint['freshWaterClearanceEvidence'] = dict(beforeMissedWetPoints=132,
    afterMissedWetPoints=profiles['missedWetClearance'], bankProbes=profiles['bankProbes'],
    waterProfileProbes=profiles['probes'], maximumWaterLevelDeltaMetres=profiles['maximumMaskDeltaMetres'],
    distanceProbes=profiles['distanceProbes'], maximumDistanceErrorMetres=profiles['maximumDistanceErrorMetres'],
    evidence='UNITY_EDITOR_DISPOSABLE_PREWAVE_PRODUCTION_WATER_MESHES_NOT_ORDINARY_TRAVEL')
checkpoint['nextConcreteStep'] = 'Measure exact native-patch handoff and final graded terrain/collider agreement, then exercise version-7 ordinary startup and streaming on a disposable profile; continue stages A-E and all fifteen gates.'
for stage in checkpoint['stages']:
    if stage['id'] != 'B':
        continue
    stage['implemented'] = ('Topology_12 persists bounded connected basin beds and wet components; gate_3 validates metre-based drainage and explicit open/closed basin metadata. Version 7 derives native lake/channel wet polygons from the frozen source height field, pins receiving portals to the lake datum, and publishes through existing origin and streamed owners. Canonical bank offsets survive subdivision and clipping. An immutable triangle index now measures finite placement clearance from the same ribbon/native wet union, with old accepted versions and approved terminal rays preserved. Accepted heights and content hashes are unchanged by this clearance repair. Current compile, focused regressions, profile and clearance evidence are recorded in WaterClearanceVerification.json. Earlier version-by-version investigations and failed receipts remain available; they are historical measurements, not current blanket acceptance.')
    stage['nextGate'] = checkpoint['nextConcreteStep']
    if 'Tools/TerrainRefactor/update_water_clearance_checkpoint.py' not in stage['files']:
        stage['files'].append('Tools/TerrainRefactor/update_water_clearance_checkpoint.py')
for capability in checkpoint['capabilityMatrix']:
    if capability['requirement'] == 'Drainage':
        capability['runtime'] = 'Current Editor fixtures: 16 focused checks and 1,886 materialized downstream profile samples pass. No rise or missing coverage at the sampled near-join centerlines; maximum mesh/sampler level delta 0.034mm. Final grading, exact handoff, waves, mixed LOD and ordinary travel remain unverified.'
limitation = 'Version-7 pre-wave centerline and clearance fixtures pass; final grading/colliders, exact handoff, waves, mixed LOD, ordinary travel and performance remain unverified.'
if limitation not in checkpoint['knownLimits']:
    checkpoint['knownLimits'].append(limitation)
commands = ['python Tools/PlayableEvaluation/compile_current.py --out outputs/TerrainRefactor_20261011/<fresh-water-clearance-compile>',
            'Unity: YourQuest/Terrain Refactor/Verify Basin Contracts (Read Only), then Measure Joined Water Profiles (Read Only). These create timestamped disposable Editor receipts.',
            'python Tools/TerrainRefactor/update_water_clearance_checkpoint.py --compile <fresh-water-clearance-compile> --basins <fresh-unity-basins> --profiles <fresh-water-profiles> --regressions <fresh-detached-checks> --suite <fresh-contract-suite>']
checkpoint['reproductionCommands'] = checkpoint.get('reproductionCommands', []) + commands
path.write_text(json.dumps(checkpoint, indent=2), encoding='utf-8')
print(json.dumps(checkpoint['freshWaterClearanceEvidence'], indent=2))
