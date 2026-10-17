"""Diagnose captured bank/heightmap disagreement; passing checks confirm a defect, not traversal acceptance."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,math,hashlib,subprocess,sys
root=Path(__file__).resolve().parents[2];native=root/sys.argv[1];out=root/sys.argv[2]
assert native.is_relative_to(root/'outputs/TerrainRefactor_20261013_BankTraversal/native')
assert out.is_relative_to(root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint') and not out.exists();out.mkdir()
inputs=[Path(__file__),native/'NativeHeights.f32',native/'BankMeshes.json',native/'Rejected.json',native/'Plan.json']
hashfile=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();hashes={str(p):hashfile(p) for p in inputs}
meta=json.loads((native/'Rejected.json').read_text());meshes=json.loads((native/'BankMeshes.json').read_text());resolution=meta['resolution'];size=meta['terrainSize'];origin=meta['terrainOrigin'];heights=struct.unpack('<'+str(resolution*resolution)+'f',(native/'NativeHeights.f32').read_bytes())
def terrain(x,z):
 gx=(x-origin['x'])/size['x']*(resolution-1);gz=(z-origin['z'])/size['z']*(resolution-1);ix=int(gx);iz=int(gz);u=gx-ix;v=gz-iz
 a,b,c,d=[heights[j*resolution+i]*size['y']+origin['y'] for i,j in [(ix,iz),(ix+1,iz),(ix,iz+1),(ix+1,iz+1)]]
 bil=a*(1-u)*(1-v)+b*u*(1-v)+c*(1-u)*v+d*u*v
 slash=a+(b-a)*u+(c-a)*v if u+v<=1 else d+(c-d)*(1-u)+(b-d)*(1-v)
 back=a+(b-a)*u+(d-b)*v if u>=v else a+(d-c)*u+(c-a)*v
 sx=size['x']/(resolution-1);sz=size['z']/(resolution-1)
 slopes=[math.degrees(math.atan(math.hypot(dx/sx,dz/sz))) for dx,dz in [(b-a,c-a),(d-c,d-b),(b-a,d-b),(d-c,c-a)]]
 return dict(index=[ix,iz],fraction=[u,v],corners=[a,b,c,d],bilinear=bil,diagonal_slash=slash,diagonal_back=back,allTriangleSlopes=slopes)
def tri_slope(v):
 a,b,c=v;u=[b[k]-a[k] for k in ['x','y','z']];w=[c[k]-a[k] for k in ['x','y','z']];nx=u[1]*w[2]-u[2]*w[1];ny=u[2]*w[0]-u[0]*w[2];nz=u[0]*w[1]-u[1]*w[0]
 return math.degrees(math.atan2(math.hypot(nx,nz),abs(ny)))
x,z=17.67,-9.87;matches=[]
for mi,m in enumerate(meshes):
 if not m['active'] or 'encampment_kelspire' not in m['owner']:continue
 vs=m['vertices'];ts=m['triangles']
 for ti in range(0,len(ts),3):
  a,b,c=[vs[ts[ti+j]] for j in range(3)];det=(b['z']-c['z'])*(a['x']-c['x'])+(c['x']-b['x'])*(a['z']-c['z'])
  if abs(det)<1e-10:continue
  u=((b['z']-c['z'])*(x-c['x'])+(c['x']-b['x'])*(z-c['z']))/det;v=((c['z']-a['z'])*(x-c['x'])+(a['x']-c['x'])*(z-c['z']))/det;w=1-u-v
  if min(u,v,w)<-1e-6:continue
  vertices=[a,b,c];regraded=[dict(q,y=terrain(q['x'],q['z'])['bilinear']+.028) for q in vertices]
  matches.append(dict(mesh=mi,triangle=ti//3,owner=m['owner'],vertices=vertices,barycentric=[u,v,w],height=u*a['y']+v*b['y']+w*c['y'],slope=tri_slope(vertices),vertexHeightErrors=[q['y']-terrain(q['x'],q['z'])['bilinear'] for q in vertices],bilinearRegroundedTriangleSlope=tri_slope(regraded),bilinearRegroundedCenter=sum(t*q['y'] for t,q in zip([u,v,w],regraded))))
center=terrain(x,z);east=terrain(x+.304,z);checks=[]
check=lambda value,name:checks.append(dict(name=name,passed=bool(value)))
check(meta['planUnchanged'],'captured canonical plan preserved')
check(len(matches)==1,'failed center has one hostile bank triangle')
m=matches[0]
check(m['height']-max(center['diagonal_slash'],center['diagonal_back'])>.6,'bank is above reachable final terrain at center')
check(max(m['vertexHeightErrors'])>1,'bank vertex mismatch exceeds interpolation error')
check(min(center['allTriangleSlopes'])>58,'both terrain diagonal orientations exceed controller slope')
check(east['bilinear']-center['bilinear']>.6,'final terrain footprint rise exceeds unchanged step plus skin')
check(m['bilinearRegroundedTriangleSlope']>58,'vertex-only regrounding remains too steep')
check(abs(center['bilinear']-center['diagonal_back'])<.02 and abs(center['bilinear']-center['diagonal_slash'])<.02,'center diagonal ambiguity cannot explain .7m mismatch')
result=dict(centerXZ=[x,z],terrain=center,eastFootprint=east,matches=matches,checks=checks)
(out/'Geometry.json').write_text(json.dumps(result,indent=2));changed=[str(p) for p in inputs if hashfile(p)!=hashes[str(p)]];failures=sum(not c['passed'] for c in checks)
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if checks and not failures and not changed else 'FAIL',checks=len(checks),failures=failures,changed_inputs=changed,input_sha256=hashes,evidence='CAPTURED_NATIVE_HEIGHTFIELD_AND_BANK_TRIANGLE_DEFECT_DIAGNOSIS; PASS_CONFIRMS_DEFECT_NOT_TRAVERSAL',limitations='Native log coordinates rounded .01m; both possible heightfield diagonal orientations evaluated. Vertex-only re-grounding is a rejected diagnostic hypothesis, not a production repair.')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(dict(checks=len(checks),failures=failures,meshHeight=m['height'],terrainBilinear=center['bilinear'],maxVertexError=max(m['vertexHeightErrors']),terrainSlopes=center['allTriangleSlopes'],regroundedSlope=m['bilinearRegroundedTriangleSlope']),indent=2));sys.exit(0 if receipt['status']=='PASS' else 1)
