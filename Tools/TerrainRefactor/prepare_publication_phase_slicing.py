"""Prepare a phase-slicing publication patch outside Assets, preserving atomic final commit/install."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, difflib, hashlib, json

ROOT = Path(__file__).resolve().parents[2]
ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument('--out', type=Path, required=True)
ap.add_argument('--single-yield', action='store_true', help='Keep player capture and final commit/install contiguous; excludes the two-yield experiment')
args = ap.parse_args()
out = (ROOT / args.out).resolve()
if out.exists() or not out.is_relative_to(ROOT / 'outputs'): ap.error('Use a fresh outputs directory')
out.mkdir(parents=True)
paths = ['Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs',
         'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs']
if args.single_yield: paths = paths[:1]
patch = ''
identity = {'utc': datetime.now(timezone.utc).isoformat(), 'status': 'OUTPUT_ONLY_PROPOSAL', 'sources': {}, 'asset_edits': False}
for relative in paths:
    path = ROOT / relative
    before = path.read_text(encoding='utf-8-sig')
    after = before
    def replace(old, new):
        global after
        assert after.count(old) == 1, (relative, old[:110], after.count(old))
        after = after.replace(old, new, 1)
    if path.name == 'YQProfileSaveSystem.cs':
        old = '    private ContinuationPublicationData _preparedContinuationData;\n'
        new = old + '''
    internal void RetirePreparedContinuationPublication(PreparedContinuationPublication publication)
    {
        // note: Cancelling a staged frontier may retire only its exact private marker; superseded or consumed operations cannot clear another preparation.
        if (publication == null || !ReferenceEquals(publication, _preparedContinuationPublication)) return;
        _preparedContinuationPublication = null;
        _preparedContinuationData = null;
    }
'''
        replace(old, new)
    else:
        replace('    private string _frontierConstructionFailure;\n', '''    private string _frontierConstructionFailure;
    // note: The existing owned iterator stack carries publication across two frame boundaries; these flags describe its one completion, not another authority.
    private bool _frontierPublicationStarted, _frontierPublicationCompleted, _frontierPublicationPublished;
''')
        if args.single_yield:
            after = after.replace('publication across two frame boundaries', 'publication across one frame boundary', 1)
        reset = '_frontierConstructionAccepted = null; _frontierConstructionFailure = string.Empty;'
        assert after.count(reset) == 2
        after = after.replace(reset, reset + '\n            _frontierPublicationStarted = _frontierPublicationCompleted = _frontierPublicationPublished = false;')
        start = after.index('    private bool TryPublishAndInstallContinuation(')
        end = after.index('    private void RetireContinuationPrefetches(', start)
        original = after[start:end]
        final_start = original.index('        using (new FrontierPublicationTimingScope(this, 2))')
        atomic_body = original[final_start:]
        routine = '''    private IEnumerator PublishAndInstallContinuationRoutine(
        YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction, Action<bool, string> completed)
    {
        bool published = false;
        string failure = "Frontier publication was cancelled before its durable pointer.";
        YQProfileSaveSystem profile = null;
        YQProfileSaveSystem.PreparedContinuationPublication publication = null;
        bool Current() => ReferenceEquals(_frontierConstructionAccepted, construction) && IsFrontierConstructionWorkCurrent();
        try
        {
            if (!Current()) yield break;
            PreparedContinuationRefresh refresh;
            using (new FrontierPublicationTimingScope(this, 0))
            using (new ProfilerMarker("YQ.Frontier.PrepareRefresh").Auto())
                if (!TryPrepareContinuationRefresh(construction, out refresh, out failure)) yield break;
            profile = YQProfileSaveSystem.Instance;
            if (profile == null) { failure = "Continuation profile publication owner is unavailable."; yield break; }
            if (!Current()) { failure = "Frontier publication ownership changed after refresh preparation."; yield break; }
            // note: Return to current/neighbor terrain work after private refresh preparation; no accepted pointer or runtime owner has changed.
            yield return null;
            if (!Current() || !ReferenceEquals(YQProfileSaveSystem.Instance, profile) || !profile.isActiveAndEnabled)
            { failure = "Frontier publication ownership changed before profile preparation."; yield break; }
            using (new FrontierPublicationTimingScope(this, 1))
            using (new ProfilerMarker("YQ.Frontier.PrepareProfile").Auto())
                if (!profile.TryPrepareContinuationPublication(construction, out publication, out failure)) yield break;
            if (!Current()) { failure = "Frontier publication ownership changed after profile preparation."; yield break; }
            // note: The prepared profile remains private and its own transaction flag is released; exact final proofs still reject changes during this gap.
            yield return null;
            if (!Current() || !ReferenceEquals(YQProfileSaveSystem.Instance, profile) || !profile.isActiveAndEnabled)
            { failure = "Frontier publication ownership changed before final publication."; yield break; }
            // note: Never yield between final source/provider/disk proofs, durable pointer, live apply and the existing runtime install.
            published = TryPublishAndInstallPreparedContinuation(profile, publication, refresh, out failure);
        }
        finally
        {
            if (profile != null) profile.RetirePreparedContinuationPublication(publication);
            completed?.Invoke(published, failure);
        }
    }

    private bool TryPublishAndInstallPreparedContinuation(YQProfileSaveSystem profile,
        YQProfileSaveSystem.PreparedContinuationPublication publication, PreparedContinuationRefresh refresh, out string failure)
    {
''' + atomic_body
        if args.single_yield:
            old_gap = '''            if (!Current()) { failure = "Frontier publication ownership changed after profile preparation."; yield break; }
            // note: The prepared profile remains private and its own transaction flag is released; exact final proofs still reject changes during this gap.
            yield return null;
            if (!Current() || !ReferenceEquals(YQProfileSaveSystem.Instance, profile) || !profile.isActiveAndEnabled)
            { failure = "Frontier publication ownership changed before final publication."; yield break; }'''
            new_gap = '''            // note: Keep player/world capture and final commit/install in this same step so normal frame updates cannot continually stale the exact source proof.
            if (!Current() || !ReferenceEquals(YQProfileSaveSystem.Instance, profile) || !profile.isActiveAndEnabled)
            { failure = "Frontier publication ownership changed during profile preparation."; yield break; }'''
            assert routine.count(old_gap) == 1
            routine = routine.replace(old_gap, new_gap, 1)
            routine = routine.replace('            if (profile != null) profile.RetirePreparedContinuationPublication(publication);\n', '')
        replace(original, routine)
        old = '''            if (_frontierConstructionWork.Count == 0 && IsFrontierConstructionWorkCurrent())
            {
                bool published = _frontierConstructionAccepted != null &&
                    TryPublishAndInstallContinuation(_frontierConstructionAccepted, out _frontierConstructionFailure);
                // note: Both preparation and final publication rejection leave the existing cooldown after completion; recheck ownership because publication may change it.
                if (!published && IsFrontierConstructionWorkCurrent())
                    _nextFrontierConstructionAt = Mathf.Max(_nextFrontierConstructionAt, Time.unscaledTime + FrontierConstructionRetrySeconds);
                if (published) Debug.Log("[YQSemanticChunkStreamer] Accepted frontier location published and admitted without replacing live terrain.");
                else if (!string.IsNullOrWhiteSpace(_frontierConstructionFailure))
                    Debug.LogWarning("[YQSemanticChunkStreamer] Frontier construction rejected: " + _frontierConstructionFailure);
                CancelFrontierConstructionWork();
            }'''
        new = '''            if (_frontierConstructionWork.Count == 0 && (IsFrontierConstructionWorkCurrent() || _frontierPublicationCompleted))
            {
                if (!_frontierPublicationStarted && _frontierConstructionAccepted != null && IsFrontierConstructionWorkCurrent())
                {
                    // note: Dispatch once into the existing cancellation-owned stack; each null yield resumes on a later permitted streaming frame.
                    _frontierPublicationStarted = true;
                    var accepted = _frontierConstructionAccepted;
                    _frontierConstructionWork.Push(PublishAndInstallContinuationRoutine(accepted, (published, reason) => {
                        if (!ReferenceEquals(_frontierConstructionAccepted, accepted)) return;
                        _frontierPublicationCompleted = true;
                        _frontierPublicationPublished = published;
                        _frontierConstructionFailure = reason;
                    }));
                }
                else
                {
                    bool published = _frontierPublicationCompleted && _frontierPublicationPublished;
                    // note: Success intentionally changes the prior continuation owner; explicit completion prevents that expected transition being mistaken for cancellation.
                    if (!published && IsFrontierConstructionWorkCurrent())
                        _nextFrontierConstructionAt = Mathf.Max(_nextFrontierConstructionAt, Time.unscaledTime + FrontierConstructionRetrySeconds);
                    if (published) Debug.Log("[YQSemanticChunkStreamer] Accepted frontier location published and admitted without replacing live terrain.");
                    else if (!string.IsNullOrWhiteSpace(_frontierConstructionFailure))
                        Debug.LogWarning("[YQSemanticChunkStreamer] Frontier construction rejected: " + _frontierConstructionFailure);
                    CancelFrontierConstructionWork();
                }
            }'''
        replace(old, new)
        old = '''            overlaySnapshot = Newtonsoft.Json.JsonConvert.SerializeObject(_plan.semanticAuthority.featureOverlays),
            nextCellAuthority = _continuousAuthority.WithAcceptedMaterialization(installation.Prepared)
        };'''
        new = '''            overlaySnapshot = Newtonsoft.Json.JsonConvert.SerializeObject(_plan.semanticAuthority.featureOverlays)
        };
        // note: Isolate fresh accepted-cell/apron derivation from semantic graph preparation in the same private refresh owner.
        using (new ProfilerMarker("YQ.Frontier.Refresh.CellAuthority").Auto())
            staged.nextCellAuthority = _continuousAuthority.WithAcceptedMaterialization(installation.Prepared);'''
        replace(old, new)
        old = '''        if (!YQSemanticWorldAuthority.TryPrepareContinuationAuthority(_plan, candidate, prepared,
                out staged.nextSemanticAuthority, out failure) || !TryValidateContinuationRefresh(staged, out failure)) return false;'''
        new = '''        using (new ProfilerMarker("YQ.Frontier.Refresh.SemanticAuthority").Auto())
            if (!YQSemanticWorldAuthority.TryPrepareContinuationAuthority(_plan, candidate, prepared,
                out staged.nextSemanticAuthority, out failure)) return false;
        if (!TryValidateContinuationRefresh(staged, out failure)) return false;'''
        replace(old, new)
    preimage = out / (path.name + '.before')
    proposal = out / (path.name + '.proposal.cs')
    preimage.write_bytes(path.read_bytes())
    proposal.write_text(after, encoding='utf-8', newline='\n')
    identity['sources'][relative] = {'before_sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'proposal_sha256': hashlib.sha256(proposal.read_bytes()).hexdigest()}
    file_patch = ''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='a/' + relative, tofile='b/' + relative))
    patch += file_patch
    (out / ('Streamer.patch' if path.name == 'YQPlayerFollowingSemanticChunkStreamer.cs' else 'ProfileCleanup.patch')).write_text(file_patch, encoding='utf-8', newline='\n')
(out / 'Proposal.patch').write_text(patch, encoding='utf-8', newline='\n')
(out / 'Source_Identity.json').write_text(json.dumps(identity, indent=2))
print('Prepared output-only phase slicing: ' + str(out))
