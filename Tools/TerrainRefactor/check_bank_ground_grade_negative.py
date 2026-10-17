"""Execute unchanged bank A* on controlled dry terrain, then inspect grounded ribbon triangle slopes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,subprocess,sys,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_BankGrade/controlled_negative'
assert not out.exists();out.mkdir(parents=True)
feature=root/'Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs'
env=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs'
source=feature.read_text();a=source.index('    private static IEnumerator BuildDryBankConnectionRoutine(');i=source.index('{',a)+1;d=1
while d:d+=(source[i]=='{')-(source[i]=='}');i+=1
method=source[a:i].replace('private static IEnumerator','public static IEnumerator',1)
compile_refs=root/'outputs/TerrainRefactor_20261013_BankTraversal/compile_tread_safe_contacts/runtime/Compile.csproj'
refs=ET.parse(compile_refs).findall('.//Reference')
engine=next(Path(r.findtext('HintPath')) for r in refs if Path(r.findtext('HintPath')).name=='UnityEngine.CoreModule.dll')
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (feature,env,Path(__file__),engine)}
assert 'if (!SegmentDry(point, Point(next))) continue;' in method
assert 'maximumGrade' not in method and 'SampleTerrain' not in method[method.index('bool SegmentDry('):method.index('Vector2 Point(')]
(out/'Checks.cs').write_text('using System;using System.Collections;using System.Collections.Generic;using UnityEngine;\n'+'''
public sealed class YQPreparedSpatialMaterializationV2 {
    public readonly Func<float,float,float> Height;
    public YQPreparedSpatialMaterializationV2(Func<float,float,float> height){Height=height;}
    public Sample SampleTerrain(float x,float z)=>new Sample{elevationNormalized=Height(x,z)/140f};
}
public struct Sample {public float elevationNormalized;}
// note: Controlled cases are entirely dry and outside landmark reservations; no water or landmark admission is being certified.
public static class YQGeneratedWorldLayout {
    public static bool TryResolveDryGoddessSummit(YQPreparedSpatialMaterializationV2 p,out Vector3 summit){summit=new Vector3(-10000,0,-10000);return true;}
    public static bool IsOriginLandmarkRouteCorridorClear(Vector2 a,Vector2 b,float width,Vector2 center)=>true;
}
public static class YQGeneratedRiverBridge {public const float CooperativeSliceSeconds=.002f;}
public static class Time {public static float realtimeSinceStartup;}
public static class ExtractedBank {
    private static bool UsesNativeBridgeRoutes(YQPreparedSpatialMaterializationV2 p)=>true;
    private static bool IsDryBridgeBank(YQPreparedSpatialMaterializationV2 p,Vector2 x,float width)=>true;
'''+method+'''
}
public static class Checks {
    static int checks,failures;
    static void Check(bool pass,string message){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+message);}
    static void Drain(IEnumerator it){try{while(it.MoveNext())if(it.Current is IEnumerator child)Drain(child);}finally{(it as IDisposable)?.Dispose();}}
    // note: This is the exact rectangular triangle ordering used by BuildGroundedRoadRibbon's Emit(al,bl,ar) and Emit(ar,bl,br).
    static float GroundedTriangleSlope(Vector3 a,Vector3 b,Vector3 c)=>Mathf.Acos(Mathf.Clamp(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y),0,1))*Mathf.Rad2Deg;
    static void Case(string name,Func<float,float,float> height,bool expectSteep){
        var prepared=new YQPreparedSpatialMaterializationV2(height);List<Vector3> path=null;
        Drain(ExtractedBank.BuildDryBankConnectionRoutine(prepared,Vector3.zero,new Vector3(16,0,0),5.2f,0,140,p=>path=p));
        Check(path!=null&&path.Count>=2,name+" unchanged A* admits a dry connector");
        float length=0,maximum=0;
        for(int k=1;k<path.Count;k++){
            var a=path[k-1];var b=path[k];var delta=new Vector3(b.x-a.x,0,b.z-a.z);length+=delta.magnitude;
            var side=Vector3.Cross(Vector3.up,delta.normalized);int rows=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude));
            for(int row=0;row<rows;row++){
                var p=Vector3.Lerp(a,b,row/(float)rows);var q=Vector3.Lerp(a,b,(row+1)/(float)rows);
                var al=p-side*2.6f;var ar=p+side*2.6f;var bl=q-side*2.6f;var br=q+side*2.6f;
                al.y=height(al.x,al.z)+.025f;ar.y=height(ar.x,ar.z)+.025f;bl.y=height(bl.x,bl.z)+.025f;br.y=height(br.x,br.z)+.025f;
                maximum=Mathf.Max(maximum,GroundedTriangleSlope(al,bl,ar),GroundedTriangleSlope(ar,bl,br));
            }
        }
        Check(Mathf.Abs(length-16)<.001f,name+" chooses shortest XZ edge irrespective of height");
        Check(expectSteep?maximum>58:maximum<=20.001f,name+" grounded triangles "+maximum.ToString("F3")+" degrees");
        Console.WriteLine("CASE "+name+" length="+length+" maxTriangleSlope="+maximum);
    }
    public static void Main(){
        Case("dry6mCliff",(x,z)=>x<=6?0:x>=8?6:3*(x-6),true);
        Case("smooth20deg",(x,z)=>Mathf.Tan(20*Mathf.Deg2Rad)*x,false);
        Case("flat",(x,z)=>10,false);
        Case("crossSlope65deg",(x,z)=>Mathf.Tan(65*Mathf.Deg2Rad)*z,true);
        Console.WriteLine($"RESULT checks={checks} failures={failures}");Environment.ExitCode=failures==0?0:1;
    }
}
''')
(out/'Checks.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Reference Include="UnityEngine.CoreModule"><HintPath>{engine}</HintPath></Reference></ItemGroup></Project>')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True)
(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if build.returncode==0 else None
text=run.stdout+run.stderr if run else build.stdout+build.stderr
(out/'run.log').write_text(text);print(text)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',text);checks=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h]
code=build.returncode or (run.returncode if run else 1) or bool(changed or not checks or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=checks,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_CURRENT_ASTAR_CONTROLLED_DRY_TERRAIN_NEGATIVE; RIBBON_RECTANGULAR_TRIANGLE_MODEL; WATER/LANDMARK/NATIVE_HEIGHT/WEAR_SUBSTITUTED; NOT_REPAIR_OR_NATIVE_ACCEPTANCE'),indent=2))
sys.exit(code)
