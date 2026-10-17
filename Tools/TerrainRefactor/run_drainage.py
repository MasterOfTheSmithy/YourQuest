"""Run synthetic drainage records through a freshly compiled production assembly; never launches Unity."""
import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--compile', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--baseline', action='store_true')
    parser.add_argument('--beta-repro', action='store_true')
    parser.add_argument('--preimages', type=Path)
    parser.add_argument('--unity-artifact', type=Path)
    parser.add_argument('--quick', action='store_true', help='Six generator regression seeds during iteration; the default runs all twenty')
    parser.add_argument('--native-reaches', action='store_true', help='Focused production connected-wet projection checks with analytical input fields')
    parser.add_argument('--query-orders-from', type=Path, help='Replay twenty saved generated fixtures through source query-order checks without regenerating worlds')
    args = parser.parse_args()
    compiled = args.compile.resolve()
    receipt = json.loads((compiled / 'receipt.json').read_text(encoding='utf-8'))
    if receipt['status'] != 'PASS' or not receipt['inputs_unchanged_at_completion']:
        raise ValueError('A successful unchanged-input detached compilation receipt is required')
    output = args.out.resolve()
    # note: Evidence is written only to a new owned directory, never to sources, Unity caches or previous receipts.
    evidence_roots = [ROOT / 'outputs/TerrainRefactor_20261011', ROOT / 'outputs/TerrainRefactor_20261012']
    if output.exists() or not any(output.is_relative_to(path.resolve()) for path in evidence_roots):
        raise ValueError('Choose a fresh child of outputs/TerrainRefactor_20261011 or outputs/TerrainRefactor_20261012')
    runtime = compiled / 'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
    if args.native_reaches and (args.beta_repro or args.baseline or args.quick or args.unity_artifact or args.preimages or args.query_orders_from):
        raise ValueError('Native reach checks are a separate fixed-field verification boundary')
    if args.query_orders_from and (args.beta_repro or args.baseline or args.quick or args.unity_artifact or args.preimages):
        raise ValueError('Query-order replay is a separate saved-input verification boundary')
    source = ROOT / ('Tools/TerrainRefactor/NativeReachChecks.cs' if args.native_reaches else
                     'Tools/TerrainRefactor/BetaFixtureProbe.cs' if args.beta_repro else 'Tools/TerrainRefactor/DrainageChecks.cs')
    tracked = {str(source): digest(source), str(runtime): digest(runtime),
               str(compiled / 'receipt.json'): digest(compiled / 'receipt.json'), str(Path(__file__)): digest(Path(__file__))}
    # note: Historical accepted fixtures are explicit read-only inputs, independent of the fresh result directory; pin their bytes just like production sources.
    archive_root = ROOT / 'outputs/TerrainRefactor_20261011'
    if not (args.native_reaches or args.beta_repro or args.baseline or args.query_orders_from):
        archives = [archive_root / 'persistence_regressions' / ('generated-' + seed + '.json')
                    for seed in ('semantic-v2-318', '33b9e87d', 'watershed-alpha', 'watershed-beta')]
        archives.append(archive_root / '20261011_101211_592_unity_basins/new-draft.json')
        for path in archives:
            tracked[str(path)] = digest(path)
    if args.query_orders_from:
        replay = args.query_orders_from.resolve()
        if not any(replay.is_relative_to(path.resolve()) for path in evidence_roots):
            raise ValueError('Replay inputs must be owned terrain evidence')
        replay_identity = replay / 'identity.json'
        origin_identity = json.loads(replay_identity.read_text(encoding='utf-8'))
        if origin_identity['compiler_exit'] != 0 or not origin_identity['inputs_unchanged']:
            raise ValueError('Replay requires source-bound generated artifacts from a successful harness compilation')
        fixtures = sorted(replay.glob('generated-*.json'))
        expected_names = {'generated-' + seed + '.json' for seed in
                          ['semantic-v2-318', '33b9e87d', 'watershed-alpha', 'watershed-beta'] +
                          ['basin-seed-' + str(index).zfill(2) for index in range(16)]}
        if {path.name for path in fixtures} != expected_names:
            raise ValueError('Replay requires the exact twenty fixed seed artifacts')
        for path in [replay_identity, *fixtures]:
            tracked[str(path)] = digest(path)
    if args.unity_artifact:
        artifact = args.unity_artifact.resolve()
        unity_receipt_path = artifact.parent / 'Receipt.json'
        if args.baseline or args.beta_repro or artifact.name != 'new-draft.json' or not artifact.is_relative_to((ROOT / 'outputs/TerrainRefactor_20261011').resolve()):
            raise ValueError('Unity artifact must be an owned read-only basin verification draft')
        unity_receipt = json.loads(unity_receipt_path.read_text(encoding='utf-8'))
        if not unity_receipt['passed']:
            raise ValueError('Unity artifact requires a passing Editor receipt')
        # note: Unity emits portable slash paths while the Windows compile receipt uses native separators; compare the same relative source identity.
        compiled_sources = {relative.replace('\\', '/'): expected for relative, expected in receipt['source_sha256'].items()}
        for relative, expected in unity_receipt['sourceSha256'].items():
            if digest(ROOT / relative) != expected or compiled_sources.get(relative) != expected:
                raise ValueError('Unity/detached source identity mismatch: ' + relative)
        tracked[str(artifact)] = digest(artifact)
        tracked[str(unity_receipt_path)] = digest(unity_receipt_path)
    # note: Production source hashes are checked against the compile receipt before executing the harness.
    preserved = {'YQSpatialBlueprintV2Validation.cs', 'YQWorldGenerationV2Contracts.cs', 'YQGeneratedWorldRuntimeBuilder.cs'}
    if args.preimages and not args.beta_repro:
        raise ValueError('Captured preimages are allowed only for the historical beta reproduction')
    for relative, expected in receipt['source_sha256'].items():
        path = ROOT / relative
        # note: A historical reproduction may use only captured preimages of this refactor's three runtime files; every other compiled source must still match.
        if args.preimages and path.name in preserved:
            path = args.preimages.resolve() / path.name
        if digest(path) != expected:
            raise ValueError('Compiled source identity mismatch: ' + str(path))
        tracked[str(path)] = expected
    output.mkdir(parents=True)
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(project, 'PropertyGroup')
    for key, value in dict(TargetFramework='net10.0', OutputType='Exe', EnableDefaultCompileItems='false',
                           GenerateAssemblyInfo='false', AssemblyName='DrainageChecks').items():
        ET.SubElement(properties, key).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'Compile', Include=str(source))
    reference = ET.SubElement(items, 'Reference', Include='Assembly-CSharp')
    ET.SubElement(reference, 'HintPath').text = str(runtime)
    if args.beta_repro:
        editor = compiled / 'editor/bin/Debug/netstandard2.1/Assembly-CSharp-Editor.dll'
        reference = ET.SubElement(items, 'Reference', Include='Assembly-CSharp-Editor')
        ET.SubElement(reference, 'HintPath').text = str(editor)
        tracked[str(editor)] = digest(editor)
    # note: Use installed Unity managed references without replacing Mathf/Vector math or touching native terrain.
    for reference in ET.parse(compiled / 'runtime/Compile.csproj').findall('.//Reference'):
        path = Path(reference.findtext('HintPath'))
        if path.name.startswith('UnityEngine.') or 'Newtonsoft' in path.name:
            item = ET.SubElement(items, 'Reference', Include=reference.attrib['Include'])
            ET.SubElement(item, 'HintPath').text = str(path)
            tracked[str(path)] = digest(path)
    project_path = output / 'DrainageChecks.csproj'
    ET.ElementTree(project).write(project_path, encoding='utf-8')
    (output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
    build = subprocess.run(['dotnet', 'build', str(project_path), '--nologo', '-v:q'], cwd=ROOT, capture_output=True, text=True)
    (output / 'harness-compile.log').write_text(build.stdout + build.stderr, encoding='utf-8')
    exit_code = build.returncode
    if exit_code == 0:
        command = ['dotnet', str(output / 'bin/Debug/net10.0/DrainageChecks.dll'), str(output)]
        if args.beta_repro:
            command.append(str(editor))
        elif args.baseline:
            command.append('--baseline')
        elif args.unity_artifact:
            command.extend(['--unity-artifact', str(artifact)])
        elif args.query_orders_from:
            command.extend(['--query-orders-from', str(replay)])
        if args.quick:
            if args.baseline or args.beta_repro:
                raise ValueError('Quick regression selection is for current generator checks only')
            command.append('--quick')
        if not (args.native_reaches or args.beta_repro or args.baseline or args.query_orders_from):
            command.extend(['--archive-root', str(archive_root)])
        run = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        exit_code = run.returncode
        (output / 'run.log').write_text(run.stdout + run.stderr, encoding='utf-8')
        print(run.stdout + run.stderr)
    else:
        print(build.stdout + build.stderr)
    changed = [path for path, expected in tracked.items() if digest(Path(path)) != expected]
    identity = dict(utc=datetime.now(timezone.utc).isoformat(), compiler_exit=build.returncode, run_exit=exit_code,
                    runtime_sha256=tracked[str(runtime)], compiled_source_receipt=str(compiled / 'receipt.json'),
                    inputs_unchanged=not changed, changed_inputs=changed, input_sha256=tracked,
                    geometry_source_boundary=('YQNativeWaterJoinPatchV2 in YQConnectedBasinDomainV2.cs; analytical tile inputs; '
                                              'StartupPreparation.CopySavedPlan in YQSpatialMaterializationV2.cs') if args.native_reaches else
                                             ('YQSpatialBlueprintCompilerV2, YQSpatialBlueprintValidatorV2, YQSpatialBlueprintHasherV2, '
                                              'YQPreparedConnectedBasinV2, YQSpatialBlueprintTerrainSamplerV2.TryPrepareBlueprint/Sample, '
                                              'YQContinuousWorldFeatureAuthority.AcceptedWaterTerrainEnvelope; synthetic/archived inputs'),
                    evidence='DETACHED_SOURCE_QUERY_ORDER_REPLAY_NOT_UNITY_RUNTIME' if args.query_orders_from else
                             'DETACHED_COMPILED_PRODUCTION_NATIVE_WET_SELECTION_NOT_UNITY_RUNTIME' if args.native_reaches else
                             'DETACHED_COMPILED_PRODUCTION_SYNTHETIC_DRAINAGE_NOT_UNITY_RUNTIME')
    (output / 'identity.json').write_text(json.dumps(identity, indent=2), encoding='utf-8')
    return exit_code if exit_code else (2 if changed else 0)


if __name__ == '__main__':
    raise SystemExit(main())
