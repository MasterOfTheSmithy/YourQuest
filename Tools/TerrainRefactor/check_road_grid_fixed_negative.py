"""Compare source-extracted grid projection with a native fixed-source arithmetic proposal; no production edits."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,re,sys
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261013_RouteRejection_3b9bbe22/fixed_grid_negative'
assert not out.exists();out.mkdir()
path=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs';source=path.read_text()
a=source.index('    private static float ProjectRoadGridEdge(');i=source.index('{',a)+1;d=1
while d:d+=(source[i]=='{')-(source[i]=='}');i+=1
original=source[a:i];(out/'ProjectRoadGridEdge.preimage.cs').write_text(original)
old='''if (fixedGrid[bz, bx]) heights[az, ax] += correction;
        else { heights[az, ax] += correction * .5f; heights[bz, bx] -= correction * .5f; }'''
assert original.count(old)==1
candidate=original.replace('int ax, int az, int bx, int bz, float rise)','int ax, int az, int bx, int bz, float rise, bool preserveFixedSource)')
candidate=candidate.replace(old,'''// note: Native pinned source heights are immutable too; unresolved fixed/fixed constraints retain a real residual.
        if (preserveFixedSource && fixedGrid[az, ax])
        {
            if (!fixedGrid[bz, bx]) heights[bz, bx] -= correction;
        }
        else if (fixedGrid[bz, bx]) heights[az, ax] += correction;
        else { heights[az, ax] += correction * .5f; heights[bz, bx] -= correction * .5f; }''')
(out/'ProjectRoadGridEdge.proposal.cs').write_text(candidate)
code='using System;\npublic static class Mathf {public static float Abs(float x)=>Math.Abs(x);public static float Sign(float x)=>Math.Sign(x);}\n'
for name,text in (('Original',original),('Proposal',candidate)):
    code+='public static class '+name+'{\n'+text.replace('private static float','public static float',1)+'\n}\n'
code+='''
public static class Checks {
    static int checks,failures;
    static void Check(bool pass,string name){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);}
    static float[,] Heights()=>new float[,]{{65.354515f,74f}};
    static void Main(){
        var blends=new float[,]{{1,1}};
        foreach(bool af in new[]{false,true})foreach(bool bf in new[]{false,true}){
            var flags=new bool[,]{{af,bf}};var original=Heights();var proposal=Heights();var archived=Heights();
            float beforeA=original[0,0],beforeB=original[0,1];
            var oldResidual=Original.ProjectRoadGridEdge(original,blends,flags,0,0,1,0,.48f);
            var nativeResidual=Proposal.ProjectRoadGridEdge(proposal,blends,flags,0,0,1,0,.48f,true);
            var archiveResidual=Proposal.ProjectRoadGridEdge(archived,blends,flags,0,0,1,0,.48f,false);
            string kind=$"fixedA={af} fixedB={bf}";
            Check(archived[0,0]==original[0,0]&&archived[0,1]==original[0,1]&&oldResidual==archiveResidual,kind+" archived arithmetic unchanged");
            Check(!af||proposal[0,0]==beforeA,kind+" native source fixed invariant");
            Check(!bf||proposal[0,1]==beforeB,kind+" native neighbor fixed invariant");
            Check(af&&bf?nativeResidual>8f&&Math.Abs(proposal[0,1]-proposal[0,0])>8f:Math.Abs(proposal[0,1]-proposal[0,0])<=.48001f,kind+" native conflict remains visible or free nodes meet bound");
            if(af)Check(original[0,0]!=beforeA,kind+" original actually corrupts fixed apron datum");
        }
        var same=new float[,]{{65,65.2f}};var allFixed=new bool[,]{{true,true}};
        Check(Proposal.ProjectRoadGridEdge(same,blends,allFixed,0,0,1,0,.48f,true)==0&&same[0,0]==65&&same[0,1]==65.2f,"already satisfied pinned neighbors stay unchanged");
        var shoulder=new float[,]{{1,.5f}};var offroad=Heights();
        Check(Proposal.ProjectRoadGridEdge(offroad,shoulder,new bool[,]{{true,false}},0,0,1,0,.48f,true)==0&&offroad[0,0]==65.354515f&&offroad[0,1]==74,"off-road relief retains existing exclusion");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");Environment.ExitCode=failures==0?0:1;
    }
}
'''
(out/'Checks.cs').write_text(code);(out/'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Checks.cs"/></ItemGroup></Project>');(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (path,Path(__file__))}
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True);(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if build.returncode==0 else None
text=run.stdout+run.stderr if run else build.stdout+build.stderr;(out/'run.log').write_text(text);print(text)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',text);checks=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h];exitcode=build.returncode or (run.returncode if run else 1) or bool(changed or not checks or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not exitcode else 'FAIL',checks=checks,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_FIXED_GRID_NEGATIVE_AND_PROPOSED_NATIVE_ARITHMETIC; NOT_CURRENT_REPAIR_OR_NATIVE_ROUTE_ACCEPTANCE'),indent=2));sys.exit(exitcode)
