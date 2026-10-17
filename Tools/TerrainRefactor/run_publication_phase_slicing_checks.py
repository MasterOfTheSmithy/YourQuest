"""Verify output-only phase proposal against exact preimages, full Unity-reference compile and managed scheduling controls."""
import argparse, hashlib, json, re, shutil, subprocess, xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime, timezone
ROOT=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__)
ap.add_argument('--proposal',type=Path,required=True);ap.add_argument('--compile',type=Path,required=True);ap.add_argument('--out',type=Path,required=True)
ap.add_argument('--single-yield',action='store_true');ap.add_argument('--old-proposal',type=Path)
a=ap.parse_args();proposal,baseline,out=[(ROOT/p).resolve() for p in (a.proposal,a.compile,a.out)]
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Use a fresh outputs directory')
out.mkdir(parents=True);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();checks=0;failures=[];inputs={}
def check(ok,name):
    global checks
    checks+=1;print(('PASS ' if ok else 'FAIL ')+name,flush=True)
    if not ok:failures.append(name)
identity=json.loads((proposal/'Source_Identity.json').read_text());overlays={}
for relative,row in identity['sources'].items():
    current=ROOT/relative;before=proposal/(current.name+'.before');after=proposal/(current.name+'.proposal.cs')
    check(sha(current)==sha(before)==row['before_sha256'],'exact current preimage '+current.name)
    check(sha(after)==row['proposal_sha256'],'exact proposal identity '+current.name)
    for p in(current,before,after):inputs[str(p)]=sha(p)
    overlays[str(current)]=after
old_stream=(proposal/'YQPlayerFollowingSemanticChunkStreamer.cs.before').read_text(encoding='utf-8-sig')
new_stream=(proposal/'YQPlayerFollowingSemanticChunkStreamer.cs.proposal.cs').read_text()
profile_source=ROOT/'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs'
if a.single_yield:
    old_profile=new_profile=profile_source.read_text(encoding='utf-8-sig');inputs[str(profile_source)]=sha(profile_source)
    check(len(identity['sources'])==1 and not (proposal/'ProfileCleanup.patch').exists(),'one-yield proposal is streamer-only with no profile cleanup API')
else:
    old_profile=(proposal/'YQProfileSaveSystem.cs.before').read_text(encoding='utf-8-sig');new_profile=(proposal/'YQProfileSaveSystem.cs.proposal.cs').read_text()
def method(source,signature):
    start=source.index(signature);brace=source.index('{',start);depth=0
    # note: These selected methods have no braces in literal/comment text; fail if signatures cease to be uniquely extractable.
    for i in range(brace,len(source)):
        if source[i]=='{':depth+=1
        elif source[i]=='}':
            depth-=1
            if depth==0:return source[start:i+1]
    raise ValueError('unterminated method')
old_atomic=method(old_stream,'    private bool TryPublishAndInstallContinuation(')
new_atomic=method(new_stream,'    private bool TryPublishAndInstallPreparedContinuation(')
suffix='        using (new FrontierPublicationTimingScope(this, 2))'
check(old_atomic[old_atomic.index(suffix):]==new_atomic[new_atomic.index(suffix):], 'entire final validation publish/projection/install executable suffix byte-identical')
check('yield' not in new_atomic,'final durable-pointer and runtime-install body contains no yield')
for sig in('    public bool TryPrepareContinuationPublication(', '    public bool TryPublishContinuation(', '    private bool TryValidateContinuationPublication(', '    private bool TryCaptureContinuationAuxiliary(', '    private bool TryRequireContinuationOwner('):
    check(method(old_profile,sig)==method(new_profile,sig),'complete profile proof/callback/disk/pointer method unchanged '+sig.split('(')[0].strip())
routine=method(new_stream,'    private IEnumerator PublishAndInstallContinuationRoutine(')
retire='' if a.single_yield else method(new_profile,'    internal void RetirePreparedContinuationPublication(')
check(routine.count('yield return null;')==(1 if a.single_yield else 2),'exact inter-phase frame yield count')
if a.single_yield:
    check(routine.index('yield return null;')>routine.index('TryPrepareContinuationRefresh') and routine.index('yield return null;')<routine.index('TryPrepareContinuationPublication'),'only yield precedes player source capture')
else:
    check(routine.index('yield return null;')>routine.index('TryPrepareContinuationRefresh') and routine.rindex('yield return null;')>routine.index('TryPrepareContinuationPublication') and routine.rindex('yield return null;')<routine.index('TryPublishAndInstallPreparedContinuation'), 'yield boundaries isolate preparation before atomic final owner')
check('Task.' not in routine and 'StartCoroutine' not in routine,'no secondary worker coroutine or writer introduced')
check(new_stream.count('_frontierPublicationStarted = _frontierPublicationCompleted = _frontierPublicationPublished = false;')==2,'outcome flags reset at request capture and cancellation')
cancel=method(new_stream,'    private void CancelFrontierConstructionWork(')
dispose=method(new_stream,'    private static Exception DisposeOwnedContinuationIterators(')
old_proc=method(old_stream,'    private void ProcessFrontierConstructionWork(');new_proc=method(new_stream,'    private void ProcessFrontierConstructionWork(')
prefix='        try\n        {\n            // note: Resources.LoadAsync'
scheduler=new_proc[new_proc.index(prefix):]
check(method(old_stream,'    private static Exception DisposeOwnedContinuationIterators(')==dispose,'existing full nested iterator disposal unchanged')
check('TryAutosave' not in (proposal/'Proposal.patch').read_text() and 'TryStageContinuationCommit' not in routine,'player writer and profile stage remain synchronous under original final boundary')
template=ROOT/'Tools/TerrainRefactor/PublicationPhaseSlicingChecks.cs';inputs[str(template)]=sha(template)
program=template.read_text()
if a.single_yield:
    assert a.old_proposal is not None
    old_proposal=(ROOT/a.old_proposal).resolve()
    old_stream_path=old_proposal/'YQPlayerFollowingSemanticChunkStreamer.cs.proposal.cs';old_profile_path=old_proposal/'YQProfileSaveSystem.cs.proposal.cs'
    for p in(old_stream_path,old_profile_path):inputs[str(p)]=sha(p)
    old_routine=method(old_stream_path.read_text(),'    private IEnumerator PublishAndInstallContinuationRoutine(')
    retire=method(old_profile_path.read_text(),'    internal void RetirePreparedContinuationPublication(')
    main_path=ROOT/'Tools/TerrainRefactor/PublicationSingleYieldProgram.cs';inputs[str(main_path)]=sha(main_path)
    program=program[:program.index('static class Program')]+main_path.read_text()
    program='using Newtonsoft.Json;\n'+program
    program=program.replace('    public Action OnPrepare;','''    public Action OnPrepare;
    public readonly PlayerState Player = new PlayerState();
    string playerSourceJson;
    static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
        Formatting = Formatting.None, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };
    public bool CapturedPlayerSourceMatches => ExactPlayerProof.JsonEqualsCaptured(Player, playerSourceJson, Settings);''',1)
    program=program.replace('        OnPrepare?.Invoke();','        playerSourceJson = JsonConvert.SerializeObject(Player, Settings);\n        OnPrepare?.Invoke();',1)
    program=program.replace('bool valid = PublishOK && !DeepChanged && ReferenceEquals(publication, Marker);','bool valid = PublishOK && !DeepChanged && ReferenceEquals(publication, Marker) && CapturedPlayerSourceMatches;',1)
    program=program.replace('    string _frontierConstructionFailure = "";','    string _frontierConstructionFailure = "";\n    string _frontierConstructionInputIdentity;',1)
    proof_path=ROOT/'Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs';inputs[str(proof_path)]=sha(proof_path)
    proof=proof_path.read_text();proof=proof[proof.index('    internal static bool JsonEqualsCaptured('):proof.index('    public static bool TryCommit(')]
    program+='\npublic static class ExactPlayerProof {\n'+proof+'}\n'
for key,value in [('@@RETIRE@@',retire),('@@ROUTINE@@',routine),('@@SCHEDULER@@',scheduler),('@@CANCEL@@',cancel),('@@DISPOSE@@',dispose)]:
    assert program.count(key)==1;program=program.replace(key,value)
variants=[('scheduling_controls',program,'')]
if a.single_yield:
    old_cancel = method(old_stream_path.read_text(), '    private void CancelFrontierConstructionWork(')
    excluded_program = program.replace(routine, old_routine).replace(cancel, old_cancel)
    variants.append(('excluded_two_yield_negative', excluded_program, 'OLD_TWO_YIELDS'))
for label,source,defines in variants:
    control=out/label;control.mkdir();(control/'Program.cs').write_text(source)
    project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
    for k,v in dict(OutputType='Exe',TargetFramework='net10.0',DefineConstants=defines,NoWarn='0436;0649;0169').items():ET.SubElement(props,k).text=v
    if a.single_yield:
        items=ET.SubElement(project,'ItemGroup');dll=baseline/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
        refs=[('Assembly-CSharp',dll)]
        for ref in ET.parse(baseline/'runtime/Compile.csproj').findall('.//Reference'):
            p=Path(ref.findtext('HintPath'))
            if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((ref.attrib['Include'],p))
        for name,p in refs:
            inputs[str(p)]=sha(p);node=ET.SubElement(items,'Reference',Include=name);ET.SubElement(node,'HintPath').text=str(p)
    ET.ElementTree(project).write(control/'Checks.csproj',encoding='utf-8')
    (control/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    run=subprocess.run(['dotnet','run','--project',str(control/'Checks.csproj'),'--nologo'],cwd=ROOT,capture_output=True,text=True)
    (control/'run.log').write_text(run.stdout+run.stderr);print((run.stdout+run.stderr)[-3800:],flush=True)
    match=re.search(r'CHECKS (\d+) FAILURES 0',run.stdout);check(run.returncode==0 and match is not None,'source-extracted scheduling cancellation/stale-input controls '+label)
    if match:checks+=int(match.group(1))
base_receipt=json.loads((baseline/'receipt.json').read_text());check(base_receipt['status']=='PASS','baseline integrated compile accepted')
for part in('runtime','editor'):
    tree=ET.parse(baseline/part/'Compile.csproj')
    for node in tree.findall('.//Compile'):
        p=Path(node.attrib['Include']);inputs[str(p)]=sha(p)
        if str(p) in overlays:node.set('Include',str(overlays[str(p)]))
    for node in tree.findall('.//Reference/HintPath'):
        p=Path(node.text);inputs[str(p)]=sha(p)
    for node in tree.findall('.//ProjectReference'):
        if Path(node.attrib['Include']).name=='Compile.csproj':node.set('Include',str(out/'runtime/Compile.csproj'))
    folder=out/part;folder.mkdir();tree.write(folder/'Compile.csproj',encoding='utf-8',xml_declaration=True)
    shutil.copyfile(baseline/part/'NuGet.Config',folder/'NuGet.Config')
run=subprocess.run(['dotnet','build',str(out/'editor/Compile.csproj'),'--nologo','-v:q'],cwd=ROOT,capture_output=True,text=True)
(out/'compile.log').write_text(run.stdout+run.stderr);print((run.stdout+run.stderr)[-2300:],flush=True)
check(run.returncode==0,'complete proposal runtime/editor Unity-reference compile')
changed=[p for p,h in inputs.items() if not Path(p).exists() or sha(Path(p))!=h];check(not changed,'all source/proposal/reference inputs unchanged at completion')
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not failures else 'FAIL',checks=checks,failures=len(failures),failure_names=failures,changed_inputs=changed,input_sha256=inputs,runtime_sources=base_receipt['runtime_sources'],editor_sources=base_receipt['editor_sources'],evidence='DETACHED_PROPOSAL_COMPILE_SOURCE_EQUIVALENCE_AND_SOURCE_EXTRACTED_SCHEDULER; final physical/profile proof owners and native profiler substituted in scheduling controls; no native performance or gameplay acceptance')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps({k:v for k,v in receipt.items() if k!='input_sha256'}),flush=True)
raise SystemExit(bool(failures))
