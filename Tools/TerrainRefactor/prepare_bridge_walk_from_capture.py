"""Prepare unexecuted R3 bridge walk requests from a matching loaded native strip census; never touches Unity or saves."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,math,re

ROOT=Path(__file__).resolve().parents[2]
def capture_time(value):
 # note: Unity's round-trip UTC formatter has seven fractional digits; older bundled Python accepts six. This only compares capture timestamps.
 return datetime.fromisoformat(re.sub(r'(\.\d{6})\d+',r'\1',value.replace('Z','+00:00')))
def finite_point(p):
 return all(isinstance(p.get(k),(int,float)) and math.isfinite(p[k]) for k in('x','y','z'))
def distance(a,b):return math.sqrt(sum((a[k]-b[k])**2 for k in('x','y','z')))
def planar(a,b):return math.hypot(a['x']-b['x'],a['z']-b['z'])
def safe_id(value):
 for c in(':','|','/','\\'):value=value.replace(c,'_')
 return value
def strip_stations(geometry):
 if not geometry or not geometry.get('captured') or geometry.get('topology')!='section_strip' or geometry.get('bankReconstructionAvailable') is not True:
  raise ValueError('Require exact captured native section-strip topology; buffered unions are never treated as stations.')
 vertices=geometry.get('vertices',[])
 if len(vertices)<8 or len(vertices)%4 or not all(finite_point(p) for p in vertices):raise ValueError('Nonfinite or malformed native strip vertices')
 points=[]
 for i in range(0,len(vertices),4):
  left,right,bottom_left,bottom_right=vertices[i:i+4]
  if abs(left['y']-right['y'])>.002 or abs(left['x']-bottom_left['x'])>.002 or abs(left['z']-bottom_left['z'])>.002 or abs(right['x']-bottom_right['x'])>.002 or abs(right['z']-bottom_right['z'])>.002 or left['y']<=bottom_left['y'] or right['y']<=bottom_right['y'] or planar(left,right)<.1:raise ValueError('Unexpected top/bottom station ordering')
  points.append({k:(left[k]+right[k])*.5 for k in('x','y','z')})
 if any(planar(a,b)<.000001 for a,b in zip(points,points[1:])):raise ValueError('Degenerate consecutive station')
 return points
def chain_sections(sections,tolerance=.002):
 if not sections:raise ValueError('No active bounded native deck sections')
 # note: Recursive native builds share exact terminal sections. Accept only a single unbranched connected chain, never jump a gap or choose one ambiguous splice.
 remaining=list(sections);links=[]
 for i,a in enumerate(remaining):
  for j,b in enumerate(remaining):
   if j<=i:continue
   matches=[(side,other) for side in(0,-1) for other in(0,-1) if distance(a[side],b[other])<=tolerance]
   if len(matches)>1:raise ValueError('Duplicate/ambiguous section endpoints')
   if matches:links.append((i,j,*matches[0]))
 degree=[sum(i in pair[:2] for pair in links) for i in range(len(remaining))]
 if any(n>2 for n in degree) or len(links)!=len(remaining)-1:raise ValueError('Bridge sections are disconnected, branched or cyclic')
 ends=[i for i,d in enumerate(degree) if d<=1]
 if len(remaining)>1 and len(ends)!=2:raise ValueError('No unique terminal banks')
 first=ends[0];path=remaining[first][:];used={first}
 if len(remaining)>1:
  link=next(x for x in links if first in x[:2]);side=link[2] if first==link[0] else link[3]
  if side==0:path.reverse()
 while len(used)<len(remaining):
  candidates=[]
  for i,section in enumerate(remaining):
   if i in used:continue
   for reverse in(False,True):
    p=section[::-1] if reverse else section
    if distance(path[-1],p[0])<=tolerance:candidates.append((i,p))
  if len(candidates)!=1:raise ValueError('Cannot uniquely join actual native seam')
  index,section=candidates[0];used.add(index);path.extend(section[1:])
 return path
def belongs_route(point,route):
 reach=route['width']+route['shoulderWidth']+16;vertices=route['points']
 for a,b in zip(vertices,vertices[1:]):
  dx,dz=b['x']-a['x'],b['z']-a['z'];length=dx*dx+dz*dz
  t=max(0,min(1,((point['x']-a['x'])*dx+(point['z']-a['z'])*dz)/length)) if length>.0001 else 0
  if math.hypot(point['x']-a['x']-dx*t,point['z']-a['z']-dz*t)<=reach:return True
 return any(planar(point,p)<=reach for p in vertices)
def extract(discovery,census,crossing_id):
 crossings=[c for c in discovery['crossings'] if c['crossingId']==crossing_id]
 if len(crossings)!=1:raise ValueError('Missing/ambiguous accepted crossing identity')
 crossing=crossings[0];routes=[r for r in discovery['routes'] if r['routeId']==crossing['routeId']]
 if len(routes)!=1:raise ValueError('Missing/ambiguous accepted route')
 expected='AcceptedRiverCrossing_'+safe_id(crossing_id);sections=[];owners=[]
 matches=[c['crossingId'] for c in discovery['crossings'] if safe_id(c['crossingId'])==safe_id(crossing_id)]
 if len(matches)!=1:raise ValueError('Sanitized root does not identify one exact accepted crossing')
 for bridge in census['bridges']:
  components=[re.sub(r'\[\d+\]$','',x) for x in bridge['path'].split('/')]
  if expected not in components:continue
  if not bridge['active']:raise ValueError('Crossing includes inactive bridge owner')
  decks=[c for c in bridge['colliders'] if c.get('mesh')=='YQ_StoneBridgeWalkSurface']
  if len(decks)!=1 or not decks[0]['active'] or not decks[0]['enabled'] or decks[0]['isTrigger']:raise ValueError('Actual bridge has no unique active nontrigger native tread')
  sections.append(strip_stations(decks[0]['geometry']));owners.append({'path':bridge['path'],'instanceId':bridge['instanceId'],'collider':decks[0]['path']})
 path=chain_sections(sections)
 if not all(belongs_route(p,routes[0]) for p in path):raise ValueError('Existing R3 accepted-route admission excludes this derived geometry; do not bypass its gate.')
 return crossing,routes[0],path,owners
def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--discovery',type=Path,required=True);ap.add_argument('--crossing',required=True);ap.add_argument('--out',type=Path,required=True);ap.add_argument('--render-origin',type=float,nargs=3,required=True);ap.add_argument('--historical',action='store_true');a=ap.parse_args();folder=(ROOT/a.discovery).resolve();out=(ROOT/a.out).resolve()
 if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Require fresh outputs directory')
 if a.render_origin!=[0,0,0]:ap.error('Current R3 BelongsToFeature is not render-origin-aware; nonzero origin needs a separate reviewed observer repair.')
 discovery_path=folder/'Discovery.json';census_path=folder/'BridgeColliders.json';discovery=json.loads(discovery_path.read_text());census=json.loads(census_path.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
 identities={str(p):sha(p) for p in(discovery_path,census_path,Path(__file__))};changed=[]
 for relative,h in discovery['sources'].items():
  p=ROOT/relative
  if not p.exists() or sha(p)!=h:changed.append(relative)
 for name,key in(('Assembly-CSharp.dll','runtimeAssemblyHash'),('Assembly-CSharp-Editor.dll','editorAssemblyHash')):
  p=ROOT/'Library/ScriptAssemblies'/name
  if not p.exists() or sha(p)!=discovery.get(key):changed.append('loaded assembly parity '+name)
  elif p.exists():identities[str(p)]=sha(p)
 if changed and not a.historical:ap.error('Capture source hashes differ from current production; obtain a fresh snapshot. '+str(changed[:4]))
 delta=abs((capture_time(discovery['utc'])-capture_time(census['utc'])).total_seconds())
 if delta>180:ap.error('Discovery/census are not one bounded capture')
 crossing,route,path,owners=extract(discovery,census,a.crossing)
 # note: Generated requests do not admit an off-road ingress. The operator must reach a measured dry endpoint through the actual road first; the runtime owner will still validate every request and frame.
 if planar(discovery['playerPosition'],path[-1])<planar(discovery['playerPosition'],path[0]):path.reverse()
 root_offset=discovery['capsule']['rootToFeet']['y'];waypoints=[dict(p,y=p['y']-root_offset) for p in path];length=sum(planar(x,y) for x,y in zip(path,path[1:]));out.mkdir(parents=True)
 for direction,points in(('forward',waypoints),('reverse',list(reversed(waypoints)))):
  for index,start in enumerate(range(0,len(points)-1,255)):
   rows=points[start:min(len(points),start+256)];request=dict(action='walk',rowId='actual_bridge_'+direction+'_'+str(index+1),profileId=discovery['profileId'],artifactHash=discovery['artifactHash'],featureId=route['routeId'],waypoints=rows,timeoutSeconds=300,tolerance=.25,sprint=False)
   (out/(direction+'_'+str(index+1)+'.json')).write_text(json.dumps(request,indent=2))
 evidence=dict(utc=datetime.now(timezone.utc).isoformat(),status='UNEXECUTED_HISTORICAL_REQUESTS' if a.historical else 'UNEXECUTED_SOURCE_MATCHED_CAPTURE_REQUESTS',input_sha256=identities,capture_utc=discovery['utc'],changed_production_sources=changed,crossing=crossing,route_id=route['routeId'],native_sections=len(owners),native_station_count=len(path),planar_length=length,owners=owners,terminal_tread_centers=[path[0],path[-1]],player_capture_position=discovery['playerPosition'],first_bank_distance=planar(discovery['playerPosition'],path[0]),requires_actual_dry_road_ingress=True,physical_fitted_centerline=path,evidence='READ_ONLY_RECONSTRUCTION_OF_ACTUAL_CAPTURED_NATIVE_STRIP; NO_UNITY_ACTIONS_NO_INPUT_NO_TELEPORT_NO_TRAVERSAL_CERTIFICATE; cached planned span.points is intentionally not used')
 (out/'Evidence.json').write_text(json.dumps(evidence,indent=2));print(json.dumps({k:v for k,v in evidence.items() if k not in('physical_fitted_centerline','owners','input_sha256','changed_production_sources')}))
if __name__=='__main__':main()
