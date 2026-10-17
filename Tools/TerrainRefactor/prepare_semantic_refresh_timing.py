from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,difflib,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2];B=R/'outputs/TerrainRefactor_20261014_StreamingRetest/SemanticRefreshLeafTiming';B.mkdir(exist_ok=False);changes=[]
semantic=R/'Assets/Assets/Scripts/Generated/YQSemanticWorldAuthority.cs';editor=R/'Assets/Assets/Scripts/Generated/Editor/YQWorldPresentationReview.cs'
for path in [semantic,editor]:
 old=path.read_text();n=old;edits=[]
 def replace(a,b):
  global n
  assert n.count(a)==1,(path.name,a[:100],n.count(a));n=n.replace(a,b,1);edits.append((a,b))
 if path==semantic:
  header='''    // note: Lazily register fixed main-thread continuation timing handles; ordinary semantic queries do not initialize native markers.
    private static class ContinuationRefreshTiming
    {
        internal static readonly Unity.Profiling.ProfilerMarker Validation = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.Validation");
        internal static readonly Unity.Profiling.ProfilerMarker Fingerprint = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.Fingerprint");
        internal static readonly Unity.Profiling.ProfilerMarker Overrides = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.Overrides");
        internal static readonly Unity.Profiling.ProfilerMarker Graph = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.Graph");
        internal static readonly Unity.Profiling.ProfilerMarker OpeningCells = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.OpeningCells");
        internal static readonly Unity.Profiling.ProfilerMarker FinalProof = new Unity.Profiling.ProfilerMarker("YQ.Frontier.Semantic.FinalProof");
        internal static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] {
            "YQ.Frontier.Semantic.Validation", "YQ.Frontier.Semantic.Fingerprint", "YQ.Frontier.Semantic.Overrides",
            "YQ.Frontier.Semantic.Graph", "YQ.Frontier.Semantic.OpeningCells", "YQ.Frontier.Semantic.FinalProof"
        });
    }

    public static IReadOnlyList<string> PrimeContinuationRefreshProfilerMarkersForCpuCapture()
    {
        // note: Empty priming runs only at the explicit Editor capture boundary before recording; no world work is performed.
        foreach (string name in ContinuationRefreshTiming.Names)
            using (new Unity.Profiling.ProfilerMarker(name).Auto()) { }
        return ContinuationRefreshTiming.Names;
    }

'''
  anchor='    internal static bool TryPrepareContinuationAuthority(';replace(anchor,header+anchor)
  start=n.index('        var parent = plan?.spatialPlanV2;',n.index(anchor));end=n.index('        var current = plan.semanticAuthority;',start);block=n[start:end];wrapped=block[:block.index('\n')+1]+'        string stagedHash; long stagedRevision;\n        using (ContinuationRefreshTiming.Validation.Auto())\n        {\n'+block[block.index('\n')+1:].replace('        string stagedHash =','        stagedHash =').replace('        long stagedRevision =','        stagedRevision =')+'        }\n\n';replace(block,wrapped)
  replace('        string fingerprint = BuildSourceFingerprint(plan, stagedEnvelope, immutablePrepared);','        string fingerprint = BuildSourceFingerprint(plan, stagedEnvelope, immutablePrepared, true);')
  finalstart=n.index('        if (!ReferenceEquals(plan.spatialPlanV2, parent)',n.index(anchor));finalend=n.index('        detachedAuthority = staged;',finalstart);block=n[finalstart:finalend];replace(block,'        using (ContinuationRefreshTiming.FinalProof.Auto())\n        {\n'+block.replace('BuildSourceFingerprint(plan, stagedEnvelope, immutablePrepared) != fingerprint','BuildSourceFingerprint(plan, stagedEnvelope, immutablePrepared, true) != fingerprint')+'        }\n')
  replace('''        BuildAcceptedOverrides(plan, authority, acceptedPrepared, continuation);
        BuildAcceptedGraph(plan, authority, acceptedPrepared, continuation, detached);''','''        // note: Detached graph construction is timed without adding work to ordinary Ensure/query paths.
        using (detached ? ContinuationRefreshTiming.Overrides.Auto() : default(Unity.Profiling.ProfilerMarker.AutoScope))
            BuildAcceptedOverrides(plan, authority, acceptedPrepared, continuation);
        using (detached ? ContinuationRefreshTiming.Graph.Auto() : default(Unity.Profiling.ProfilerMarker.AutoScope))
            BuildAcceptedGraph(plan, authority, acceptedPrepared, continuation, detached);''')
  start=n.index('        var detachedCells = detached ? new Dictionary');end=n.index('        authority.meaningfulFacts.Add(',start);block=n[start:end];replace(block,'        using (detached ? ContinuationRefreshTiming.OpeningCells.Auto() : default(Unity.Profiling.ProfilerMarker.AutoScope))\n        {\n'+block+'        }\n')
  replace('''        NormalizeSiteOwnership(authority);
        SortAuthority(authority);
        return authority;''','''        using (detached ? ContinuationRefreshTiming.FinalProof.Auto() : default(Unity.Profiling.ProfilerMarker.AutoScope))
        {
            NormalizeSiteOwnership(authority);
            SortAuthority(authority);
        }
        return authority;''')
  replace('''    private static string BuildSourceFingerprint(GeneratedWorldPlanRecord plan,
        GeneratedSpatialContinuationV2Record continuation, YQPreparedSpatialMaterializationV2 acceptedPrepared)
    {''','''    private static string BuildSourceFingerprint(GeneratedWorldPlanRecord plan,
        GeneratedSpatialContinuationV2Record continuation, YQPreparedSpatialMaterializationV2 acceptedPrepared,
        bool measureContinuationRefresh = false)
    {
        // note: Only continuation staging opts into this scope; existing fingerprint callers keep the same deterministic derivation and avoid native marker initialization.
        using var timing = measureContinuationRefresh ? ContinuationRefreshTiming.Fingerprint.Auto() : default(Unity.Profiling.ProfilerMarker.AutoScope);''')
 else:
  replace('        _cpuFrontierMarkerNames = publicationNames.AsReadOnly();','''        // note: Semantic refresh leaves use the same bounded native recorder and missing-handle reporting as the existing publication owners.
        publicationNames.AddRange(YQSemanticWorldAuthority.PrimeContinuationRefreshProfilerMarkersForCpuCapture());
        _cpuFrontierMarkerNames = publicationNames.AsReadOnly();''')
  replace('''        var frontierNames = new HashSet<string>(_cpuFrontierMarkerNames, StringComparer.Ordinal);''','''        // note: The released-gameplay capture already uses all 48 slots. Fund six semantic leaves from inactive title/binding and secondary rendering metrics; startup retains those targets.
        if (!startup) wanted.ExceptWith(new[] { "YQ.Title.SelectSavedProfile", "YQ.Title.Presentation", "YQ.PlayerVisual.InitialBinding",
            "Camera.Render", "RenderLoop.Draw", "Gfx.WaitForPresentOnGfxThread" });
        var frontierNames = new HashSet<string>(_cpuFrontierMarkerNames, StringComparer.Ordinal);''')
 # note: Exact reverse substitution certifies no noninstrumented behavior text changed; executable compilation separately verifies scope/local compatibility.
 undo=n
 for a,b in reversed(edits):assert undo.count(b)==1;undo=undo.replace(b,a,1)
 assert undo==old
 (B/(path.name+'.before')).write_bytes(path.read_bytes());(B/(path.name+'.proposal.cs')).write_text(n)
 changes.append(dict(path=str(path),preimage_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),proposal_sha256=hashlib.sha256((B/(path.name+'.proposal.cs')).read_bytes()).hexdigest(),exact_reverse_equivalence=True,edits=len(edits)))
 patch=''.join(difflib.unified_diff(old.splitlines(True),n.splitlines(True),fromfile='a/'+path.relative_to(R).as_posix(),tofile='b/'+path.relative_to(R).as_posix()))
 # note: Each source's existing newline convention is preserved in its patch; this checkout contains both LF and CRLF owners.
 (B/(path.name+'.patch')).write_bytes(patch.replace('\n','\r\n').encode() if b'\r\n' in path.read_bytes() else patch.encode())
(B/'Proposal.patch').write_bytes(b''.join((B/(p.name+'.patch')).read_bytes() for p in [semantic,editor]));(B/'SourceIdentity.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),files=changes),indent=2))
C=R/'outputs/TerrainRefactor_20261014_StreamingRetest/IntegratedCompile_17'
for scope in ['runtime','editor']:
 tree=ET.parse(C/scope/'Compile.csproj')
 for node in tree.findall('.//Compile'):
  p=Path(node.attrib['Include'])
  if p.resolve() in [semantic.resolve(),editor.resolve()]:node.attrib['Include']=str(B/(p.name+'.proposal.cs'))
 for ref in tree.findall('.//Reference'):
  p=Path(ref.findtext('HintPath'))
  if p.name=='Assembly-CSharp.dll':ref.find('HintPath').text=str(B/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll')
 for ref in tree.findall('.//ProjectReference'):
  if Path(ref.attrib['Include']).parent.name=='runtime':ref.attrib['Include']=str(B/'runtime/Compile.csproj')
 D=B/scope;D.mkdir();tree.write(D/'Compile.csproj',encoding='utf-8')
(B/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>');print(B)
