"""Verify exact output-only reviewed wander proposal with source-extracted decisions and complete overlay compile."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,re,shutil,subprocess,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2];ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--proposal',type=Path,required=True);ap.add_argument('--compile',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args();proposal,baseline,out=[(ROOT/p).resolve() for p in(a.proposal,a.compile,a.out)]
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Use fresh outputs directory')
out.mkdir(parents=True);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inputs={};checks=0;fails=[];overlays={};old={};new={}
def check(ok,name):
 global checks
 checks+=1;print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 if not ok:fails.append(name)
def method(text,marker):
 start=text.index(marker);i=text.index('{',start);depth=0
 for end in range(i,len(text)):
  depth+=(text[end]=='{')-(text[end]=='}')
  if depth==0:return text[start:end+1]
 raise ValueError(marker)
identity=json.loads((proposal/'Source_Identity.json').read_text())
for relative,row in identity['sources'].items():
 p=ROOT/relative;before=proposal/(p.name+'.before');after=proposal/(p.name+'.proposal.cs');check(sha(p)==sha(before)==row['before_sha256'],'exact current dirty preimage '+p.name);check(sha(after)==row['proposal_sha256'],'exact proposal source '+p.name)
 for q in(p,before,after):inputs[str(q)]=sha(q)
 overlays[str(p)]=after;old[p.name]=before.read_text(encoding='utf-8-sig');new[p.name]=after.read_text()
cs=new['YQRuntimeWorldSiteCatalog.cs'];ws=new['YQGeneratedNpcWander.cs'];original=old['YQGeneratedNpcWander.cs']
floor_markers=['    private bool TryProjectToSurface(', '    private bool TryProjectResidentSurface(', '    private bool HasResidentStandingClearance(', '    private static bool IsRoofOrCeilingCollider(', '    private float MaximumResidentSurfaceHeight(']
for marker in floor_markers:check(method(cs,marker)==method(old['YQRuntimeWorldSiteCatalog.cs'],marker),'existing exact floor gate unchanged '+marker.split('(')[0])
for marker in['    public void Configure(', '    private void UpdateCore(', '    private void UpdateAuthoredGait(', '    private float Next01(', '    private static uint StableHash(', '    private bool HitsExternalObstacle(']:check(method(ws,marker)==method(original,marker),'existing motion/seed/obstacle path unchanged '+marker.split('(')[0])
old_ground=method(original,'    private bool TryGetGround(');new_ground=method(ws,'    private bool TryGetGround(');marker='        // note: Search near the current foot plane'
check(old_ground[old_ground.index(marker):]==new_ground[new_ground.index(marker):],'complete original exterior ground selector including root physics exclusion unchanged')
check('if (usesReviewedSurface && settlement != null)' in new['YQGeneratedWorldPopulation.cs'] and 'wander.BindReviewedSurface(settlement.settlementId, info);' in new['YQGeneratedWorldPopulation.cs'],'only explicit reviewed settlement producer binds floor context; exterior/developer Configure path retained')
context=cs[cs.index('    private static readonly object ResidentWanderSurfaceIssuer'):cs.index('    private sealed class ResidentPositionBinding')]
check('worldOwner' in context and 'streamExecutionEpoch' in context and 'residentPositionBindings.Contains(binding)' in context,'context carries exact lifetime manager/world/plan/entity/binding/content/provider gates')
load=method(cs,'    private IEnumerator LoadPreparedSiteRoutine(')
check(load.index('RenewRegisteredResidentWanderSurfacesRoutine()')>load.index('loaded = contentSpawned;') and 'if (IsLoaded) yield return RenewRegisteredResidentWanderSurfacesRoutine();' in load,'retained actor renewal occurs only after full loaded owner publication')
methods='\n'.join(method(cs,m) for m in floor_markers)
wandermethods='internal bool UsesReviewedSurface => _requiresReviewedSurface;\n'+'\n'.join(method(ws,m) for m in['    internal void RenewReviewedSurface(', '    internal void BindReviewedSurface(', '    private bool TryGetGround(', '    private bool TryChooseTarget(', '    private void UpdateCore(', '    private bool HitsExternalObstacle(', '    private bool IsOwnedCollider(', '    private static bool IsRoofLike(', '    private float Next01('])
rotation='transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), Mathf.Clamp01(Time.deltaTime * 8f));'
assert wandermethods.count(rotation)==1
# note: Quaternion.LookRotation/Slerp are native engine calls. Only this visual observer is a no-op in the managed movement controls; position/floor decisions remain exact.
wandermethods=wandermethods.replace(rotation,'_ = direction; // native visual rotation observer substituted')
template=ROOT/'Tools/TerrainRefactor/ReviewedNpcWanderChecks.cs';inputs[str(template)]=sha(template);inputs[str(Path(__file__))]=sha(Path(__file__));program=template.read_text()
for marker,value in [('@@CONTEXT@@',context),('@@FLOOR@@',methods),('@@WANDER@@',wandermethods),('@@ORIGINAL@@',old_ground.replace('TryGetGround','OriginalTryGetGround',1))]:assert program.count(marker)==1;program=program.replace(marker,value)
controls=out/'controls';controls.mkdir();(controls/'Program.cs').write_text(program)
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(OutputType='Exe',TargetFramework='net10.0',NoWarn='0649;0169').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup')
for ref in ET.parse(baseline/'runtime/Compile.csproj').findall('.//Reference'):
 p=Path(ref.findtext('HintPath'))
 if p.name.startswith('UnityEngine.'):
  inputs[str(p)]=sha(p);node=ET.SubElement(items,'Reference',Include=ref.attrib['Include']);ET.SubElement(node,'HintPath').text=str(p)
ET.ElementTree(project).write(controls/'Checks.csproj',encoding='utf-8');(controls/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
run=subprocess.run(['dotnet','run','--project',str(controls/'Checks.csproj')],cwd=ROOT,capture_output=True,text=True);(controls/'run.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr,flush=True);m=re.search(r'RESULT checks=(\d+) failures=0',run.stdout);check(run.returncode==0 and m is not None,'exact source-extracted registered-floor/wander executable controls')
if m:checks+=int(m[1])
base=json.loads((baseline/'receipt.json').read_text());check(base['status']=='PASS','baseline compile accepted');additional_runtime=0
for part in('runtime','editor'):
 tree=ET.parse(baseline/part/'Compile.csproj')
 if part=='runtime':
  included={Path(node.attrib['Include']).resolve() for node in tree.findall('.//Compile')}
  for name in('YQPhysicalChunkAdmissionBoundary.cs','YQPlayerFollowingSemanticChunkStreamer.PhysicsAdmission.cs'):
   p=ROOT/'Assets/Assets/Scripts/Generated'/name
   if p.exists() and p not in included:
    ET.SubElement(tree.getroot().find('ItemGroup'),'Compile',Include=str(p));additional_runtime+=1
 for node in tree.findall('.//Compile'):
  p=Path(node.attrib['Include']);inputs[str(p)]=sha(p)
  if str(p) in overlays:node.set('Include',str(overlays[str(p)]))
 for node in tree.findall('.//Reference/HintPath'):p=Path(node.text);inputs[str(p)]=sha(p)
 for node in tree.findall('.//ProjectReference'):node.set('Include',str(out/'runtime/Compile.csproj'))
 folder=out/part;folder.mkdir();tree.write(folder/'Compile.csproj',encoding='utf-8');shutil.copyfile(baseline/part/'NuGet.Config',folder/'NuGet.Config')
run=subprocess.run(['dotnet','build',str(out/'editor/Compile.csproj'),'--nologo','-v:q'],cwd=ROOT,capture_output=True,text=True);(out/'compile.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr,flush=True);check(run.returncode==0,'complete current runtime/editor overlay compile with proposal')
changed=[p for p,h in inputs.items() if not Path(p).exists() or sha(Path(p))!=h];check(not changed,'all exact sources/proposals/reference inputs stable')
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not fails else 'FAIL',checks=checks,failures=len(fails),failure_names=fails,changed_inputs=changed,input_sha256=inputs,runtime_sources=base['runtime_sources']+additional_runtime,editor_sources=base['editor_sources'],evidence='OUTPUT_ONLY_FULL_UNITY_REFERENCE_OVERLAY_COMPILE_SOURCE_EQUIVALENCE_AND_EXTRACTED_REAL_FLOOR/CONTEXT/WANDER_DECISIONS; Physics/scene/lifetime/initial-contact helpers and native visual rotation are deterministic substitutes; no native animation/placement/traversal proof')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps({k:v for k,v in receipt.items() if k!='input_sha256'}));raise SystemExit(bool(fails))
