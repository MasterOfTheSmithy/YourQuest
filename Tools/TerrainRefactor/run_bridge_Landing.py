"""Source-bound managed bridge regression using compiled production terrain/cell owners; no Unity/model/save writes."""
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[2]
compiled = (root / sys.argv[1]).resolve()
output = (root / sys.argv[2]).resolve()
receipt = json.loads((compiled / 'receipt.json').read_text())
assert receipt['status'] == 'PASS' and receipt['inputs_unchanged_at_completion']
assert output.is_relative_to(root / 'outputs/TerrainRefactor_20261013_BridgeLanding') and not output.exists()
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
inputs = {root / path: value for path, value in receipt['source_sha256'].items()}
assert all(digest(path) == value for path, value in inputs.items())
feature = (root / 'Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs').read_text()
env = (root / 'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs').read_text()
def section(text, start, end):
    return text[text.index(start):text.index(end, text.index(start))]
def declaration(text, start):
    opening = text.index('{', text.index(start)); depth = 1; position = opening + 1
    while depth:
        depth += (text[position] == '{') - (text[position] == '}'); position += 1
    return text[text.index(start):position] + '\n'

# note: Extract production geometry unchanged. Only unavailable Terrain/Time APIs and internal method accessibility receive explicit adapters below.
pure = 'using System; using System.Collections; using System.Collections.Generic; using System.Runtime.CompilerServices; using UnityEngine;\npublic static partial class BridgeLandingChecks {\n'
pure += section(feature, '    private sealed class AcceptedCrossingSpan', '    internal static IEnumerator PrepareAcceptedBridgeRoutesRoutine(')
pure += section(feature, '    private static bool IsDryBridgeBank(', '    private static bool CrossingDecksConflict(')
pure += section(feature, '    private static Vector3 SampleCrossingRoute(', '    private static void PrepareWildernessLandmark(')
pure += section(feature, '    private static bool TryBuildCrossingSpan(', '    private static float DistanceToWaterFeatureSquared(')
pure += section(feature, '    private static float DistanceToWaterFeatureSquared(', '    private static Vector3 SampleCrossingRoute(')
pure += section(feature, '    internal static List<Vector3> BuildNativeBridgeDeckProfile(', '    private static IEnumerator FitBridgeBankPathsToTerrainRoutine(')
pure += declaration(feature, '    private static bool IsFinitePoint(')
pure += declaration(env, '    private sealed class LivedPathSegment')
pure += section(env, '    internal static List<Vector2> GetAcceptedRouteCenterline(', '    private static void BuildV2LivedPathNetwork(')
start = env.index('    private static void BuildV2LivedPathNetwork(')
end = env.index('        if (!includeSiteApproaches) return;', start) + len('        if (!includeSiteApproaches) return;')
pure += env[start:end] + '\n    }\n'
pure += section(env, '    private static void AddV2LivedPathLeg(', '    private static GeneratedSettlementRecord FindProceduralSettlement(')
pure += section(env, '    private static void ApplyLocalV2RouteGate(', '    private static Vector2 ResolveLiveRouteEndpoint(')
pure = pure.replace('YQContinuousWorldFeatureMaterializer.UsesNativeBridgeRoutes(prepared)', 'UsesNativeBridgeRoutes(prepared)')
pure = pure.replace('YQGeneratedWorldEnvironment.GetAcceptedRouteCenterline(prepared,', 'GetAcceptedRouteCenterline(prepared,')
pure = pure.replace('YQContinuousWorldCellAuthority.CreateAcceptedFeatureHeightProjection(', 'ProjectionFactory(')
pure = pure.replace('Terrain terrain', 'TerrainDatum terrain').replace('Terrain authored', 'TerrainDatum authored')
pure = pure.replace('Time.realtimeSinceStartup', '0f')
pure = pure.replace('YQGeneratedRiverBridge.WalkSurfaceTopOffset', '(0.08f+0.28f*0.5f)')
pure = pure.replace('YQGeneratedRiverBridge.CooperativeSliceSeconds', '0.004f')
pure = pure.replace('YQNativeBridgeTread.FitProfile(', 'FitTread(')
pure = pure.replace('YQStartupLoadingScreen.SetGenerationSubstep(', 'ReportSubstep(')
pure = pure.replace('YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress()', 'ReportProgress()')

# note: Stage logs observe the existing fitter. Raising the detached maximum is a hypothesis, not a production modification.
pure=pure.replace('var floors = new float[profile.Count];',
    'RecordLanding("assembled",startExtension,endExtension,profile,Sample,firstCore); var floors = new float[profile.Count];')
pure=pure.replace('profile = ConstrainNativeBridgeDeckGrade(profile, prepared.GetRoute(span.routeIndex).maximumGradeDegrees);',
    'RecordLanding("after_floor",startExtension,endExtension,profile,Sample,firstCore); profile = ConstrainNativeBridgeDeckGrade(profile, prepared.GetRoute(span.routeIndex).maximumGradeDegrees); RecordLanding("after_centre_grade",startExtension,endExtension,profile,Sample,firstCore);')
pure=pure.replace('profile = FitTread(profile,span.width,prepared.GetRoute(span.routeIndex).maximumGradeDegrees);',
    'profile = FitTread(profile,span.width,prepared.GetRoute(span.routeIndex).maximumGradeDegrees); RecordLanding("after_triangle_grade",startExtension,endExtension,profile,Sample,firstCore);')
pure += '''
    private sealed class TerrainDatum {
        public TerrainDatum transform=>this; public TerrainDatum terrainData=>this;
        public Vector3 position=>new Vector3(-512,0,-512);public Vector3 size=>new Vector3(1024,140,1024);
        public Func<float,float,float> height; public readonly Dictionary<Vector2,int> sampleCalls=new Dictionary<Vector2,int>();
        public float SampleHeight(Vector3 point) {var key=new Vector2(point.x,point.z);sampleCalls.TryGetValue(key,out int calls);sampleCalls[key]=calls+1;return height(point.x,point.z)*140;}
    }
    private static List<Vector3> FitTread(IReadOnlyList<Vector3> source,float width,float grade)=>
        (List<Vector3>)typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread")
            .GetMethod("FitProfile",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{source,width,grade});
    private static void ReportSubstep(string value) { }
    private static void ReportProgress() { }
    private static bool IsLivedPathPotentiallyRelevant(Vector2 start,Vector2 end,float half,float shoulder,bool bounded,Vector3 pos,Vector3 size) {
        if(bounded)throw new InvalidOperationException("Probe excludes native terrain clipping.");return true;
    }
}\n'''
source = root / 'Tools/TerrainRefactor/BridgeLandingChecks.cs'
inputs[source] = digest(source)
fixture = root / 'outputs/TerrainRefactor_20261013_BridgeLanding/Failing_Plan.json'
inputs[fixture] = digest(fixture)
output.mkdir(parents=True)
(output / 'Extracted.cs').write_text(pure)
inputs[output / 'Extracted.cs'] = digest(output / 'Extracted.cs')
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for key, value in dict(TargetFramework='net10.0', OutputType='Exe', EnableDefaultCompileItems='false', NoWarn='0649').items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(project, 'ItemGroup')
for path in [source, output / 'Extracted.cs']:
    ET.SubElement(items, 'Compile', Include=str(path))
refs = [('Assembly-CSharp', compiled / 'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll')]
for node in ET.parse(compiled / 'runtime/Compile.csproj').findall('.//Reference'):
    path = Path(node.findtext('HintPath'))
    if path.name.startswith('UnityEngine.') or 'Newtonsoft' in path.name:
        refs.append((node.attrib['Include'], path))
for name, path in refs:
    node = ET.SubElement(items, 'Reference', Include=name)
    ET.SubElement(node, 'HintPath').text = str(path)
    inputs[path] = digest(path)
ET.ElementTree(project).write(output / 'Checks.csproj', encoding='utf-8')
(output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
build = subprocess.run(['dotnet', 'build', str(output / 'Checks.csproj'), '--nologo', '-v:q'], capture_output=True, text=True)
(output / 'compile.log').write_text(build.stdout + build.stderr)
run = None
if build.returncode == 0:
    process = subprocess.Popen(['dotnet', str(output / 'bin/Debug/net10.0/Checks.dll')], stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, text=True, cwd=root)
    lines = []
    with (output / 'run.log').open('w') as stream:
        for line in process.stdout:
            lines.append(line); stream.write(line); stream.flush(); print(line, end='', flush=True)
    process.wait()
    run = subprocess.CompletedProcess(process.args, process.returncode, ''.join(lines), '')
else:
    print(build.stdout + build.stderr)
summary = re.search(r'^RESULT checks=(\d+) failures=(\d+)\s*$', run.stdout, re.MULTILINE) if run else None
changed = [str(path) for path, value in inputs.items() if digest(path) != value]
checks = int(summary.group(1)) if summary else 0
failures = int(summary.group(2)) if summary else 1
code = build.returncode or (run.returncode if run else 1) or (2 if changed or not checks or failures else 0)
(output / 'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),
    status='PASS' if code == 0 else 'FAIL', checks=checks, failures=failures, changed_inputs=changed,
    input_sha256={str(path): value for path, value in inputs.items()},
    evidence='SOURCE_EXTRACTED_LANDING_DIAGNOSTIC; AUTHORED_TERRAIN_APPROXIMATED_WITH_FINITE_FEATURE_DATUM; NO_NATIVE_RASTER_OR_TRAVERSAL'), indent=2))
sys.exit(code)
