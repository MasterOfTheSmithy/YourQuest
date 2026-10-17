"""Read-only triangle reconstruction of captured native bank mesh witnesses; no physics replay."""
from pathlib import Path
from datetime import datetime,timezone
import json,math,re,hashlib,sys
root=Path(__file__).resolve().parents[2]
native=root/sys.argv[1];out=root/sys.argv[2]
assert native.is_relative_to(root/'outputs/TerrainRefactor_20261013_BankTraversal/native')
assert out.is_relative_to(root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint') and not out.exists()
out.mkdir(parents=True)
log=root/'outputs/TerrainRefactor_20261013_BankTraversal/Native_Diagnostic.log'
inputs=[Path(__file__),native/'BankMeshes.json',native/'Rejected.json',log]
hashfile=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
hashes={str(p):hashfile(p) for p in inputs}
meshes=json.loads(inputs[1].read_text());metadata=json.loads(inputs[2].read_text())
triangles={}
for m in meshes:
 if not m['active']:continue
 vs=[(v['x'],v['y'],v['z']) for v in m['vertices']]
 ts=m['triangles'];dest=triangles.setdefault(m['owner'],[])
 for i in range(0,len(ts),3):
  a,b,c=[vs[ts[i+j]] for j in range(3)]
  ux,uy,uz=[b[k]-a[k] for k in range(3)];vx,vy,vz=[c[k]-a[k] for k in range(3)]
  nx,ny,nz=uy*vz-uz*vy,uz*vx-ux*vz,ux*vy-uy*vx
  if abs(ny)<1e-12:continue
  slope=math.degrees(math.atan2(math.hypot(nx,nz),abs(ny)))
  dest.append((a,b,c,ny,slope,i//3))
def hit(owner,x,z):
 result=[]
 for a,b,c,ny,slope,index in triangles[owner]:
  if x<min(a[0],b[0],c[0])-1e-7 or x>max(a[0],b[0],c[0])+1e-7 or z<min(a[2],b[2],c[2])-1e-7 or z>max(a[2],b[2],c[2])+1e-7:continue
  det=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
  if abs(det)<1e-12:continue
  u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/det
  v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/det;w=1-u-v
  if min(u,v,w)>=-1e-7:result.append(dict(height=u*a[1]+v*b[1]+w*c[1],slope=slope,triangle=index,upward=ny>0,barycentric=[u,v,w]))
 return max(result,key=lambda r:r['height']) if result else None
pattern=re.compile(r'Rejected route=(.*?) feet=\((.*?)\) issues=UnsupportedFootprint support=YQ_GENERATED_WORLD_RUNTIME/(.*?)/SharedBridgeBankPath .*?footprintPoint=\((.*?)\) footprintSupport=(True|False) footprintDelta=([-.0-9]+) footprintSlope=([0-9]+(?:\.[0-9]+)?)')
witnesses=[];checks=0;failures=[]
def check(ok,label):
 global checks
 checks+=1
 if not ok:failures.append(label)
for ordinal,m in enumerate(pattern.finditer(log.read_text())):
 route,feet,owner,point,found,delta,slope=m.groups();f=list(map(float,feet.split(',')));q=list(map(float,point.split(',')))
 center=hit(owner,f[0],f[2]);bad=hit(owner,q[0],q[2]);check(center is not None and bad is not None,f'witness{ordinal} native center/failed footprint triangles exist')
 if not center or not bad:continue
 four=[]
 for ox,oz in ((.304,0),(-.304,0),(0,.304),(0,-.304)):
  h=hit(owner,f[0]+ox,f[2]+oz)
  four.append(dict(offset=[ox,oz],hit=h,delta=None if h is None else h['height']-center['height']))
 check(all(v['hit'] and abs(v['delta'])<=.6 and v['hit']['slope']<=58 for v in four),f'witness{ordinal} all4 unshifted support gates')
 # note: Dense triangle containment proves the local captured ribbon has no geometric hole; rounded log coordinates prevent exact PhysX replay.
 dense=[]
 for ring in range(1,9):
  radius=.304*ring/8
  for spoke in range(36):
   angle=spoke*math.tau/36;h=hit(owner,f[0]+radius*math.cos(angle),f[2]+radius*math.sin(angle));dense.append(h)
 check(all(h is not None for h in dense),f'witness{ordinal} dense288 sampled footprint has no mesh hole')
 olddelta=bad['height']-f[1];oldreachable=olddelta>=-.65 and olddelta<=.6
 check(not oldreachable or abs(olddelta)>.6,f'witness{ordinal} old shifted reference reproduces rejection')
 witnesses.append(dict(ordinal=ordinal,route=route,owner=owner,loggedFeet=f,loggedFailedPoint=q,loggedFound=found=='True',loggedDelta=float(delta),loggedSlope=float(slope),center=center,failedPointHit=bad,oldDeltaReconstructed=olddelta,oldRayReachable=oldreachable,centerDeltaReconstructed=bad['height']-center['height'],four=four,denseSamples=len(dense),denseMissing=sum(h is None for h in dense),denseMaxSlope=max(h['slope'] for h in dense if h),coordinateToleranceMeters=.005))
check(len(witnesses)==8,'eight logged native first-failure witnesses reconstructed')
check(metadata['planUnchanged'],'capture accepted plan unchanged')
changed=[str(p) for p in inputs if hashfile(p)!=hashes[str(p)]]
(out/'Witnesses.json').write_text(json.dumps(witnesses,indent=2))
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if checks and not failures and not changed else 'FAIL',checks=checks,failures=len(failures),failed_checks=failures,changed_inputs=changed,input_sha256=hashes,evidence='CAPTURED_NATIVE_BANK_MESH_TRIANGLE_RECONSTRUCTION_AT_ROUNDED_LOG_COORDINATES; NO_PHYSX_OR_PLAYER_TRAVERSAL_REPLAY',limitations='Log coordinates are rounded to 0.01m. Captured ribbon only; scene-wide occupancy/sweeps and terrain collider interpolation not replayed. Dense finite mesh samples do not prove all continuous capsule points.')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2))
for w in witnesses:print('WITNESS',w['ordinal'],'oldDelta',round(w['oldDeltaReconstructed'],6),'centerDelta',round(w['centerDeltaReconstructed'],6),'slope',round(w['failedPointHit']['slope'],3),'missing',w['denseMissing'],'fourMaxDelta',round(max(abs(v['delta']) for v in w['four']),6))
print('RESULT checks=%d failures=%d'%(checks,len(failures)))
for f in failures:print('FAIL',f)
sys.exit(0 if receipt['status']=='PASS' else 1)
