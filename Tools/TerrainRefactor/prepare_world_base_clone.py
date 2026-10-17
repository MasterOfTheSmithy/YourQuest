"""Prepare a guarded main-thread base-world clone proposal outside Assets."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,difflib
ROOT=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args();out=(ROOT/a.out).resolve()
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Use fresh outputs directory')
out.mkdir(parents=True)
paths=['Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs','Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs','Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs']
helper=(ROOT/'Tools/TerrainRefactor/WorldBaseClone.proposal.cs').read_text();patch='';receipt={'utc':datetime.now(timezone.utc).isoformat(),'status':'OUTPUT_ONLY_PROPOSAL','sources':{}}
for relative in paths:
    path=ROOT/relative;before=path.read_text(encoding='utf-8-sig');after=before
    def replace(old,new):
        global after
        assert after.count(old)==1,(path.name,old[:100],after.count(old));after=after.replace(old,new,1)
    if path.name=='WorldStateManager.cs':
        marker='    internal bool TryPrepareContinuationSnapshot(WorldState expectedWorld, PlayerState pairedPlayer,'
        replace(marker,helper+marker)
        replace('        YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction = null)\n',
                '        YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction = null,\n        PreparedContinuationBaseWorld baseWorld = null)\n')
        old='''            string sourceJson = JsonConvert.SerializeObject(expectedWorld, JsonSettings);
            WorldState detached = JsonConvert.DeserializeObject<WorldState>(sourceJson, JsonSettings);
            if (!YQProfileCommitStore.JsonEqualsCaptured(detached, sourceJson, JsonSettings))
            {
                failure = "The world snapshot requires normalization before continuation staging; existing document fields were preserved.";
                return false;
            }'''
        new='''            string sourceJson;
            WorldState detached;
            // note: Consume only a private base copy of byte-identical current inputs. Changed or unsupported preparation retries the original synchronous factory.
            if (baseWorld == null || !baseWorld.TryTake(this, expectedWorld, out sourceJson, out detached))
            {
                sourceJson = JsonConvert.SerializeObject(expectedWorld, JsonSettings);
                detached = JsonConvert.DeserializeObject<WorldState>(sourceJson, JsonSettings);
                if (!YQProfileCommitStore.JsonEqualsCaptured(detached, sourceJson, JsonSettings))
                {
                    failure = "The world snapshot requires normalization before continuation staging; existing document fields were preserved.";
                    return false;
                }
            }'''
        replace(old,new)
    elif path.name=='YQProfileSaveSystem.cs':
        old='''    public bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationPublication publication, out string failure)
    {'''
        new='''    public bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationPublication publication, out string failure)
        => TryPrepareContinuationPublicationCore(construction, null, out publication, out failure);

    internal bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        WorldStateManager.PreparedContinuationBaseWorld baseWorld, out PreparedContinuationPublication publication, out string failure)
        => TryPrepareContinuationPublicationCore(construction, baseWorld, out publication, out failure);

    private bool TryPrepareContinuationPublicationCore(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        WorldStateManager.PreparedContinuationBaseWorld baseWorld, out PreparedContinuationPublication publication, out string failure)
    {'''
        replace(old,new)
        replace('out var worldSnapshot, out failure, construction) ||','out var worldSnapshot, out failure, construction, baseWorld) ||')
    else:
        replace('''        YQProfileSaveSystem.PreparedContinuationPublication publication = null;
        bool Current()''','''        YQProfileSaveSystem.PreparedContinuationPublication publication = null;
        WorldStateManager.PreparedContinuationBaseWorld baseWorld = null;
        bool Current()''')
        old='''            { failure = "Frontier publication ownership changed before profile preparation."; yield break; }
            using (new FrontierPublicationTimingScope(this, 1))'''
        new='''            { failure = "Frontier publication ownership changed before profile preparation."; yield break; }
            var worldManager = WorldStateManager.Instance;
            if (worldManager != null && worldManager.TryCaptureContinuationBaseWorld(_world, out baseWorld))
            {
                // note: Only private world data is held across this gap; fresh player capture and final profile publication still share their next step.
                yield return null;
                if (!Current() || !ReferenceEquals(YQProfileSaveSystem.Instance, profile) || !profile.isActiveAndEnabled)
                { failure = "Frontier publication ownership changed after private world capture."; yield break; }
            }
            using (new FrontierPublicationTimingScope(this, 1))'''
        replace(old,new)
        replace('if (!profile.TryPrepareContinuationPublication(construction, out publication, out failure)) yield break;',
                'if (!profile.TryPrepareContinuationPublication(construction, baseWorld, out publication, out failure)) yield break;')
        replace('''        finally
        {
            completed?.Invoke(published, failure);
        }
    }

    private bool TryPublishAndInstallPreparedContinuation''','''        finally
        {
            baseWorld?.Retire();
            completed?.Invoke(published, failure);
        }
    }

    private bool TryPublishAndInstallPreparedContinuation''')
    preimage=out/(path.name+'.before');proposal=out/(path.name+'.proposal.cs');preimage.write_bytes(path.read_bytes());proposal.write_text(after,encoding='utf-8',newline='\n')
    receipt['sources'][relative]={'before_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'proposal_sha256':hashlib.sha256(proposal.read_bytes()).hexdigest()}
    patch+=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/'+relative,tofile='b/'+relative))
(out/'Proposal.patch').write_text(patch,encoding='utf-8',newline='\n');(out/'Source_Identity.json').write_text(json.dumps(receipt,indent=2))
print('Output-only base-world proposal: '+str(out))
