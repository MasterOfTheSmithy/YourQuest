"""Source-extracted 16-pass road projection caller: original vs native fixed-source guard proposal."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,re,sys
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261013_RouteRejection_3b9bbe22/fixed_grid_loop_current_checks'
assert not out.exists();out.mkdir()
path=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs';source=path.read_text()
preimage=root/'outputs/TerrainRefactor_20261013_RouteRejection_3b9bbe22/YQGeneratedWorldEnvironment.cs.before';before=preimage.read_text()
a=before.index('        const int maximumRoadGradePasses = 16;');b=before.index('        int nextDirtyRow = 0;',a);loop=before[a:b]
old='if (roadBlends[z, x] < .999f) continue;';new='if (roadBlends[z, x] < .999f || (preserveConnectedBasinGeometry && fixedRoadGrid[z, x])) continue;'
assert loop.count(old)==1
ca=source.index('        const int maximumRoadGradePasses = 16;');cb=source.index('        int nextDirtyRow = 0;',ca);proposal=source[ca:cb]
core=lambda text:re.sub(r'\s+','',re.sub(r'//[^\n]*','',text))
assert core(proposal)==core(loop.replace(old,new)), 'Unexpected caller executable change'
start=source.index('    private static float ProjectRoadGridEdge(');i=source.index('{',start)+1;d=1
while d:d+=(source[i]=='{')-(source[i]=='}');i+=1
helper=source[start:i]
oa=before.index('    private static float ProjectRoadGridEdge(');oi=before.index('{',oa)+1;d=1
while d:d+=(before[oi]=='{')-(before[oi]=='}');oi+=1
assert helper==before[oa:oi], 'Archived helper must remain identical'
(out/'Caller.preimage.cs').write_text(loop);(out/'Caller.proposal.cs').write_text(proposal);(out/'Helper.preimage.cs').write_text(helper)
stub='''using System;using System.Collections;
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public static class Mathf {public static float Abs(float x)=>Math.Abs(x);public static float Sign(float x)=>Math.Sign(x);public static float Max(float a,float b)=>Math.Max(a,b);}
public static class Time {public static float realtimeSinceStartup;}
public static class YQGeneratedWorldRuntimeBuilder {public static void ReportInitialGenerationProgress(){}}
'''
for name,body in (('Original',loop),('Candidate',proposal)):
    stub+='public static class '+name+'{\n'+helper+'\npublic static IEnumerator Solve(float[,] heights,float[,] roadBlends,float[,] roadMinimum,bool[,] fixedRoadGrid,bool preserveConnectedBasinGeometry){\n'
    stub+='int resolution=heights.GetLength(0);int[] dirtyMinimumXByRow=new int[resolution],dirtyMaximumXByRow=new int[resolution];for(int row=0;row<resolution;row++)dirtyMaximumXByRow[row]=resolution-1;float gridRiseX=.48f/140f,gridRiseZ=.48f/140f;var terrainSize=new Vector3(2*(resolution-1),140,2*(resolution-1));const float routeWorkSliceSeconds=.008f;\n'+body+'}\n}\n'
stub+='''
public static class Checks {
    static int checks,failures;
    static void Check(bool pass,string name){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);}
    static void Drain(IEnumerator it){try{while(it.MoveNext())if(it.Current is IEnumerator child)Drain(child);}finally{(it as IDisposable)?.Dispose();}}
    static bool Equal(float[,] a,float[,] b){for(int z=0;z<a.GetLength(0);z++)for(int x=0;x<a.GetLength(1);x++)if(a[z,x]!=b[z,x])return false;return true;}
    static float[,] Grid(int n,float h){var v=new float[n,n];for(int z=0;z<n;z++)for(int x=0;x<n;x++)v[z,x]=h;return v;}
    static void Main(){
        const float apron=65.354515f/140f,foundation=74f/140f;
        var blends=Grid(3,1);var floor=Grid(3,0);var flags=new bool[3,3];var initial=Grid(3,apron);
        flags[1,0]=true;flags[1,1]=true;initial[1,1]=foundation;
        var original=(float[,])initial.Clone();var candidate=(float[,])initial.Clone();var archive=(float[,])initial.Clone();
        Drain(Original.Solve(original,blends,floor,flags,true));Drain(Candidate.Solve(candidate,blends,floor,flags,true));Drain(Candidate.Solve(archive,blends,floor,flags,false));
        Check(original[1,0]!=apron,"baseline16pass loop corrupts locked apron beside locked foundation");
        Check(original[1,1]!=foundation,"baseline16pass loop corrupts locked foundation in reverse edge visits");
        Check(candidate[1,0]==apron&&candidate[1,1]==foundation,"native caller guard preserves BOTH exact pinned datums");
        Check(Equal(original,archive),"archive full16pass array bit-identical to baseline");
        Check((candidate[1,1]-candidate[1,0])*140>8,"incompatible adjacent fixed datums remain visibly steep for physical rejection");
        // note: A water minimum contradicting a fixed datum must not secretly rewrite that fixed height.
        floor[1,0]=80f/140;var lifted=(float[,])initial.Clone();var locked=(float[,])initial.Clone();
        Drain(Original.Solve(lifted,blends,floor,flags,true));Drain(Candidate.Solve(locked,blends,floor,flags,true));
        Check(lifted[1,0]!=apron,"baseline direct roadMinimum lift changes locked apron");
        Check(locked[1,0]==apron&&locked[1,1]==foundation,"native guard precedes BOTH floor lift and edge projection");
        Check(locked[1,0]<floor[1,0],"incompatible fixed waterminimum remains explicit in data");
        // note: Free banks converge to adjacent equal pinned datums under the unchanged bounded solver.
        var surround=new bool[3,3];for(int z=0;z<3;z++)for(int x=0;x<3;x++)surround[z,x]=(x!=1||z!=1);
        var free=Grid(3,apron);free[1,1]=foundation;Drain(Candidate.Solve(free,blends,Grid(3,0),surround,true));
        Check(free[1,1]!=foundation&&Math.Abs(free[1,1]-apron)*140<=.48001,"free bank adjusts to fixed neighbors and reaches grade bound");
        bool pins=true;for(int z=0;z<3;z++)for(int x=0;x<3;x++)if(surround[z,x]&&free[z,x]!=apron)pins=false;
        Check(pins,"all perimeter pins remain bit-identical throughout free bank convergence");
        var emptyFlags=new bool[3,3];var allFree=Grid(3,apron);allFree[1,1]=foundation;
        var baselineFree=(float[,])allFree.Clone();Drain(Original.Solve(baselineFree,blends,Grid(3,0),emptyFlags,true));Drain(Candidate.Solve(allFree,blends,Grid(3,0),emptyFlags,true));
        Check(Equal(allFree,baselineFree),"free/free grid behavior exactly unchanged");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");Environment.ExitCode=failures==0?0:1;
    }
}
'''
(out/'Checks.cs').write_text(stub);(out/'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Checks.cs"/></ItemGroup></Project>');(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (path,preimage,Path(__file__))}
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True);(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if build.returncode==0 else None
text=run.stdout+run.stderr if run else build.stdout+build.stderr;(out/'run.log').write_text(text);print(text)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',text);checks=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h];exitcode=build.returncode or (run.returncode if run else 1) or bool(changed or not checks or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not exitcode else 'FAIL',checks=checks,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_CURRENT16PASS_CALLER_VS_EXACT_PREIMAGE_AND_UNCHANGED_HELPER; CONTROLLED_GRID; NOT_NATIVE_ROUTE_ACCEPTANCE'),indent=2));sys.exit(exitcode)
