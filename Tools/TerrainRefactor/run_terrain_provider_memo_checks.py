"""Exact-source compatibility memo equivalence; native JSON parsing explicitly substituted."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--out',required=True);args=ap.parse_args()
out=(root/args.out).resolve()
if not out.is_relative_to(root/'outputs') or out.exists():ap.error('--out must be a fresh outputs folder')
builder=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs'
terrain=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldTerrain.cs'
before=root/'outputs/TerrainRefactor_20261014_StreamingRetest/TerrainProviderCompatibilityMemo/YQGeneratedWorldRuntimeBuilder.cs.before'
checks=root/'Tools/TerrainRefactor/TerrainProviderMemoChecks.cs'
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (builder,terrain,before,checks,Path(__file__))}
source=builder.read_text();old=before.read_text()
def method(text,marker):
    a=text.index(marker);i=text.index('{',a)+1;depth=1
    while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[a:i]
predicate=method(source,'    private static bool IsCompatibleProfileTerrainSnapshot(')
assert predicate==method(old,'    private static bool IsCompatibleProfileTerrainSnapshot('),'Original predicate changed'
provider=method(source,'    private string CreateProfileTerrainSnapshotForCommit(')
oldProvider=method(old,'    private string CreateProfileTerrainSnapshotForCommit(')
assert provider==oldProvider.replace('IsCompatibleProfileTerrainSnapshot(','IsCompatibleCachedProfileTerrainSnapshot('),'Provider changed beyond exact memo call adapters'
assert provider.count('IsCompatibleCachedProfileTerrainSnapshot(')==2
start=source.index('    private sealed class ProfileTerrainCompatibilityMemo')
end=source.index('    // note: Carry terrain forward only',start)
memo=source[start:end].replace('private bool IsCompatibleCachedProfileTerrainSnapshot','internal bool IsCompatibleCachedProfileTerrainSnapshot',1)
proxy='using System;\npublic sealed class MemoProbe {\n'+memo+predicate.replace('private static bool','internal static bool',1)+'}\n'
record=method(terrain.read_text(),'    public sealed class ProfileTerrainSnapshotRecord')
proxy+='public static class YQGeneratedWorldTerrain {\n'+record+'}\n'
out.mkdir(parents=True);(out/'Extracted.cs').write_text(proxy)
(out/'Checks.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Extracted.cs"/><Compile Include="{checks}"/></ItemGroup></Project>')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True)
(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if not build.returncode else None
log=(run.stdout+run.stderr) if run else build.stdout+build.stderr
(out/'run.log').write_text(log);print(log)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',log);count=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h]
code=build.returncode or (run.returncode if run else 1) or bool(changed or not count or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=count,failures=failures,changed_inputs=changed,original_predicate_unchanged=True,provider_changes='exact two compatibility call adapters only',input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_CURRENT_MEMO_AND_UNCHANGED_ORIGINAL_PREDICATE; FULL_TERRAIN_RECORD_FIELDS; SYSTEM_TEXT_JSON_SUBSTITUTES_NATIVE_JSONUTILITY; NO_UNITY_OR_NATIVE_PERFORMANCE_CLAIM'),indent=2))
sys.exit(code)
