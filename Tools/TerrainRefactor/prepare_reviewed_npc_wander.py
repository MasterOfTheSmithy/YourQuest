"""Prepare output-only reviewed-floor wander repair against exact current dirty preimages."""
import argparse,hashlib,json,difflib
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[2];ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args();out=(ROOT/a.out).resolve()
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Require fresh outputs directory')
out.mkdir(parents=True);paths=['YQGeneratedNpcWander.cs','YQRuntimeWorldSiteCatalog.cs','YQGeneratedWorldPopulation.cs'];identity=dict(utc=datetime.now(timezone.utc).isoformat(),status='OUTPUT_ONLY_PROPOSAL',sources={});patch=''
for name in paths:
 p=ROOT/'Assets/Assets/Scripts/Generated'/name;before=p.read_text(encoding='utf-8-sig');after=before
 def replace(old,new):
  global after
  assert after.count(old)==1,(name,old[:80],after.count(old));after=after.replace(old,new,1)
 if name=='YQRuntimeWorldSiteCatalog.cs':
  replace('    private sealed class ResidentPositionBinding\n', (ROOT/'Tools/TerrainRefactor/ResidentWanderContext.proposal.cs').read_text()+'    private sealed class ResidentPositionBinding\n')
  replace('''        if (loaded)
        {
            // note: Replay durable overlays''','''        if (loaded)
        {
            // note: Retained reviewed actors adopt the newly published floor lifetime before ordinary pacing resumes.
            if (IsLoaded) yield return RenewRegisteredResidentWanderSurfacesRoutine();
            // note: Replay durable overlays''')
 elif name=='YQGeneratedWorldPopulation.cs':
  old='        wander.Configure(seed + "|" + npcRecord.npcId, 6f, dotVisual != null ? dotVisual.AuthoredWalkSpeed : 0.8f);'
  replace(old,old+'\n        // note: Only an explicitly reviewed settlement resident adopts its registered provider floor; exterior and temporary developer actors retain their original movement contract.\n        if (usesReviewedSurface && settlement != null)\n            wander.BindReviewedSurface(settlement.settlementId, info);')
 else:
  replace('    private YQDotGaitSampler _authoredGait;','    private YQDotGaitSampler _authoredGait;\n    private bool _requiresReviewedSurface;\n    private YQCompiledWorldSiteInstance.ResidentWanderSurfaceContext _reviewedSurface;')
  marker='    private void Awake()'
  bind='''    internal bool UsesReviewedSurface => _requiresReviewedSurface;

    internal void RenewReviewedSurface(YQCompiledWorldSiteInstance provider, string locationId, EntityInfo entity)
    {
        // note: Geometry reload cannot transfer a retained actor to a different provider or world; the original provider explicitly renews its registered contract.
        if (!_requiresReviewedSurface) return;
        if (_reviewedSurface == null || !_reviewedSurface.CanRenewFrom(provider))
            throw new System.InvalidOperationException("Resident wander renewal has no original actor/world owner: " + name);
        BindReviewedSurface(locationId, entity);
    }

    internal void BindReviewedSurface(string locationId, EntityInfo entity)
    {
        // note: Initial contact and all later home/step queries borrow the exact registered provider's accepted floor authority; a missing receipt never becomes terrain fallback.
        _requiresReviewedSurface = true;
        if (entity == null || entity.gameObject != gameObject ||
            !YQCompiledWorldSiteInstance.TryCaptureResidentWanderSurface(locationId, entity, out _reviewedSurface) ||
            !_reviewedSurface.TryProjectInitial(transform.position, out Vector3 floor) ||
            !YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(gameObject, floor) ||
            !YQGeneratedWorldTerrain.TryGetActorContactGeometry(gameObject, out _, out float bottom))
            throw new System.InvalidOperationException("Resident wander has no current reviewed home floor: " + name);
        _origin = transform.position;
        _contactOffset = bottom - _origin.y;
        _hasTarget = false;
    }

'''
  replace(marker,bind+marker)
  old='''        _hasTarget = false;
        return false;
    }

    private bool TryGetGround'''
  new='''        // note: A reviewed resident can head back to its validated home when nearby pacing targets fail, but each return step still requires the same current floor.
        if (_requiresReviewedSurface && TryGetGround(_origin, out Vector3 home))
        {
            _target = new Vector3(_origin.x, home.y - _contactOffset, _origin.z);
            _hasTarget = true;
            return true;
        }
        _hasTarget = false;
        return false;
    }

    private bool TryGetGround'''
  replace(old,new)
  marker='''    private bool TryGetGround(Vector3 candidate, out Vector3 ground)
    {'''
  new=marker+'''
        // note: Preserve reviewed ownership through every target/step/home query; unsupported or stale provider floors stop this resident instead of admitting terrain beneath a building.
        if (_requiresReviewedSurface)
        {
            ground = candidate;
            return _reviewedSurface != null && _reviewedSurface.TryProject(candidate, out ground) &&
                ground.y <= _origin.y + _contactOffset + 2.5f && ground.y >= _origin.y + _contactOffset - 2.5f;
        }'''
  replace(marker,new)
 pre=out/(name+'.before');post=out/(name+'.proposal.cs');pre.write_bytes(p.read_bytes());post.write_text(after,encoding='utf-8',newline='\n');identity['sources'][str(p.relative_to(ROOT)).replace('\\','/')]=dict(before_sha256=hashlib.sha256(pre.read_bytes()).hexdigest(),proposal_sha256=hashlib.sha256(post.read_bytes()).hexdigest())
 patch+=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/'+str(p.relative_to(ROOT)).replace('\\','/'),tofile='b/'+str(p.relative_to(ROOT)).replace('\\','/')))
(out/'Proposal.patch').write_text(patch,encoding='utf-8',newline='\n');(out/'Source_Identity.json').write_text(json.dumps(identity,indent=2));print(out)
