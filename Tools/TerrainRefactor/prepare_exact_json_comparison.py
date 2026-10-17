"""Prepare output-only exact JSON comparison change; never edits Assets or saves."""
from pathlib import Path
import argparse,difflib,hashlib,json
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--out',required=True);args=ap.parse_args()
out=(root/args.out).resolve()
if not out.is_relative_to(root/'outputs') or out.exists():ap.error('Use a fresh outputs directory')
paths=('Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs','Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs','Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs')
helper=(root/'Tools/TerrainRefactor/ExactJsonComparison.proposal.cs').read_text()
out.mkdir(parents=True);patch='';sources={}
for relative in paths:
    path=root/relative;before=path.read_text();after=before
    if path.name=='YQProfileCommitStore.cs':
        marker='public static class YQProfileCommitStore\n{';assert before.count(marker)==1
        after=before.replace(marker,marker+'\n'+helper,1)
    elif path.name=='YQProfileSaveSystem.cs':
        pairs=(('!string.Equals(JsonConvert.SerializeObject(detachedPlayer, ContinuationPlayerJsonSettings), playerJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(detachedPlayer, playerJson, ContinuationPlayerJsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(_manifest, ContinuationJsonSettings), data.manifestJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(_manifest, data.manifestJson, ContinuationJsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(psm.state, ContinuationJsonSettings), data.playerSourceJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(psm.state, data.playerSourceJson, ContinuationJsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(accepted, ContinuationJsonSettings), data.acceptedJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(accepted, data.acceptedJson, ContinuationJsonSettings)'))
        for a,b in pairs:assert after.count(a)==1;after=after.replace(a,b,1)
    else:
        pairs=(('!string.Equals(JsonConvert.SerializeObject(detached, JsonSettings), sourceJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(detached, sourceJson, JsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(State, JsonSettings), snapshot.sourceJson, StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(State, snapshot.sourceJson, JsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(expectedWorld.identityRecords[index], JsonSettings),\n                        JsonConvert.SerializeObject(detached.identityRecords[index], JsonSettings), StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(detached.identityRecords[index],\n                        JsonConvert.SerializeObject(expectedWorld.identityRecords[index], JsonSettings), JsonSettings)'),
            ('!string.Equals(JsonConvert.SerializeObject(oldLocations[index], JsonSettings),\n                    JsonConvert.SerializeObject(nextLocations[index], JsonSettings), StringComparison.Ordinal)','!YQProfileCommitStore.JsonEqualsCaptured(nextLocations[index],\n                    JsonConvert.SerializeObject(oldLocations[index], JsonSettings), JsonSettings)'))
        for a,b in pairs:assert after.count(a)==1;after=after.replace(a,b,1)
    (out/(path.name+'.before')).write_bytes(path.read_bytes());(out/(path.name+'.proposal')).write_text(after)
    sources[relative]=dict(before_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),proposal_sha256=hashlib.sha256((out/(path.name+'.proposal')).read_bytes()).hexdigest())
    patch+=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/'+relative,tofile='b/'+relative))
(out/'Proposal.patch').write_text(patch)
(out/'Source_Identity.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PREVIEW_ONLY_NOT_APPLIED',sources=sources,helper_source_sha256=hashlib.sha256((root/'Tools/TerrainRefactor/ExactJsonComparison.proposal.cs').read_bytes()).hexdigest()),indent=2))
print('Prepared output-only exact comparison proposal: '+str(out))
