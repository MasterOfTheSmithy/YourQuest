"""Source-extracted support math with explicitly substituted native physics; no Unity/player/profile writes."""
from pathlib import Path
import hashlib,json,subprocess,sys,re,xml.etree.ElementTree as ET
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2];out=root/sys.argv[1];assert out.is_relative_to(root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint') and not out.exists();out.mkdir(parents=True)
path=root/'Assets/Assets/Scripts/Generated/YQRouteTraversalProbe.cs';current=path.read_text();digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inputs={path:digest(path)}
preimage=root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint/CentreReference_Preimage.cs'
expected=root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint/CentreReference_Expected.cs'
applied=root/'outputs/TerrainRefactor_20261013_BankTraversal/footprint/CentreReference_Applied.cs'
applied_source=applied.read_text()
source=preimage.read_text();candidate=expected.read_text();inputs.update({preimage:digest(preimage),expected:digest(expected),Path(__file__):digest(Path(__file__)),applied:digest(applied)})
if current not in (source,candidate,applied_source):
    (out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='FAIL',checks=0,failures=1,source_state='UNEXPECTED_SOURCE',input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_IDENTITY_REJECTED; NO_TEST_EXECUTION'),indent=2))
    raise AssertionError('Unexpected production source; explicit reference/preimage refresh and review required.')
source_state='APPLIED_EXACT_CENTRE_REFERENCE' if current in (candidate,applied_source) else 'ORIGINAL_EXACT_PREIMAGE'
def declaration(text,start):
    a=text.index(start);b=text.index('{',a);i=b+1;depth=1
    while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[a:i]
capsule=declaration(source,'        public void Capsule(')
def cls(name,text):
    enum=declaration(text,'    public enum Issue');result=declaration(text,'    public readonly struct Result');measure=declaration(text,'    public Result Measure(');support=declaration(text,'    private bool TrySupport(')
    finite=text[text.index('    private static bool Finite(Vector3 point)'):text.index('    private bool Ignore(')]
    value='public sealed class '+name+' {\n'+enum+'\n'+result+'\n'+measure+'\n'+support+'\n'+finite+'''
    private readonly TestScene scene;private readonly ProbeShape shape=new ProbeShape();
    private readonly RaycastHit[] hits=new RaycastHit[64];private readonly Collider[] overlaps=new Collider[64];
    public NAME(TestScene scene){this.scene=scene;}
    private bool Ignore(Collider c)=>false;
}\n'''.replace('NAME',name)
    return value.replace('RaycastHit','TestHit').replace('Collider','TestCollider')
bindings='using System;using UnityEngine;\n'+cls('OriginalProbe',source)+cls('CandidateProbe',candidate)+cls('CurrentProbe',current)+'public static class VerifiedSource { public const bool CurrentIsApplied='+('true' if current in (candidate,applied_source) else 'false')+';}\n'+'''
public sealed class ProbeShape {
    public object controller=new object();public float radius=.38f,height=1.8f,skin=.08f,step=.52f,slope=58f;
'''+capsule+'''
}
public sealed class TestCollider {}
public struct TestHit {public Vector3 point,normal;public TestCollider collider;public float distance;}
public sealed class TestScene {
    public Func<float,float,float?> height;public Vector3 normal=Vector3.up;public bool occupied,swept,saturated;
    private readonly TestCollider floor=new TestCollider(),obstacle=new TestCollider();
    public int Raycast(Vector3 origin,Vector3 direction,TestHit[] hits,float distance,int layers,QueryTriggerInteraction trigger) {
        if(saturated)return hits.Length;float? h=height(origin.x,origin.z);if(!h.HasValue||h>origin.y||h<origin.y-distance)return 0;
        hits[0]=new TestHit{point=new Vector3(origin.x,h.Value,origin.z),normal=normal,collider=floor,distance=origin.y-h.Value};return 1;
    }
    public int OverlapCapsule(Vector3 a,Vector3 b,float radius,TestCollider[] values,int layers,QueryTriggerInteraction trigger) {
        if(!occupied)return 0;values[0]=obstacle;return 1;
    }
    public int CapsuleCast(Vector3 a,Vector3 b,float radius,Vector3 direction,TestHit[] values,float distance,int layers,QueryTriggerInteraction trigger) {
        if(!swept)return 0;values[0]=new TestHit{collider=obstacle,point=a,distance=distance*.5f};return 1;
    }
}
'''
(out/'Extracted.cs').write_text(bindings);inputs[out/'Extracted.cs']=digest(out/'Extracted.cs')
tests=root/'Tools/TerrainRefactor/FootprintProbeChecks.cs';inputs[tests]=digest(tests)
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(TargetFramework='net10.0',OutputType='Exe',EnableDefaultCompileItems='false').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup')
for p in [tests,out/'Extracted.cs']:ET.SubElement(items,'Compile',Include=str(p))
compiled=root/'outputs/TerrainRefactor_20261013_BankDiscovery/compile_shared_route'
for n in ET.parse(compiled/'runtime/Compile.csproj').findall('.//Reference'):
    p=Path(n.findtext('HintPath'))
    if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:
        node=ET.SubElement(items,'Reference',Include=n.attrib['Include']);ET.SubElement(node,'HintPath').text=str(p);inputs[p]=digest(p)
ET.ElementTree(project).write(out/'Checks.csproj',encoding='utf-8');(out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
b=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True);(out/'compile.log').write_text(b.stdout+b.stderr)
if b.returncode:print(b.stdout+b.stderr);sys.exit(b.returncode)
r=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True);(out/'run.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr)
summary=re.search(r'^RESULT checks=(\d+) failures=(\d+)\s*$',r.stdout,re.MULTILINE)
checks=int(summary.group(1)) if summary else 0;failures=int(summary.group(2)) if summary else 1
changed=[str(p) for p,h in inputs.items() if digest(p)!=h]
code=r.returncode or (2 if not checks or failures or changed else 0)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if code==0 else 'FAIL',checks=checks,failures=failures,exit_code=code,source_state=source_state,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_SAVED_PREIMAGE_VS_EXACT_EXPECTED_CANDIDATE_VS_CURRENT; NATIVE_PHYSICS_SUBSTITUTED; NOT_RUNTIME_ACCEPTANCE'),indent=2));sys.exit(code)
