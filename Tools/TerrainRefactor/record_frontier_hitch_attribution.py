"""Record static profiler/immutable selected-profile commit correlation; no Unity, saves or source mutation."""
from pathlib import Path
from datetime import datetime,timedelta,timezone
import hashlib,json,re
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss/hitch_attribution_2'
assert not out.exists();out.mkdir()
frame=root/'outputs/DialogueVramRepair_20261011/whole-session-063617.json'
direct=root/'Docs/G08_R1_Streaming_CPU_2026-10-14_063950_341.md'
profile=Path('C:/Users/Garri/AppData/LocalLow/DefaultCompany/YourQuest/profiles/04de758c424e466a92158561de7ef246/revisions')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inputs={p:sha(p) for p in (frame,direct,Path(__file__))}
utc=lambda value:datetime.strptime(value[:26],'%Y-%m-%dT%H:%M:%S.%f').replace(tzinfo=timezone.utc)
frames=json.loads(frame.read_text());start=utc(frames['utc'])-timedelta(seconds=frames['elapsedSeconds'])
hitches=[h for h in frames['Hitches'] if h['elapsedSeconds']>350 and h['gapMs']>1000]
records=[];checks=0
for hitch,rev in zip(hitches,(64,65,66)):
    folder=next(profile.glob(f'r{rev:08d}-*'));path=folder/'world_state.json';inputs[path]=sha(path);world=json.loads(path.read_text())
    continuation=world['generatedWorldPlan']['spatialPlanV2']['acceptedContinuation']
    end=start+timedelta(seconds=hitch['elapsedSeconds']);begin=end-timedelta(milliseconds=hitch['gapMs']);written=datetime.fromtimestamp(path.stat().st_mtime,timezone.utc)
    assert begin<=written<=end;checks+=1
    assert hitch['values']['PlayerLoop']>3000 and hitch['values']['CoroutinesDelayedCalls']<2 and hitch['values']['GC.Collect']<50;checks+=1
    records.append(dict(elapsedSeconds=hitch['elapsedSeconds'],gapMs=hitch['gapMs'],gapBeginUtc=begin.isoformat(),gapEndUtc=end.isoformat(),pairedRevision=rev,pairedWorldWrittenUtc=written.isoformat(),acceptedContinuationRevision=continuation['revision'],acceptedLocationCount=len(continuation['locations']),worldBytes=path.stat().st_size))
assert [r['acceptedLocationCount'] for r in records]==[34,35,36];checks+=1
text=direct.read_text()
metric=lambda name:float(re.search(r'\| '+re.escape(name)+r' \| ms \| \d+ \| [\d.]+ \| ([\d.]+) \|',text)[1])
streamer=metric('YQPlayerFollowingSemanticChunkStreamer.Update');loop=metric('PlayerLoop')
assert streamer>3500 and streamer/loop>.99;checks+=1
direct_start=utc(re.search(r'UTC: (\S+)',text)[1])-timedelta(seconds=float(re.search(r'Real elapsed seconds: ([\d.]+)',text)[1]))
folder72=next(profile.glob('r00000072-*'));path72=folder72/'world_state.json';inputs[path72]=sha(path72);written72=datetime.fromtimestamp(path72.stat().st_mtime,timezone.utc)
assert direct_start<=written72<=direct_start+timedelta(seconds=15.053);checks+=1
aux=[]
for rev in (64,65,66,72):
    path=next(profile.glob(f'r{rev:08d}-*'))/'auxiliary/generated_terrain_heightmap_v1.json';inputs[path]=sha(path);data=json.loads(path.read_text());assert data['hasTerrain'];checks+=1
    aux.append(dict(revision=rev,fingerprint=data['planFingerprint'],checksum=data['heightmapChecksum']))
assert len({(r['fingerprint'],r['checksum']) for r in aux})==1;checks+=1
for p in (root/'Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs',root/'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs',root/'Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs'):inputs[p]=sha(p)
changed=[str(p) for p,h in inputs.items() if sha(p)!=h];assert not changed
(out/'Attribution.json').write_text(json.dumps(dict(frameUtc=frames['utc'],frameStartUtc=start.isoformat(),correlations=records,direct=dict(streamerUpdateMaximumMs=streamer,playerLoopMaximumMs=loop,pairedRevision72Utc=written72.isoformat()),auxiliary=aux,probableOwner='YQPlayerFollowingSemanticChunkStreamer.ProcessFrontierConstructionWork -> TryPublishAndInstallContinuation',profilingTargets=['YQ.Frontier.PrepareRefresh','YQ.Frontier.PrepareProfile','YQ.Frontier.PublishProfile','YQ.Frontier.InstallRuntime']),indent=2))
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=checks,failures=0,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='DIRECT_NATIVE_RECORDER_STREAMER_OWNER_PLUS_IMMUTABLE_PAIRED_COMMIT_TIME_CORRELATION; INNER_PUBLICATION_PHASE_TIMINGS_UNMEASURED; NO_SAVE_MUTATION'),indent=2))
print('RESULT checks='+str(checks)+' failures=0')
