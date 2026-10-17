"""Read only the authorized disposable profile history; export bounded identity metadata, never save/profile content."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,base64,zlib
root=Path(__file__).resolve().parents[2]
profile=Path('C:/Users/Garri/AppData/LocalLow/DefaultCompany/YourQuest/profiles/04de758c424e466a92158561de7ef246')
out=root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss';assert not out.exists();out.mkdir()
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs={};records=[];checks=0
for folder in sorted((profile/'revisions').iterdir()):
    terrain=folder/'auxiliary/generated_terrain_heightmap_v1.json'
    if not folder.is_dir() or not terrain.is_file():continue
    inputs[terrain]=digest(terrain);snapshot=json.loads(terrain.read_text())
    records.append(dict(revision=int(folder.name[1:9]),schema=snapshot.get('schemaVersion'),hasTerrain=snapshot.get('hasTerrain'),seed=snapshot.get('worldSeed'),fingerprint=snapshot.get('planFingerprint'),checksum=snapshot.get('heightmapChecksum'),file_sha256=inputs[terrain]))
selected={}
for revision in (19,25,26):
    folder=next(p for p in (profile/'revisions').iterdir() if p.name.startswith(f'r{revision:08d}-'))
    world=folder/'world_state.json';terrain=folder/'auxiliary/generated_terrain_heightmap_v1.json';inputs[world]=digest(world)
    w=json.loads(world.read_text());p=w['generatedWorldPlan'];s=json.loads(terrain.read_text())
    # note: Exact current BuildVisualSignature accepted-V2 field ordering; enum AcceptedV2=2 is verified in source.
    signature='spatial:2:'+p['spatialPlanV2']['contentHash']+'|'
    for r in p.get('regions') or []:signature+=r['regionId']+':'+(r.get('assetStyleKey') or '')+'|'
    for a in p.get('assetPalettes') or []:signature+=a['regionId']+':'+(a.get('styleKey') or '')+':'+(a.get('layoutRuleProfile') or '')+'|'
    for v in p.get('settlements') or []:signature+=v['settlementId']+':'+(v.get('runtimeSiteKitId') or '')+'|'
    for v in p.get('encampments') or []:signature+=v['encampmentId']+':'+(v.get('runtimeSiteKitId') or '')+'|'
    fingerprint=hashlib.sha256(signature.encode()).hexdigest()
    selected[revision]=dict(worldId=w['worldIdentity']['worldId'],owner=w['worldIdentity']['ownerProfileId'],seed=p['worldSeed'],contentHash=p['spatialPlanV2']['contentHash'],computedFingerprint=fingerprint,storedFingerprint=s.get('planFingerprint'),regions=[dict(regionId=r['regionId'],style=r.get('assetStyleKey')) for r in p.get('regions') or []],palettes=[dict(regionId=a['regionId'],style=a.get('styleKey'),layout=a.get('layoutRuleProfile')) for a in p.get('assetPalettes') or []],snapshotPresent=s['hasTerrain'])
    if s['hasTerrain']:
        assert s['planFingerprint']==fingerprint;checks+=1
        raw=zlib.decompress(base64.b64decode(s['compressedHeightmapBase64']),-15)
        assert len(raw)==s['uncompressedByteCount'] and hashlib.sha256(raw).hexdigest()==s['heightmapChecksum'];checks+=1
assert selected[25]['snapshotPresent'] and not selected[26]['snapshotPresent'];checks+=1
for key in ('worldId','owner','seed','contentHash'):assert selected[25][key]==selected[26][key];checks+=1
assert selected[25]['computedFingerprint']!=selected[26]['computedFingerprint'];checks+=1
assert all(r['hasTerrain'] for r in records if 19<=r['revision']<=25);checks+=1
assert all(not r['hasTerrain'] for r in records if 26<=r['revision']<=61);checks+=1
for p in (Path(__file__),root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs',root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldTerrain.cs',root/'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs',root/'Assets/Assets/Scripts/Generated/YQWorldGenerationV2Contracts.cs'):inputs[p]=digest(p)
changed=[str(p) for p,h in inputs.items() if digest(p)!=h]
assert not changed
(out/'Audit.json').write_text(json.dumps(dict(profile=profile.name,revisions=records,transition=selected),indent=2))
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=checks,failures=0,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='READ_ONLY_SELECTED_PROFILE_COMMITTED_HISTORY_AND_PAYLOAD_CHECKSUM; VISUAL_FINGERPRINT_REPRODUCTION; NO_NATIVE_RESTORE/PROFILE_WRITES'),indent=2))
print(json.dumps(dict(checks=checks,lastValid=selected[25],firstEmpty=selected[26]),indent=2))
