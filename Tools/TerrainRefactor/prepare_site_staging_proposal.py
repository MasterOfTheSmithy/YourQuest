"""Prepare a structural-only hidden-provider proposal; never edit production or profiles."""
from pathlib import Path
import difflib, hashlib, json
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_SiteLifecycle'
out.mkdir(exist_ok=True)
catalog=root/'Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs'
feature=root/'Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs'
old=catalog.read_text()
helper='''    private static readonly object StagedSiteGeometryIssuer = new object();
    internal sealed class StagedSiteGeometryContext
    {
        private readonly YQCompiledWorldSiteInstance owner;
        private readonly Transform root;
        private readonly GeneratedWorldPlanRecord plan;
        private readonly int siteEpoch, requestEpoch;
        private StagedSiteGeometryContext(YQCompiledWorldSiteInstance site, Transform stagingRoot, GeneratedWorldPlanRecord currentPlan)
        { owner = site; root = stagingRoot; plan = currentPlan; siteEpoch = site.streamExecutionEpoch; requestEpoch = YQServiceLifecycle.RequestEpoch; }
        internal static StagedSiteGeometryContext Capture(YQCompiledWorldSiteInstance site, Transform root, GeneratedWorldPlanRecord plan, object issuer)
        { return ReferenceEquals(issuer, StagedSiteGeometryIssuer) ? new StagedSiteGeometryContext(site, root, plan) : null; }
        internal bool IsCurrent()
        {
            // note: Hidden intent belongs to this exact structural hierarchy and profile lifetime, not any disabled semantic root.
            return owner != null && owner.enabled && owner.gameObject.activeSelf && root != null &&
                !root.gameObject.activeInHierarchy && owner.transform.IsChildOf(root) &&
                Instances.TryGetValue(owner.settlementId ?? string.Empty, out var current) && current == owner &&
                owner.streamExecutionEpoch == siteEpoch && YQServiceLifecycle.RequestEpoch == requestEpoch &&
                plan != null && ReferenceEquals(WorldStateManager.Instance?.State?.generatedWorldPlan, plan);
        }
        internal bool TryGetOwner(out YQCompiledWorldSiteInstance site)
        { site = IsCurrent() ? owner : null; return site != null; }
    }

    internal static bool TryGetContinuationGeometryContext(string locationId, string expectedKitId,
        string expectedCompositionSeed, StagedSiteGeometryContext stagingContext, out YQCompiledWorldSiteInstance owner)
    {
        // note: Reuse every geometry binding gate; only the opaque structural receipt substitutes for public active-hierarchy readiness.
        owner = null;
        if (stagingContext == null || !stagingContext.TryGetOwner(out var captured) ||
            !TryGetContinuationGeometryBinding(locationId, expectedKitId, expectedCompositionSeed, out var current) ||
            current != captured) return false;
        owner = current;
        return true;
    }

    private static IEnumerator GuardStagedSiteExecutionRoutine(IEnumerator routine, Func<bool> isCurrent)
    {
        // note: The existing owned runner drives this wrapper; cancellation is checked before every nested step without exposing staging.
        try
        {
            while (isCurrent() && routine.MoveNext())
            {
                object yielded = routine.Current;
                if (yielded is IEnumerator nested && !(yielded is CustomYieldInstruction))
                    yield return GuardStagedSiteExecutionRoutine(nested, isCurrent);
                else yield return yielded;
            }
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    internal static IEnumerator EnsureSiteGeometryLoadedForStagingRoutine(
        string locationId, Transform stagingRoot, GeneratedWorldPlanRecord plan, Action<bool, StagedSiteGeometryContext> completed)
    {
        // note: Only this hidden structural hierarchy may borrow the detached loader; public actor readiness stays active-only.
        if (!Instances.TryGetValue(locationId ?? string.Empty, out var site) || site == null)
        { completed?.Invoke(false, null); yield break; }
        if (site.isActiveAndEnabled)
        { yield return EnsureSiteLoadedRoutine(locationId, ready => completed?.Invoke(ready, null)); yield break; }
        var context = StagedSiteGeometryContext.Capture(site, stagingRoot, plan, StagedSiteGeometryIssuer);
        while (true)
        {
            // note: Cancellation, replacement, overlay deletion and profile changes never authorize a replacement provider.
            if (!context.IsCurrent() || site.loadRejected)
            { completed?.Invoke(false, null); yield break; }
            if (site.loaded && !site.loading && !site.unloading)
            { completed?.Invoke(true, context); yield break; }
            if (!site.loading && !site.unloading && (ActiveStreamLoader == null || ActiveStreamLoader == site)) break;
            yield return null;
        }
        ActiveStreamLoader = site;
        yield return site.LoadPreparedSiteDetachedRoutine(context.IsCurrent);
        bool ready = context.IsCurrent() && site.loaded && !site.loading && !site.unloading && !site.loadRejected;
        completed?.Invoke(ready, ready ? context : null);
    }

'''
marker='    public static IEnumerator EnsureSiteLoadedRoutine('
assert old.count(marker)==1
candidate=old.replace(marker,helper+marker)
# note: Split binding from active publication so both structural and public APIs consume the exact same identity/geometry checks.
start=old.index('    internal static bool TryGetContinuationGeometryContext(string locationId, string expectedKitId,')
stop=old.index('    private static readonly object ContinuationPopulationIssuer',start)
binding=old[start:stop].replace('internal static bool TryGetContinuationGeometryContext','private static bool TryGetContinuationGeometryBinding',1)
binding=binding.replace('site == null || !site.isActiveAndEnabled || !site.loaded ||','site == null || !site.enabled || !site.loaded ||')
public='''    internal static bool TryGetContinuationGeometryContext(string locationId, string expectedKitId,
        string expectedCompositionSeed, out YQCompiledWorldSiteInstance owner)
    {
        // note: Actor/public geometry contexts still require the same exact provider to be active in the hierarchy.
        if (TryGetContinuationGeometryBinding(locationId, expectedKitId, expectedCompositionSeed, out owner) && owner.isActiveAndEnabled)
            return true;
        owner = null;
        return false;
    }

'''
candidate=candidate.replace(old[start:stop],public+binding)
candidate=candidate.replace('private IEnumerator LoadPreparedSiteDetachedRoutine()','private IEnumerator LoadPreparedSiteDetachedRoutine(Func<bool> isCurrent = null)')
start=candidate.index('    private IEnumerator LoadPreparedSiteDetachedRoutine(')
stop=candidate.index('    private void ReportSiteLoadingProgress',start)
block=candidate[start:stop]
block=block.replace('LoadPreparedSiteRoutine(), ReportStreamExecutionFailure);','isCurrent != null ? GuardStagedSiteExecutionRoutine(LoadPreparedSiteRoutine(), isCurrent) : LoadPreparedSiteRoutine(), ReportStreamExecutionFailure);')
block=block.replace('completedNormally = true;','completedNormally = isCurrent == null || isCurrent();')
candidate=candidate[:start]+block+candidate[stop:]
# note: Diagnostics expose ownership facts without relaxing the existing public request gate.
diagnostic='''    private static string DescribeUnavailableSite(string locationId, YQCompiledWorldSiteInstance site, int expectedEpoch)
    {
        // note: Capture the failed request's actual ownership facts without revealing hidden geometry or changing admission.
        bool registered = Instances.TryGetValue(locationId ?? string.Empty, out var current);
        var text = new System.Text.StringBuilder(256);
        text.Append("registered=").Append(registered).Append(",sameOwner=").Append(current == site)
            .Append(",expectedEpoch=").Append(expectedEpoch).Append(",actualEpoch=").Append(site != null ? site.streamExecutionEpoch : -1)
            .Append(",requestEpoch=").Append(YQServiceLifecycle.RequestEpoch)
            .Append(",profile=").Append(YQProfileSaveSystem.Instance != null ? YQProfileSaveSystem.Instance.ActiveProfileId : "<none>");
        if (site != null)
        {
            text.Append(",enabled=").Append(site.enabled).Append(",activeHierarchy=").Append(site.gameObject.activeInHierarchy)
                .Append(",loading=").Append(site.loading).Append(",unloading=").Append(site.unloading)
                .Append(",rejected=").Append(site.loadRejected).Append(",cleanup=").Append(site.interruptedStreamCleanupRequired);
            int count = 0;
            for (Transform parent = site.transform; parent != null && count < 6; parent = parent.parent, count++)
            {
                string name = parent.name ?? string.Empty;
                text.Append(",parent[").Append(count).Append("]=").Append(name.Length > 96 ? name.Substring(0, 96) : name)
                    .Append(":").Append(parent.gameObject.activeSelf);
            }
        }
        return text.ToString();
    }

'''
candidate=candidate.replace(marker,diagnostic+marker)
needle='''"[WORLDGEN ERROR] Required site became unavailable while awaiting materialization. " +
                    "Location=" + locationId + ", pass=settlement_instantiation.");'''
assert candidate.count(needle)==1
candidate=candidate.replace(needle,'''"[WORLDGEN ERROR] Required site became unavailable while awaiting materialization. " +
                    "Location=" + locationId + ", pass=settlement_instantiation, " + DescribeUnavailableSite(locationId, site, requestEpoch));''')
f=feature.read_text()
needle='''yield return YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(
                    existingSiteLocationId,
                    ready => existingSiteLoaded = ready);'''
assert f.count(needle)==1
fc=f.replace('bool existingSiteLoaded = false;\n                '+needle,'bool existingSiteLoaded = false;\n                YQCompiledWorldSiteInstance.StagedSiteGeometryContext stagedGeometry = null;\n                '+'''yield return YQCompiledWorldSiteInstance.EnsureSiteGeometryLoadedForStagingRoutine(
                    existingSiteLocationId, parent, plan,
                    (ready, context) => { existingSiteLoaded = ready; stagedGeometry = context; });''')
assert fc!=f
fc=fc.replace('TryGetAcceptedContinuationLoadedProvider(plan, site, out _, out _);','TryGetAcceptedContinuationLoadedProvider(plan, site, out _, out _, stagedGeometry);',1)
fc=fc.replace('out YQCompiledWorldSiteInstance provider, out string failure)\n    {\n        provider = null;\n        if (!TryResolveAcceptedContinuationSiteContext',
    'out YQCompiledWorldSiteInstance provider, out string failure,\n        YQCompiledWorldSiteInstance.StagedSiteGeometryContext stagedGeometry = null)\n    {\n        provider = null;\n        if (!TryResolveAcceptedContinuationSiteContext',1)
needle='''if (!YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                ContinuationKitId(location), location.compositionSeed, out provider) || savedLayout == null ||'''
assert fc.count(needle)==1
fc=fc.replace(needle,'''bool geometryReady = stagedGeometry != null
            ? YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                ContinuationKitId(location), location.compositionSeed, stagedGeometry, out provider)
            : YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                ContinuationKitId(location), location.compositionSeed, out provider);
        if (!geometryReady || savedLayout == null ||''')
(out/'StagingHelper.proposal.cs').write_text(helper)
(out/'GeometryBinding.proposal.cs').write_text(public+binding)
(out/'Catalog.proposal.cs').write_text(candidate)
(out/'Feature.proposal.cs').write_text(fc)
(out/'UnavailableDiagnostic.proposal.cs').write_text(diagnostic)
patch=''
for path,a,b in ((catalog,old,candidate),(feature,f,fc)):
    rel=path.relative_to(root).as_posix()
    patch+=''.join(difflib.unified_diff(a.splitlines(True),b.splitlines(True),fromfile='a/'+rel,tofile='b/'+rel))
(out/'SiteStaging.proposal.patch').write_text(patch)
(out/'Proposal_Source_Identity.json').write_text(json.dumps({str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (catalog,feature)},indent=2))
print(out/'SiteStaging.proposal.patch')
