"""Read immutable captured plan with compiled numeric owners; no Unity/native terrain or profile writes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,sys,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261013_RouteRejection_3b9bbe22/bank_numeric_query'
assert not out.exists();out.mkdir()
compiled=root/'outputs/TerrainRefactor_20261013_BankTraversal/compile_stream_site_paths'
receipt=json.loads((compiled/'receipt.json').read_text());assert receipt['status']=='PASS' and receipt['inputs_unchanged_at_completion']
inputs={root/p:h for p,h in receipt['source_sha256'].items()}
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert all(digest(p)==h for p,h in inputs.items())
numeric=root/'Tools/TerrainRefactor/BridgeNativeCurrentGeometryChecks.cs'
fixture=root/'outputs/TerrainRefactor_20261013_RouteRejection_3b9bbe22/Failing_Plan.json'
inputs.update({p:digest(p) for p in (numeric,fixture,Path(__file__))})
source=numeric.read_text().split('    private static List<Vector3> lastProfile;')[0].replace('BridgeNativeCurrentGeometryChecks','BankGradeQuery')
source+='''
    public static void Main(string[] args){
        var settings=new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,Converters={new Vector3JsonConverter(),new Vector2JsonConverter(),new QuaternionJsonConverter()}};
        string json=File.ReadAllText(args[0]);var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(json,settings);
        if(plan.worldSeed!="3b9bbe22"||plan.spatialPlanV2.contentHash!="c31627996b126cb6")throw new Exception("Wrong fixture");
        string before=JsonConvert.SerializeObject(plan,settings);var prepared=NumericProjection(plan);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        object[] summitArgs={prepared,null};bool summitReady=(bool)typeof(YQGeneratedWorldLayout).GetMethod("TryResolveDryGoddessSummit",flags).Invoke(null,summitArgs);
        var summit=(Vector3)summitArgs[1];Console.WriteLine("SUMMIT "+summitReady+" "+JsonConvert.SerializeObject(summit,settings));
        var projection=(Func<float,float,float>)typeof(YQContinuousWorldCellAuthority).GetMethod("CreateAcceptedFeatureHeightProjection",flags).Invoke(null,new object[]{prepared,140f});
        var points=new[]{new Vector2(49.36f,7.94f),new Vector2(49.11f,8.19f),new Vector2(48.86f,8.44f),new Vector2(48.61f,8.69f),new Vector2(48.36f,8.94f),new Vector2(48.11f,9.19f),new Vector2(47.86f,9.44f),new Vector2(47.61f,9.69f),new Vector2(46,6)};
        var samples=points.Select(p=>{var s=prepared.SampleTerrain(p.x,p.y);return new{point=p,rawHeight=s.elevationNormalized*140,finishedFeatureHeight=projection(p.x,p.y)*140,source=s};}).ToArray();
        foreach(var sample in samples)Console.WriteLine("SAMPLE "+JsonConvert.SerializeObject(sample,settings));
        var owner=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQContinuousWorldFeatureMaterializer");
        var aprons=(System.Collections.IEnumerable)owner.GetMethod("GetNativeBridgeEndpointApproaches",flags).Invoke(null,new object[]{prepared});
        var apronRecords=new List<object>();
        foreach(object apron in aprons){var type=apron.GetType();int index=(int)type.GetField("routeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(apron);var route=prepared.GetRoute(index);apronRecords.Add(new{route=route.routeId,apron});}
        int routeIndex=Enumerable.Range(0,prepared.RouteCount).First(i=>prepared.GetRoute(i).routeId.Contains("yorcairn_yard"));
        var center=(List<Vector2>)typeof(YQGeneratedWorldEnvironment).GetMethod("GetAcceptedRouteCenterline",flags).Invoke(null,new object[]{prepared,routeIndex});
        Console.WriteLine("ROUTE "+JsonConvert.SerializeObject(new{route=prepared.GetRoute(routeIndex),centerline=center},settings));
        var result=new{seed=plan.worldSeed,hash=plan.spatialPlanV2.contentHash,summitReady,summit,samples,aprons=apronRecords,centerline=center,planUnchanged=before==JsonConvert.SerializeObject(plan,settings)};
        File.WriteAllText(args[1],JsonConvert.SerializeObject(result,Formatting.Indented,settings));
        if(!result.planUnchanged)throw new Exception("Plan changed");Console.WriteLine("RESULT checks=3 failures=0");
    }
}
'''
(out/'Query.cs').write_text(source)
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(TargetFramework='net10.0',OutputType='Exe').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup');dll=compiled/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
refs=[('Assembly-CSharp',dll)]
for r in ET.parse(compiled/'runtime/Compile.csproj').findall('.//Reference'):
    p=Path(r.findtext('HintPath'))
    if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((r.attrib['Include'],p))
for name,p in refs:
    r=ET.SubElement(items,'Reference',Include=name);ET.SubElement(r,'HintPath').text=str(p);inputs[p]=digest(p)
ET.ElementTree(project).write(out/'Query.csproj',encoding='utf-8');(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(out/'Query.csproj'),'--nologo','-v:q'],capture_output=True,text=True);(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Query.dll'),str(fixture),str(out/'Query.json')],capture_output=True,text=True) if build.returncode==0 else None
text=run.stdout+run.stderr if run else build.stdout+build.stderr;(out/'run.log').write_text(text);print(text)
changed=[str(p) for p,h in inputs.items() if digest(p)!=h];code=build.returncode or (run.returncode if run else 1) or bool(changed)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=3 if run and run.returncode==0 else 0,failures=0 if not code else 1,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='COMPILED_NUMERIC_IMMUTABLE_QUERY; NO_NATIVE_HEIGHTFIELD/PROTECTED_PARCEL_REPLAY_OR_PHYSX'),indent=2));sys.exit(code)
