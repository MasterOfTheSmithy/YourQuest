"""Bind the version-7 channel checkpoint to fresh source-matched verification."""
import argparse
import difflib
import json
from datetime import datetime, timezone
from update_joined_native_checkpoint import ROOT, EVIDENCE, read, sha, differences


def main():
    parser = argparse.ArgumentParser()
    for key in ('compile', 'basins', 'native', 'regressions', 'suite'):
        parser.add_argument('--' + key, required=True)
    args = parser.parse_args()
    paths = {key: EVIDENCE / getattr(args, key) / filename for key, filename in (
        ('compile', 'receipt.json'), ('basins', 'Receipt.json'), ('native', 'Receipt.json'),
        ('regressions', 'drainage-receipt.json'), ('suite', 'Receipt.json'))}
    compiled, basins, native, regressions, suite = [read(paths[key]) for key in paths]
    if compiled['status'] != 'PASS' or not compiled['inputs_unchanged_at_completion']:
        raise ValueError('No passing unchanged compilation')
    if not basins['passed'] or not regressions['passed'] or not read(paths['regressions'].parent / 'identity.json')['inputs_unchanged']:
        raise ValueError('Focused checks failed or inputs changed')
    if native['error'] or not native['inputsUnchanged']:
        raise ValueError('Native measurement incomplete or stale')
    if suite['checks'] != 79 or not suite['inputsUnchanged'] or not suite['baselineFailuresUnchanged']:
        raise ValueError('Broader suite changed or is stale')
    normalized = {key.replace('\\', '/'): value for key, value in compiled['source_sha256'].items()}
    for path, expected in normalized.items():
        if sha(ROOT / path) != expected:
            raise ValueError('Compiled source changed: ' + path)
    for receipt in (basins, native, suite):
        for path, expected in receipt['sourceSha256'].items():
            if normalized.get(path) != expected or sha(ROOT / path) != expected:
                raise ValueError('Verification source changed: ' + path)
        if receipt['runtimeAssemblySha256'] != sha(ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll'):
            raise ValueError('Verification runtime assembly changed')
    draft_path = paths['basins'].parent / 'new-draft.json'
    draft = read(draft_path)
    changed = differences(read(EVIDENCE / '20261011_141344_796_unity_basins/new-draft.json'), draft)
    if any(path not in ('/spatialPlanV2/contentHash', '/spatialPlanV2/validatedContentHash') and
           not path.endswith('/connectedDomain/version') for path in changed):
        raise ValueError('Unexpected canonical change: ' + str(changed))
    if any(water['connectedDomain']['version'] != 7 for water in draft['spatialPlanV2']['blueprint']['hydrology'] if water['hasConnectedDomain']):
        raise ValueError('New domain version is not pinned')
    retained = 0
    for sample_path in paths['native'].parent.glob('water-*-samples.json'):
        if '-joined-' in sample_path.name:
            continue
        old = read(EVIDENCE / '20261011_134113_170_native_shore' / sample_path.name)
        new = read(sample_path)
        if [(point['x'], point['z']) for point in old] != [(point['x'], point['z']) for point in new]:
            raise ValueError('Original nominal witnesses changed')
        retained += len(new)
    if retained != 3680:
        raise ValueError('Original nominal witness count changed')
    # note: Diff against captured dirty inputs, preserving both user edits and immutable earlier receipts.
    parts = []
    for original in sorted((EVIDENCE / 'native_channel_preimages').glob('*.cs')):
        path = 'Tools/TerrainRefactor/DrainageChecks.cs' if original.name == 'DrainageChecks.cs' else (
            'Assets/Assets/Scripts/Generated/' + ('Editor/' if 'Verification' in original.name else '') + original.name)
        parts.extend(difflib.unified_diff(original.read_text(encoding='utf-8').splitlines(True),
            (ROOT / path).read_text(encoding='utf-8').splitlines(True), fromfile='captured-dirty-preimage/' + path, tofile=path))
    patch_path = EVIDENCE / 'NativeChannelJoinPatch.diff'
    patch_path.write_text(''.join(parts), encoding='utf-8')
    aggregate = dict(utc=datetime.now(timezone.utc).isoformat(), status='INCOMPLETE', domainVersion=7,
        planHash=draft['spatialPlanV2']['contentHash'], receipts={key: path.relative_to(ROOT).as_posix() for key, path in paths.items()},
        canonicalChanges=changed, retainedNominalSamples=retained, joinedMeasurements=native['joinedMeasurements'],
        joinedContactsPassed=all(item['passed'] for item in native['joinedMeasurements']),
        originalNominalMeasurementPassed=native['passed'], originalNominalFailures=native['failures'],
        nativeChannelMeshes=read(paths['basins'].parent / 'native-channel-meshes.json'),
        sourceSha256=native['sourceSha256'], runtimeAssemblySha256=native['runtimeAssemblySha256'],
        broaderSuite=dict(checks=suite['checks'], failures=suite['failures'], baselineFailuresUnchanged=True),
        limits='Disposable native heightfields and pre-wave origin/streamed mesh fixtures. Final grading/colliders, materialized downstream profiles and join handoff, all water exclusions, mixed LOD, waves and ordinary travel are not certified; stages A-E/all fifteen gates remain incomplete.')
    aggregate_path = EVIDENCE / 'NativeChannelJoinVerification.json'
    aggregate_path.write_text(json.dumps(aggregate, indent=2) + '\n', encoding='utf-8')
    checkpoint_path = ROOT / 'Docs/TerrainRefactor_2026-10-11/Checkpoint.json'
    checkpoint = read(checkpoint_path)
    checkpoint.update(status='INCOMPLETE', observedOn=aggregate['utc'])
    checkpoint['receipts'].update(nativeChannelJoinVerification=aggregate_path.relative_to(ROOT).as_posix(),
        nativeChannelJoinPatch=patch_path.relative_to(ROOT).as_posix())
    checkpoint['freshVerification']['nativeChannelJoin'] = aggregate
    limitation = 'Version-7 mesh fixtures and native lake contacts pass; materialized downstream profiles, patch handoff, final grading/colliders, older bank/route distance approximations, mixed LOD and waves still require integrated verification.'
    if limitation not in checkpoint['knownLimits']:
        checkpoint['knownLimits'].append(limitation)
    detail = (' Domain version 7 derives channel wet polygons on the same bounded native terrain triangles as its lake, '
        'pins shared portal nodes to the lake datum, replaces origin and streamed ribbon geometry, and retains existing published water owners. '
        'Archived versions 1-6 keep their accepted interpretation and hashes. Current receipts and limitations are in NativeChannelJoinVerification.json.')
    for stage in checkpoint['stages']:
        if stage['id'] == 'B' and detail not in stage.get('implemented', ''):
            stage['implemented'] = stage.get('implemented', '') + detail
    checkpoint['nextConcreteStep'] = ('Measure materialized downstream water profiles, channel-patch handoff continuity and final graded terrain/colliders/water coverage; '
        'close any failed ownership probes and make bank/route exclusions consume the same water footprint. '
        'Then verify ordinary startup/streaming/travel. Generalized catchments, waterfalls/coast and stages A-E/all fifteen gates remain incomplete.')
    # note: Fresh directories are mandatory for reproductions; later source changes require new compilation and Unity receipts.
    commands = [
        'python Tools/PlayableEvaluation/compile_current.py --out outputs/TerrainRefactor_20261011/native_channel_compile_reproduction',
        'Unity idle Edit Mode: YourQuest/Terrain Refactor/Verify Basin Contracts (Read Only), then Measure Native Basin Shore (Disposable). Retain the new timestamped version-7 receipts; no live profile is selected.',
        'python Tools/TerrainRefactor/run_drainage.py --compile outputs/TerrainRefactor_20261011/native_channel_compile_reproduction --out outputs/TerrainRefactor_20261011/native_channel_regressions_reproduction --quick --unity-artifact outputs/TerrainRefactor_20261011/<fresh-unity-basins>/new-draft.json',
        'python Tools/TerrainRefactor/capture_joined_native_suite.py before --out native_channel_suite_reproduction --basins <fresh-unity-basins>; invoke Unity YourQuest/AAA World Generation/Run V2 Contract Tests; after completion run the same capture command with after in place of before.',
    ]
    for command in commands:
        if command not in checkpoint['reproductionCommands']:
            checkpoint['reproductionCommands'].append(command)
    checkpoint_path.write_text(json.dumps(checkpoint, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: aggregate[key] for key in ('status', 'domainVersion', 'planHash', 'joinedContactsPassed', 'joinedMeasurements', 'nativeChannelMeshes')}, indent=2))


if __name__ == '__main__':
    main()
