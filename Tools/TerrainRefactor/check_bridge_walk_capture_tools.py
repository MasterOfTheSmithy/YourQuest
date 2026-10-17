"""Focused pure geometry controls for captured native bridge itineraries; no Unity/profile actions."""
import argparse,importlib.util,json,hashlib
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[2];ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args();out=(ROOT/a.out).resolve()
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Require fresh outputs directory')
out.mkdir(parents=True);p=ROOT/'Tools/TerrainRefactor/prepare_bridge_walk_from_capture.py';spec=importlib.util.spec_from_file_location('bridgecapture',p);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);checks=[]
def check(ok,name):
 checks.append(dict(name=name,passed=bool(ok)));print(('PASS ' if ok else 'FAIL ')+name)
def rejected(fn):
 try:fn();return False
 except ValueError:return True
def point(x,z,y=10):return dict(x=x,y=y,z=z)
check(m.capture_time('2026-10-14T14:58:29.8454643Z')==datetime(2026,10,14,14,58,29,845464,tzinfo=timezone.utc),'Unity seven-digit UTC timestamps parse on bundled Python without changing capture time scope')
def geometry(path):
 vertices=[]
 for q in path:
  vertices.extend([dict(q,x=q['x']-2),dict(q,x=q['x']+2),dict(q,x=q['x']-2,y=q['y']-.047),dict(q,x=q['x']+2,y=q['y']-.047)])
 return dict(captured=True,topology='section_strip',bankReconstructionAvailable=True,vertices=vertices)
first=[point(0,0),point(0,2),point(0,4)];second=[point(0,4),point(0,6),point(0,8)]
check(m.strip_stations(geometry(first))==first,'exact top-pair midpoints recover fitted physical station centers')
check(m.chain_sections([first,second])==first+second[1:],'recursive native seam joins once without losing stations')
check(m.chain_sections([list(reversed(second)),list(reversed(first))]) in(first+second[1:],list(reversed(first+second[1:]))),'input ordering/reversed sections preserves same physical chain')
check(rejected(lambda:m.chain_sections([first,[point(0,4.01),point(0,8)]])),'one-centimetre missing seam cannot become a bridge itinerary')
check(rejected(lambda:m.chain_sections([first,first])),'duplicate section rejected')
check(rejected(lambda:m.chain_sections([first,second,[point(0,4),point(2,8)]])),'branching section cannot silently choose one route')
bad=geometry(first);bad['topology']='buffered_union';check(rejected(lambda:m.strip_stations(bad)),'buffered union never misinterpreted as paired strip stations')
bad=geometry(first);bad['vertices'][0]['y']=float('nan');check(rejected(lambda:m.strip_stations(bad)),'nonfinite geometry rejected')
bad=geometry(first);bad['vertices'][1]['y']=20;check(rejected(lambda:m.strip_stations(bad)),'wrong station topology rejected')
route=dict(routeId='road-a',width=4,shoulderWidth=1,points=[point(0,0),point(0,8)])
crossing=dict(crossingId='crossing:a',routeId='road-a');discovery=dict(crossings=[crossing],routes=[route]);g=geometry(first+second[1:]);census=dict(bridges=[dict(path='world/AcceptedRiverCrossing_crossing_a/Generated_RiverBridge_Stone',instanceId=1,active=True,colliders=[dict(path='deck',mesh='YQ_StoneBridgeWalkSurface',active=True,enabled=True,isTrigger=False,geometry=g)])])
check(m.extract(discovery,census,'crossing:a')[2]==first+second[1:],'exact accepted crossing owner and active native tread supplies stations')
census['bridges'][0]['active']=False;check(rejected(lambda:m.extract(discovery,census,'crossing:a')),'inactive physical crossing cannot be supplied as traversal path');census['bridges'][0]['active']=True
discovery['crossings'].append(dict(crossingId='crossing|a',routeId='road-a'));check(rejected(lambda:m.extract(discovery,census,'crossing:a')),'sanitized crossing-name collision refuses ambiguous ownership');discovery['crossings'].pop()
discovery['routes'][0]['points']=[point(100,0),point(100,8)];check(rejected(lambda:m.extract(discovery,census,'crossing:a')),'existing R3 route admission is checked rather than bypassed')
sources=[p,Path(__file__)];receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if all(x['passed'] for x in checks) else 'FAIL',checks=len(checks),failures=sum(not x['passed'] for x in checks),input_sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},cases=checks,evidence='PURE_CAPTURE_GEOMETRY_RECONSTRUCTION_ONLY; NO_RUNTIME_OR_BRIDGE_TRAVERSAL')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2));raise SystemExit(receipt['failures']!=0)
