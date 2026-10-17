"""Run source-extracted iterator ownership controls with native lifecycle explicitly substituted."""
from pathlib import Path
import argparse,hashlib,json,subprocess,re,sys
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out',required=True,help='Fresh directory beneath workspace outputs; existing evidence is never overwritten.')
parser.add_argument('--catalog',default='Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs',help='Catalog source; proposal verification requires an explicit alternate path.')
parser.add_argument('--feature',default='Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs',help='Feature source; proposal verification requires an explicit alternate path.')
args=parser.parse_args()
output=(root/args.out).resolve()
if not output.is_relative_to((root/'outputs').resolve()) or output.exists():
    parser.error('--out must be a fresh directory beneath workspace outputs')
catalog=(root/args.catalog).resolve();feature=(root/args.feature).resolve()
for path in (catalog,feature):
    if not path.is_relative_to(root) or not path.is_file():
        parser.error('Source must be an existing workspace file: '+str(path))
current_catalog=root/'Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs'
current_feature=root/'Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs'
source_state='CURRENT_PRODUCTION' if catalog==current_catalog and feature==current_feature else 'EXPLICIT_ALTERNATE_SOURCE'
output.mkdir(parents=True)
test=root/'Tools/TerrainRefactor/SiteStagingChecks.cs'
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (catalog,feature,test,Path(__file__))}
source=catalog.read_text()
def method(start,text=None):
    global source
    source0=source
    if text is not None:source=text
    a=source.index(start);i=source.index('{',a)+1;d=1
    while d:d+=(source[i]=='{')-(source[i]=='}');i+=1
    result=source[a:i];source=source0;return result
extracted='using System;using System.Collections;using System.Collections.Generic;\npublic partial class YQCompiledWorldSiteInstance {\nprivate static readonly object StagedSiteGeometryIssuer = new object();\n'
for name in ('    internal sealed class StagedSiteGeometryContext','    internal static IEnumerator EnsureSiteGeometryLoadedForStagingRoutine(',
    '    internal static bool TryGetContinuationGeometryContext(string locationId, string expectedKitId,\n        string expectedCompositionSeed, StagedSiteGeometryContext',
    '    internal static bool TryGetContinuationGeometryContext(string locationId, string expectedKitId,\n        string expectedCompositionSeed, out',
    '    private static bool TryGetContinuationGeometryBinding(', '    private static IEnumerator GuardStagedSiteExecutionRoutine(',
    '    private IEnumerator LoadPreparedSiteDetachedRoutine(', '    private static string DescribeUnavailableSite(', '    public static IEnumerator EnsureSiteLoadedRoutine(', '    private static bool IsCurrentPreparedSite(', '    public static bool IsSiteLoaded('):extracted+=method(name)+'\n'
extracted+='}\n'
builder=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs';inputs[builder]=hashlib.sha256(builder.read_bytes()).hexdigest()
extracted+='public static class YQGeneratedWorldRuntimeBuilder {\n'+method('    public static IEnumerator RunOwnedGenerationRoutine(',builder.read_text())+'}\n'
qualifier=method('    private static bool TryGetAcceptedContinuationLoadedProvider(',feature.read_text()).replace('private static bool','internal static bool',1)
extracted+='public partial class FeatureQualification {\n'+qualifier+'}\n'
(output/'Extracted.cs').write_text(extracted)
(output/'Checks.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Extracted.cs"/><Compile Include="{test}"/></ItemGroup></Project>''')
(output/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(output/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True)
(output/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(output/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if build.returncode==0 else None
text=(run.stdout+run.stderr) if run else build.stdout+build.stderr
(output/'run.log').write_text(text);print(text)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',text)
checks=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h]
code=build.returncode or (run.returncode if run else 1) or bool(changed or not checks or failures)
(output/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=checks,failures=failures,changed_inputs=changed,source_state=source_state,catalog_source=str(catalog),feature_source=str(feature),input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_'+source_state+'_STRUCTURAL_OWNERSHIP_ITERATORS; NATIVE_GAMEOBJECT/COROUTINE_AND_LOAD_WORK_SUBSTITUTED; NO_UNITY_OR_POPULATION_FAILURE_REPRODUCTION'),indent=2))
sys.exit(code)
