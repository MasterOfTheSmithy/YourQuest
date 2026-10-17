"""Create an exact-preimage timing proposal in fresh outputs, without touching Assets."""
from pathlib import Path
import argparse, difflib, hashlib, json
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
out = (ROOT / args.out).resolve()
if out.exists() or not out.is_relative_to(ROOT / 'outputs'):
    parser.error('Use a fresh workspace outputs directory')
out.mkdir(parents=True)
paths = ['Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs',
         'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs',
         'Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs',
         'Assets/Assets/Scripts/Generated/Editor/YQWorldPresentationReview.cs']

helper = '''    // note: Fixed Editor-only scopes attribute repeated publication gates without changing their execution or retaining frame histories.
    internal static class FrontierPublicationDiagnostics
    {
        internal enum Operation
        {
            TokenIdentity, TokenDecode, TokenInputs, SiteFunctions, PopulationBindings,
            ProfileDiskProof, WorldPrepare, WorldEquality, AuxiliaryCapture,
            TerrainFingerprint, RevisionStage, ManifestPointer, WorkingProjection
        }

        internal readonly struct TimingScope : IDisposable
        {
#if UNITY_EDITOR
            private readonly Unity.Profiling.ProfilerMarker.AutoScope scope;
            internal TimingScope(Operation operation) => scope = MarkerHolder.Markers[(int)operation].Auto();
#else
            internal TimingScope(Operation operation) { }
#endif
            public void Dispose()
            {
#if UNITY_EDITOR
                scope.Dispose();
#endif
            }
        }

        internal static TimingScope Begin(Operation operation) => new TimingScope(operation);
#if UNITY_EDITOR
        private static class MarkerHolder
        {
            internal static readonly string[] Names = {
                "YQ.Frontier.TokenIdentity", "YQ.Frontier.TokenDecode", "YQ.Frontier.TokenInputs",
                "YQ.Frontier.SiteFunctions", "YQ.Frontier.PopulationBindings", "YQ.Frontier.ProfileDiskProof",
                "YQ.Frontier.WorldPrepare", "YQ.Frontier.WorldEquality", "YQ.Frontier.AuxiliaryCapture",
                "YQ.Frontier.TerrainFingerprint", "YQ.Frontier.RevisionStage", "YQ.Frontier.ManifestPointer",
                "YQ.Frontier.WorkingProjection"
            };
            internal static readonly Unity.Profiling.ProfilerMarker[] Markers = CreateMarkers();
            private static Unity.Profiling.ProfilerMarker[] CreateMarkers()
            {
                var markers = new Unity.Profiling.ProfilerMarker[Names.Length];
                for (int index = 0; index < markers.Length; index++) markers[index] = new Unity.Profiling.ProfilerMarker(Names[index]);
                return markers;
            }
        }

        internal static IReadOnlyList<string> Prime()
        {
            // note: Register all fixed handles before recorder discovery; these empty samples occur before recording starts.
            for (int index = 0; index < MarkerHolder.Names.Length; index++)
                using (Begin((Operation)index)) { }
            return Array.AsReadOnly(MarkerHolder.Names);
        }
#endif
    }

#if UNITY_EDITOR
    // note: The separate Editor assembly may prime diagnostics only; this API grants no publication or gameplay authority.
    public static IReadOnlyList<string> PrimeFrontierPublicationProfilerMarkersForCpuCapture()
        => FrontierPublicationDiagnostics.Prime();
#endif

'''

patch = ''
receipt = {'utc': datetime.now(timezone.utc).isoformat(), 'status': 'OUTPUT_ONLY_PROPOSAL',
           'sources': {}, 'reverse_edits_restore_exact_source': True, 'asset_edits': False}
for relative in paths:
    path = ROOT / relative
    before = path.read_text(encoding='utf-8-sig')
    after = before
    edits = []
    def replace(old, new):
        global after
        assert after.count(old) == 1, (relative, old[:120], after.count(old))
        after = after.replace(old, new, 1)
        edits.append((old, new))
    def scope(op):
        return f'using (FrontierTiming.Begin(FrontierTiming.Operation.{op}))'
    def method_scope(signature, op):
        # note: A using declaration leaves every existing branch and evaluation order intact and disposes on all exits.
        start = after.index(signature)
        brace = after.index('{', start)
        prefix = after[start:brace + 1]
        replace(prefix, prefix + f'\n        // note: Measure this existing owner without changing its callbacks or rejection gates.\n        using var publicationTiming = FrontierTiming.Begin(FrontierTiming.Operation.{op});')
    if path.name != 'YQWorldPresentationReview.cs':
        replace('using UnityEngine;\n', 'using UnityEngine;\nusing FrontierTiming = YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics;\n')
    if path.name == 'YQGeneratedWorldRuntimeBuilder.cs':
        replace('    internal static T MeasureStartupOperation<T>(string name, Func<T> action)\n', helper + '    internal static T MeasureStartupOperation<T>(string name, Func<T> action)\n')
        method_scope('        private bool TryValidatePublicationOwner(WorldState owner, out string failure)', 'TokenIdentity')
        replace('                var accepted = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(acceptedJson, FrontierConstructionJsonSettings);',
                '                GeneratedSpatialContinuationV2Record accepted;\n                ' + scope('TokenDecode') + '\n                    accepted = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(acceptedJson, FrontierConstructionJsonSettings);')
        old = '''                string parentJson = JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);
                // note: Reuse only this token's immutable numeric result for byte-identical compiler inputs. Deep source changes and current provider/population gates still reject at every publication boundary.
                string canonicalInputsJson = JsonConvert.SerializeObject(new {
                    plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
                }, FrontierConstructionJsonSettings) + "|" + parentJson;'''
        new = '''                string parentJson, canonicalInputsJson;
                // note: Measure the complete existing compiler-input proof, including parent serialization, without caching mutable sources.
                ''' + scope('TokenInputs') + '''
                {
                    parentJson = JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);
                    canonicalInputsJson = JsonConvert.SerializeObject(new {
                        plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
                    }, FrontierConstructionJsonSettings) + "|" + parentJson;
                }'''
        replace(old, new)
        old = '''                foreach (var location in accepted.locations)
                    if (!YQCompiledWorldSiteInstance.TryValidateAcceptedContinuationFunctions(location, out failure) ||
                        !YQGeneratedWorldPopulation.TryValidateContinuationPopulationBindings(plan, world, location,
                            YQRuntimeWorldAssetRegistry.Instance, out failure)) return false;'''
        new = '''                foreach (var location in accepted.locations)
                {
                    // note: Keep the original short-circuit order: population bindings run only after reviewed site functions accept.
                    ''' + scope('SiteFunctions') + '''
                        if (!YQCompiledWorldSiteInstance.TryValidateAcceptedContinuationFunctions(location, out failure)) return false;
                    ''' + scope('PopulationBindings') + '''
                        if (!YQGeneratedWorldPopulation.TryValidateContinuationPopulationBindings(plan, world, location,
                            YQRuntimeWorldAssetRegistry.Instance, out failure)) return false;
                }'''
        replace(old, new)
        method_scope('        internal bool HasPreparedCandidate(GeneratedWorldPlanRecord candidate)', 'TokenInputs')
        old = '''        string expectedFingerprint = hasCurrentWorldIdentity
            ? MeasureStartupOperation("Final.TerrainFingerprint", () => YQStateContract.Sha256Hex(BuildVisualSignature(plan)))
            : string.Empty;'''
        new = '''        string expectedFingerprint;
        // note: Retain the existing maximum-step evidence while exposing this provider cost to the native frame recorder.
        ''' + scope('TerrainFingerprint') + '''
            expectedFingerprint = hasCurrentWorldIdentity
                ? MeasureStartupOperation("Final.TerrainFingerprint", () => YQStateContract.Sha256Hex(BuildVisualSignature(plan)))
                : string.Empty;'''
        replace(old, new)
    elif path.name == 'YQProfileSaveSystem.cs':
        method_scope('    private bool TryRequireContinuationOwner(out PlayerStateManager psm, out WorldStateManager wsm,', 'ProfileDiskProof')
        method_scope('    private bool TryCaptureContinuationAuxiliary(out Dictionary<string, Func<string>> providers,', 'AuxiliaryCapture')
        old = '''            if (!YQProfileCommitStore.TryStageContinuationCommit(folder, data.profileId,'''
        replace(old, '            // note: Separate immutable staging from repeated final validation and atomic pointer publication.\n            ' + scope('RevisionStage') + '\n' + old)
        old = '''            if (!SaveManifest(next)) { failure = "Continuation commit pointer publication failed: " + LastFailure; receipt.failure = failure; return false; }'''
        replace(old, '            ' + scope('ManifestPointer') + '\n' + old)
        old = '''            if (!YQProfileCommitStore.TryRefreshPublishedContinuationProjections(folder,'''
        replace(old, '            // note: Post-pointer projection refresh retains its truthful published-receipt and recovery behavior.\n            ' + scope('WorkingProjection') + '\n' + old)
    elif path.name == 'WorldStateManager.cs':
        method_scope('    internal bool TryPrepareContinuationSnapshot(WorldState expectedWorld, PlayerState pairedPlayer,', 'WorldPrepare')
        method_scope('    internal bool TryValidateContinuationSnapshot(PreparedContinuationSnapshot snapshot, out string failure)', 'WorldEquality')
    else:
        replace('    private static bool _cpuStartupCapture;\n', '    private static bool _cpuStartupCapture;\n    private static IReadOnlyList<string> _cpuFrontierMarkerNames = Array.Empty<string>();\n')
        replace('        YQSemanticChunkRuntimeVerification.PrimeProfilerMarkerForCpuCapture();',
                '        YQSemanticChunkRuntimeVerification.PrimeProfilerMarkerForCpuCapture();\n        // note: Prime fixed inner publication handles before discovery; no gameplay work or recorded samples occur here.\n        _cpuFrontierMarkerNames = YQGeneratedWorldRuntimeBuilder.PrimeFrontierPublicationProfilerMarkersForCpuCapture();')
        old = '        int Priority(string name) => name.StartsWith("YQ.Title.", StringComparison.Ordinal) ? 0 : wanted.Contains(name) ? 1 : 2;'
        new = '''        // note: Reserve recorder capacity for inner attribution; report any missing fixed handles rather than silently losing a leaf scope.
        var frontierNames = new HashSet<string>(_cpuFrontierMarkerNames, StringComparer.Ordinal);
        foreach (string name in frontierNames) wanted.Add(name);
        int Priority(string name) => frontierNames.Contains(name) ? 0 : name.StartsWith("YQ.Title.", StringComparison.Ordinal) ? 1 : wanted.Contains(name) ? 2 : 3;'''
        replace(old, new)
        old = '        report.AppendLine("Focus at start/end: " + _cpuFocusedAtStart + "/" + Application.isFocused + "; ending timeScale: " + Time.timeScale);'
        replace(old, old + '''
        // note: Frame Samples counts retained frames; Calls sums native invocation counts, including repeated validation within one hitch frame.
        report.AppendLine("Calls: sum of ProfilerRecorderSample.Count; phase/leaf timings overlap and must not be added. Empty priming scopes ran before recording.");
        foreach (string name in _cpuFrontierMarkerNames)
            if (!CpuRecorderNames.Contains(name)) report.AppendLine("Missing inner publication recorder: " + name);''')
        replace('            report.AppendLine("\\n| Metric | Unit | Samples | Mean | Maximum |\\n| --- | --- | ---: | ---: | ---: |");',
                '            report.AppendLine("\\n| Metric | Unit | Samples | Calls | Mean | Maximum |\\n| --- | --- | ---: | ---: | ---: | ---: |");')
        replace('                double total = 0d, maximum = 0d;\n                foreach (var sample in samples) { total += sample.Value; maximum = Math.Max(maximum, sample.Value); }',
                '                double total = 0d, maximum = 0d;\n                long calls = 0;\n                foreach (var sample in samples) { total += sample.Value; maximum = Math.Max(maximum, sample.Value); calls += sample.Count; }')
        replace('"| {0} | {1} | {2} | {3:F3} | {4:F3} |", CpuRecorderNames[metric], time ? "ms" : CpuRecorderUnits[metric],\n                    samples.Count, samples.Count > 0 ? total / samples.Count / divisor : 0d, maximum / divisor));',
                '"| {0} | {1} | {2} | {3} | {4:F3} | {5:F3} |", CpuRecorderNames[metric], time ? "ms" : CpuRecorderUnits[metric],\n                    samples.Count, calls, samples.Count > 0 ? total / samples.Count / divisor : 0d, maximum / divisor));')
    restored = after
    for old, new in reversed(edits):
        assert restored.count(new) == 1
        restored = restored.replace(new, old, 1)
    assert restored == before
    before_file = out / (path.name + '.before')
    proposal_file = out / (path.name + '.proposal.cs')
    before_file.write_bytes(path.read_bytes())
    proposal_file.write_text(after, encoding='utf-8', newline='\n')
    receipt['sources'][relative] = {'before_sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'proposal_sha256': hashlib.sha256(proposal_file.read_bytes()).hexdigest(), 'edits': len(edits)}
    patch += ''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='a/' + relative, tofile='b/' + relative))
(out / 'Proposal.patch').write_text(patch, encoding='utf-8', newline='\n')
(out / 'Source_Identity.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
print('Output-only timing proposal: ' + str(out))
