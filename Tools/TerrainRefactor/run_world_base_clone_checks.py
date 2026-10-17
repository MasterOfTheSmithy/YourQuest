"""Compile and verify the output-only guarded pre-player world-base clone proposal."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,re,shutil,subprocess,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--proposal',type=Path,required=True);ap.add_argument('--compile',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args()
proposal,baseline,out=[(ROOT/p).resolve() for p in(a.proposal,a.compile,a.out)]
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Use fresh outputs directory')
out.mkdir(parents=True);inputs={};count=0;failures=[];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def check(ok,name):
 global count
 count+=1;print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 if not ok:failures.append(name)
def method(source,signature):
 start=source.index(signature);brace=source.index('{',start);depth=0
 for i in range(brace,len(source)):
  depth+=(source[i]=='{')-(source[i]=='}')
  if depth==0:return source[start:i+1]
 raise ValueError(signature)
identity=json.loads((proposal/'Source_Identity.json').read_text());overlays={};old={};new={}
for relative,row in identity['sources'].items():
 p=ROOT/relative;b=proposal/(p.name+'.before');n=proposal/(p.name+'.proposal.cs');check(sha(p)==sha(b)==row['before_sha256'],'exact preimage '+p.name);check(sha(n)==row['proposal_sha256'],'exact proposed source '+p.name)
 for q in(p,b,n):inputs[str(q)]=sha(q)
 overlays[str(p)]=n;old[p.name]=b.read_text(encoding='utf-8-sig');new[p.name]=n.read_text()
wold,wnew=old['WorldStateManager.cs'],new['WorldStateManager.cs'];pold,pnew=old['YQProfileSaveSystem.cs'],new['YQProfileSaveSystem.cs'];sold,snew=old['YQPlayerFollowingSemanticChunkStreamer.cs'],new['YQPlayerFollowingSemanticChunkStreamer.cs']
suffix='            // note: Only the accepted continuation and its appended identities change;'
before=method(wold,'    internal bool TryPrepareContinuationSnapshot(');after=method(wnew,'    internal bool TryPrepareContinuationSnapshot(')
check(before[before.index(suffix):]==after[after.index(suffix):],'entire original factory continuation/registration/pair/compiler/identity/timestamp/alias/final-proof suffix unchanged')
for sig in('    internal bool TryValidateContinuationSnapshot(', '    internal void ReserveContinuationPublication(', '    internal void ApplyPublishedContinuation(', '    private static bool TryPreserveContinuationLocationReferences('):check(method(wold,sig)==method(wnew,sig),'unchanged canonical world proof/apply '+sig.split('(')[0])
original=method(pold,'    public bool TryPrepareContinuationPublication(');core=method(pnew,'    private bool TryPrepareContinuationPublicationCore(')
check(original[original.index('{'):].replace('out var worldSnapshot, out failure, construction) ||','out var worldSnapshot, out failure, construction, baseWorld) ||')==core[core.index('{'):],'profile preparation body preserves all callbacks/gates/order except optional forwarding')
for sig in('    public bool TryPublishContinuation(', '    private bool TryValidateContinuationPublication(', '    private bool TryCaptureContinuationAuxiliary(', '    private bool TryRequireContinuationOwner('):check(method(pold,sig)==method(pnew,sig),'unchanged profile proof/publication '+sig.split('(')[0])
check(method(sold,'    private bool TryPublishAndInstallPreparedContinuation(')==method(snew,'    private bool TryPublishAndInstallPreparedContinuation('),'entire atomic final publish/install remains unchanged')
routine=method(snew,'    private IEnumerator PublishAndInstallContinuationRoutine(')
check(routine.count('yield return null;')==2 and routine.rindex('yield return null;')<routine.index('TryPrepareContinuationPublication'),'all two conditional gaps precede fresh player capture')
check('Task.Run' not in (proposal/'Proposal.patch').read_text() and 'baseWorld?.Retire();' in routine,'main-thread only preparation always retired by owner finally')
helper=wnew[wnew.index('    private static readonly object ContinuationBaseCloneIssuer'):wnew.index('    internal bool TryPrepareContinuationSnapshot(')]
helper=helper.replace('        using var timing = new Unity.Profiling.ProfilerMarker("YQ.Frontier.WorldBaseClone").Auto();','        // native profiler scope substituted in detached controls')
settings=wold[wold.index('    private static readonly JsonSerializerSettings JsonSettings'):wold.index('    private string SavePath')]
copy=after[after.index('            string sourceJson;'):after.index(suffix)]
proofpath=ROOT/'Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs';proof=proofpath.read_text();proof=proof[proof.index('    internal static bool JsonEqualsCaptured('):proof.index('    public static bool TryCommit(')];inputs[str(proofpath)]=sha(proofpath)
extracted='using System;using System.Collections.Generic;using Newtonsoft.Json;using UnityEngine;\npublic static class YQServiceLifecycle {public static int RequestEpoch;public static bool IsCurrent(int epoch)=>epoch==RequestEpoch;}\npublic static class YQProfileCommitStore {\n'+proof+'}\npublic class WorldStateManager {public static WorldStateManager Instance;public WorldState State;public bool isActiveAndEnabled=true;public WorldStateManager(WorldState value){State=value;}\n'+settings+'public static JsonSerializerSettings Settings=>JsonSettings;\n'+helper+'\ninternal WorldState CopyFinal(WorldState expectedWorld,PreparedContinuationBaseWorld baseWorld,out string result) {string failure="";\n'+copy.replace('return false;','throw new InvalidOperationException(failure);')+'result=sourceJson;return detached; }\n}\n'
controls=out/'controls';controls.mkdir();(controls/'Extracted.cs').write_text(extracted);tests=ROOT/'Tools/TerrainRefactor/WorldBaseCloneChecks.cs';inputs[str(tests)]=sha(tests);inputs[str(Path(__file__))]=sha(Path(__file__))
scheduling=ROOT/'Tools/TerrainRefactor/WorldBaseCloneSchedulingChecks.cs';inputs[str(scheduling)]=sha(scheduling);template=scheduling.read_text();assert template.count('@@ROUTINE@@')==1;(controls/'Scheduling.cs').write_text(template.replace('@@ROUTINE@@',routine))
fixture=next(Path('C:/Users/Garri/AppData/LocalLow/DefaultCompany/YourQuest/profiles/04de758c424e466a92158561de7ef246/revisions').glob('r00000025-*'))/'world_state.json';inputs[str(fixture)]=sha(fixture)
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(OutputType='Exe',TargetFramework='net10.0',EnableDefaultCompileItems='false',NoWarn='0436').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup')
for p in(controls/'Extracted.cs',controls/'Scheduling.cs',tests):ET.SubElement(items,'Compile',Include=str(p))
refs=[('Assembly-CSharp',baseline/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll')]
for ref in ET.parse(baseline/'runtime/Compile.csproj').findall('.//Reference'):
 p=Path(ref.findtext('HintPath'))
 if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((ref.attrib['Include'],p))
for name,p in refs:
 inputs[str(p)]=sha(p);node=ET.SubElement(items,'Reference',Include=name);ET.SubElement(node,'HintPath').text=str(p)
ET.ElementTree(project).write(controls/'Checks.csproj',encoding='utf-8');(controls/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
run=subprocess.run(['dotnet','run','--project',str(controls/'Checks.csproj'),'--',str(fixture)],cwd=ROOT,capture_output=True,text=True);log=run.stdout+run.stderr;(controls/'run.log').write_text(log);print(log,flush=True)
m=re.search(r'RESULT checks=(\d+) failures=0',log);check(run.returncode==0 and m is not None,'managed exact current DTO/codecs and source-extracted staging/consumption/fallback controls')
if m:count+=int(m[1])
base=json.loads((baseline/'receipt.json').read_text());check(base['status']=='PASS','baseline compile accepted')
for part in('runtime','editor'):
 tree=ET.parse(baseline/part/'Compile.csproj')
 for node in tree.findall('.//Compile'):
  p=Path(node.attrib['Include']);inputs[str(p)]=sha(p)
  if str(p) in overlays:node.set('Include',str(overlays[str(p)]))
 for node in tree.findall('.//Reference/HintPath'):
  p=Path(node.text);inputs[str(p)]=sha(p)
 for node in tree.findall('.//ProjectReference'):node.set('Include',str(out/'runtime/Compile.csproj'))
 folder=out/part;folder.mkdir();tree.write(folder/'Compile.csproj',encoding='utf-8');shutil.copyfile(baseline/part/'NuGet.Config',folder/'NuGet.Config')
run=subprocess.run(['dotnet','build',str(out/'editor/Compile.csproj'),'--nologo','-v:q'],cwd=ROOT,capture_output=True,text=True);(out/'compile.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr,flush=True);check(run.returncode==0,'complete runtime/editor proposal overlay compile')
changed=[p for p,h in inputs.items() if not Path(p).exists() or sha(Path(p))!=h];check(not changed,'all exact source/reference/proposal inputs stable')
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not failures else 'FAIL',checks=count,failures=len(failures),failure_names=failures,changed_inputs=changed,input_sha256=inputs,runtime_sources=base['runtime_sources'],editor_sources=base['editor_sources'],evidence='OUTPUT_ONLY_SOURCE_EQUIVALENCE_FULL_UNITY_REFERENCE_OVERLAY_COMPILE_AND_SOURCE_EXTRACTED_HELPER/COPY_WITH_REAL_DTO_JSONNET_CODECS; owner active/instance/epoch and profiler substituted; no actual publication, native timing or gameplay proof')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps({k:v for k,v in receipt.items() if k!='input_sha256'}));raise SystemExit(bool(failures))
