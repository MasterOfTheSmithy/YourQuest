"""Bind the native lake join checkpoint to fresh, immutable verification receipts."""
import argparse
import difflib
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'outputs/TerrainRefactor_20261011'


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def differences(first, second, prefix=''):
    if type(first) is not type(second):
        return [prefix]
    if isinstance(first, dict):
        return [item for key in sorted(first.keys() | second.keys()) for item in
                (differences(first[key], second[key], prefix + '/' + key)
                 if key in first and key in second else [prefix + '/' + key])]
    if isinstance(first, list):
        if len(first) != len(second):
            return [prefix + '/length']
        return [item for index, (a, b) in enumerate(zip(first, second))
                for item in differences(a, b, prefix + '/' + str(index))]
    return [] if first == second else [prefix]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--basins', required=True)
    parser.add_argument('--native', required=True)
    parser.add_argument('--regressions', required=True)
    args = parser.parse_args()
    compiled_path = EVIDENCE / 'joined_native_compile_4/receipt.json'
    basins_path = EVIDENCE / args.basins / 'Receipt.json'
    native_path = EVIDENCE / args.native / 'Receipt.json'
    regression_path = EVIDENCE / args.regressions / 'drainage-receipt.json'
    identity_path = regression_path.parent / 'identity.json'
    compiled, basins, native, regressions, identity = map(read,
        (compiled_path, basins_path, native_path, regression_path, identity_path))
    if compiled['status'] != 'PASS' or not compiled['inputs_unchanged_at_completion']:
        raise ValueError('No passing unchanged compilation')
    if not basins['passed'] or not regressions['passed'] or not identity['inputs_unchanged']:
        raise ValueError('Focused contracts or source identity failed')
    if native['error'] or not native['inputsUnchanged']:
        raise ValueError('Native measurement did not finish with stable inputs')
    normalized_sources = {key.replace('\\', '/'): value for key, value in compiled['source_sha256'].items()}
    for path, expected in normalized_sources.items():
        if sha(ROOT / path) != expected:
            raise ValueError('Compilation source changed: ' + path)
    for receipt in (basins, native):
        for path, expected in receipt['sourceSha256'].items():
            if sha(ROOT / path) != expected or normalized_sources.get(path) != expected:
                raise ValueError('Stale verification source: ' + path)
    if basins['runtimeAssemblySha256'] != native['runtimeAssemblySha256']:
        raise ValueError('Native and focused contracts used different Unity assemblies')
    if sha(ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll') != native['runtimeAssemblySha256']:
        raise ValueError('Loaded runtime identity changed')
    old_path = EVIDENCE / '20261011_133900_123_unity_basins/new-draft.json'
    new_path = basins_path.parent / 'new-draft.json'
    changes = differences(read(old_path), read(new_path))
    if any(path not in ('/spatialPlanV2/contentHash', '/spatialPlanV2/validatedContentHash') and not path.endswith('/connectedDomain/version') for path in changes):
        raise ValueError('Native join unexpectedly changed other canonical records: ' + str(changes))
    old_native = read(EVIDENCE / '20261011_134113_170_native_shore/Receipt.json')
    for index, measurement in enumerate(native['measurements']):
        if measurement['samples'] != old_native['measurements'][index]['samples']:
            raise ValueError('A nominal witness was dropped')
    for sample_path in sorted(native_path.parent.glob('water-*-samples.json')):
        if '-joined-' in sample_path.name:
            continue
        old_samples = read(EVIDENCE / '20261011_134113_170_native_shore' / sample_path.name)
        new_samples = read(sample_path)
        if [(item['x'], item['z']) for item in old_samples] != [(item['x'], item['z']) for item in new_samples]:
            raise ValueError('Nominal witness positions changed')
    parts = []
    preimages = EVIDENCE / 'joined_native_preimages'
    for original in sorted(preimages.glob('*')):
        if original.suffix != '.cs':
            continue
        relative = 'Tools/TerrainRefactor/DrainageChecks.cs' if original.name == 'DrainageChecks.cs' else (
            'Assets/Assets/Scripts/Generated/' + ('Editor/' if 'Verification' in original.name else '') + original.name)
        parts.extend(difflib.unified_diff(original.read_text(encoding='utf-8').splitlines(True),
            (ROOT / relative).read_text(encoding='utf-8').splitlines(True),
            fromfile='captured-dirty-preimage/' + relative, tofile=relative))
    patch_path = EVIDENCE / 'JoinedNativeLakePatch.diff'
    patch_path.write_text(''.join(parts), encoding='utf-8')
    joined = native['joinedMeasurements']
    aggregate = dict(utc=datetime.now(timezone.utc).isoformat(), status='INCOMPLETE', domainVersion=6,
        compilation=compiled_path.relative_to(ROOT).as_posix(), focusedUnity=basins_path.relative_to(ROOT).as_posix(),
        detachedRegression=regression_path.relative_to(ROOT).as_posix(), native=native_path.relative_to(ROOT).as_posix(),
        joinedMeasurements=joined, canonicalChanges=changes, retainedNominalSamples=sum(item['samples'] for item in native['measurements']),
        sourceSha256=native['sourceSha256'], runtimeAssemblySha256=native['runtimeAssemblySha256'],
        runtimeAssemblyMvid=native['runtimeAssemblyMvid'],
        broaderSuite='Domain-5 79-check suite is historical; domain-6 broader regression still pending.',
        limits='Native disposable heightfield contact and all ownership portals; not final graded colliders/meshes, a combined channel wet union, ordinary PlaySafe travel, performance or full staged acceptance.')
    aggregate_path = EVIDENCE / 'JoinedNativeLakeVerification.json'
    suite_path = EVIDENCE / 'joined_native_contract_suite/Receipt.json'
    if suite_path.exists():
        suite = read(suite_path)
        if not suite['inputsUnchanged'] or suite['checks'] != 79 or not suite['baselineFailuresUnchanged']:
            raise ValueError('Broader suite has a new failure or stale inputs')
        aggregate['broaderSuite'] = dict(receipt=suite_path.relative_to(ROOT).as_posix(), checks=suite['checks'],
            failures=suite['failures'], baselineFailuresUnchanged=True)
    aggregate_path.write_text(json.dumps(aggregate, indent=2) + '\n', encoding='utf-8')
    checkpoint_path = ROOT / 'Docs/TerrainRefactor_2026-10-11/Checkpoint.json'
    checkpoint = read(checkpoint_path)
    checkpoint['status'] = 'INCOMPLETE'
    checkpoint['observedOn'] = aggregate['utc']
    checkpoint['receipts']['joinedNativeLakeVerification'] = aggregate_path.relative_to(ROOT).as_posix()
    checkpoint['receipts']['joinedNativeLakePatch'] = patch_path.relative_to(ROOT).as_posix()
    checkpoint['freshVerification']['joinedNativeLake'] = aggregate
    stage_detail = (' Domain version 6 derives bounded native-grid lake wet polygons from the same frozen height-source evaluation, '
        'and exposes that projection to origin/streamed water meshes and placement. Height publication skips duplicate water preparation; '
        'terrain modifiers preserve the source projection to prevent interpolation feedback. Archived versions 1-5 remain pinned. '
        'Fresh checks: 394 runtime/188 Editor sources compile cleanly; 50 detached checks and 14 focused Editor checks pass. '
        'All 3,680 original nominal witnesses remain unchanged. Across 7,140 native contact probes, both joined lake boundaries '
        'are within 2.2mm of their datum; the primary lake has zero failed portal probes, while the opening lake retains seven '
        'coverage/datum failures among 145 portal probes. Final channel union, construction and ordinary traversal remain unverified.')
    for stage in checkpoint['stages']:
        if stage['id'] == 'B' and stage_detail not in stage.get('implemented', ''):
            stage['implemented'] = stage.get('implemented', '') + stage_detail
    limitation = 'Domain-6 joined native lake contact does not certify the entire lake/channel union, final construction or travel. The broader 79-check suite is not current for this patch yet.'
    if suite_path.exists():
        checkpoint['knownLimits'] = [item for item in checkpoint['knownLimits'] if item != limitation]
        checkpoint['receipts']['joinedNativeContractSuite'] = suite_path.relative_to(ROOT).as_posix()
    elif limitation not in checkpoint['knownLimits']:
        checkpoint['knownLimits'].append(limitation)
    checkpoint['nextConcreteStep'] = ('Close the native lake/channel ownership portals against a shared final channel wet boundary. '
        'Preserve fixed lake levels, downstream profiles and the unchanged source-height evaluation. '
        'Then verify final grading/colliders/water meshes and ordinary startup/streaming; generalized regional catchments, falls, coast and stages A-E/all fifteen acceptance gates remain incomplete.')
    checkpoint_path.write_text(json.dumps(checkpoint, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: aggregate[key] for key in ('status', 'domainVersion', 'joinedMeasurements', 'canonicalChanges', 'retainedNominalSamples')}, indent=2))


if __name__ == '__main__':
    main()
