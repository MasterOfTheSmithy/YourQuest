"""Controlled shortest-crossing negative; not production geometry or accepted-world certification."""
from pathlib import Path
from datetime import datetime,timezone
import json,math,hashlib
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261014_NaturalBridgeProposal';width=7.5;bank_width=width+2;half_water=8.;grid=2.;radius=max(.5,bank_width*.55)+.5
# note: The flat strip isolates route alignment. These support rules retain the existing native cardinal/raster and three-station dry-run constraints.
def wet(x,z):return abs(z)<half_water
def dry(x,z):
 for dx,dz in [(0,0),(bank_width/2,0),(-bank_width/2,0),(0,bank_width/2),(0,-bank_width/2)]:
  if wet(x+dx,z+dz):return False
 for iz in range(math.floor((z-radius)/grid),math.floor((z+radius)/grid)+1):
  for ix in range(math.floor((x-radius)/grid),math.floor((x+radius)/grid)+1):
   lx,lz=ix*grid,iz*grid;dx=max(lx-x,0,x-lx-grid);dz=max(lz-z,0,z-lz-grid)
   if dx*dx+dz*dz>radius*radius:continue
   if any(wet(nx,nz) for nx,nz in [(lx,lz),(lx+grid,lz),(lx,lz+grid),(lx+grid,lz+grid)]):return False
 return True
def candidate(angle):
 axis=(math.cos(angle),math.sin(angle));contacts=[]
 for sign in [-1,1]:
  run=0
  for station in range(1,513):
   x,z=axis[0]*station*sign,axis[1]*station*sign;run=run+1 if dry(x,z) else 0
   if run>=3 and station>=5.6:contacts.append([x,z]);break
  else:return None
 # note: Both banks and a positive referenced wet interval are mandatory; a dry same-bank chord is not a crossing.
 if abs(axis[1])<1e-12:return None
 wet_length=half_water*2/abs(axis[1]);deck=math.dist(*contacts)
 return dict(angleDegrees=math.degrees(angle),contacts=contacts,wetLength=wet_length,deckLength=deck)
route=candidate(math.atan2(24,300));options=[c for degree in range(180) if (c:=candidate(math.radians(degree)))];best=min(options,key=lambda c:(c['wetLength'],c['deckLength'],c['angleDegrees']))
checks=[]
def check(value,name):checks.append(dict(name=name,passed=bool(value)))
check(route is not None,'route-aligned baseline exists')
check(route['deckLength']>300,'baseline preserves huge route-aligned negative')
check(abs(best['wetLength']-16)<1e-9,'natural crossing attains analytic shortest wet bank-to-bank chord')
check(abs(best['angleDegrees']-90)<1e-9,'shortest chord follows channel normal')
check(best['deckLength']<route['deckLength']*.15,'safe deck reduced by more than85percent')
check(all(dry(*p) for p in best['contacts']),'both full-shoulder raster-supported landings dry')
check(candidate(0) is None,'parallel same-bank direction cannot substitute for water crossing')
for contact,bank in zip(route['contacts'],best['contacts']):
 check(all(dry(contact[0]+(bank[0]-contact[0])*i/100,contact[1]+(bank[1]-contact[1])*i/100) for i in range(101)),'same-bank connector remains dry without second water crossing')
result=dict(domain='synthetic flat16m water strip; no origin landmarks/sites or admitted plan',requiredSpan=11.2,routeWidth=width,bankWidth=bank_width,fullRibbonRadius=radius,baseline=route,natural=best,checks=checks,limitations='This defines the negative acceptance case only. It does not prove actual basin selection, connector grade/occupancy, miter polygons, save restoration, or runtime traversal.')
(out/'Controlled_Negative.json').write_text(json.dumps(result,indent=2));fail=sum(not c['passed'] for c in checks);source=root/'Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs'
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not fail else 'FAIL',checks=len(checks),failures=fail,evidence='CONTROLLED_GEOMETRIC_NEGATIVE_WITH_ANALYTIC_SHORTEST_WET_CHORD; NOT_PRODUCTION_OR_ADMISSION_PROOF',input_sha256={str(Path(__file__)):hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),str(source):hashlib.sha256(source.read_bytes()).hexdigest()})
(out/'Controlled_Negative_Receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(dict(baseline=route,natural=best,checks=len(checks),failures=fail),indent=2))
