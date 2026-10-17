"""Source-extracted proposed fingerprint decisions; structured serialization/authority/native APIs explicitly substituted."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,subprocess,sys,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss/fingerprint_decision_checks_2'
assert not out.exists();out.mkdir()
candidate=root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss/RuntimeBuilder.proposal.cs';source=candidate.read_text()
def method(start):
    a=source.index(start);i=source.index('{',a)+1;d=1
    while d:d+=(source[i]=='{')-(source[i]=='}');i+=1
    return source[a:i]
helper=(root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss/TerrainFingerprintHelper.proposal.cs').read_text()
helper=helper.replace('private static string BuildProfileTerrainFingerprint','internal static string BuildProfileTerrainFingerprint').replace('private static bool IsCompatibleTerrainFingerprint','internal static bool IsCompatibleTerrainFingerprint')
compat=method('    private static bool IsCompatibleProfileTerrainSnapshot(').replace('private static bool','internal static bool',1)
visual=method('    private static string BuildVisualSignature(').replace('private static string','internal static string',1)
text='using System;using System.Collections.Generic;using System.Text;using System.Linq;using System.IO;using Newtonsoft.Json;using UnityEngine;\npublic static class Fingerprints {\n'+helper+compat+visual+'}\n'
text+='''
// note: Hashing is real; Unity field serialization is replaced by the named structured serializer, and accepted authority is a controlled fixture gate.
public static class YQStateContract {public static string Sha256Hex(string value)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();}
public static class JsonUtility {
    public static readonly JsonSerializerSettings Settings=new(){TypeNameHandling=TypeNameHandling.None,Converters={new Vector3JsonConverter(),new Vector2JsonConverter(),new QuaternionJsonConverter()}};
    public static string ToJson(object o)=>JsonConvert.SerializeObject(o,Settings);
    public static T FromJson<T>(string text)=>JsonConvert.DeserializeObject<T>(text,Settings);
}
public static class YQWorldGenerationArchitecture {
    public static bool Ready=true;
    public static bool TryResolveRuntimeAuthority(GeneratedWorldPlanRecord plan,out YQSpatialPlanAuthority authority,out string failure){authority=plan.spatialPlanV2!=null?YQSpatialPlanAuthority.AcceptedV2:YQSpatialPlanAuthority.PersistedV1;failure="fixture";return Ready;}
}
public static class YQGeneratedWorldTerrain {
    public static string TerrainGenerationVersion="generated_terrain_v4_biome_tiles";
    public static float WorldSize=1024,TerrainHeight=140;public static int HeightmapResolution=513;
    public sealed class ProfileTerrainSnapshotRecord {public int schemaVersion=1;public bool hasTerrain=true;public string ownerProfileId,worldId,worldSeed,planFingerprint,heightmapChecksum;}
}
public static class Checks {
    static int checks,failures;
    static void Check(bool pass,string name){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);}
    static GeneratedWorldPlanRecord Clone(GeneratedWorldPlanRecord p)=>JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(p));
    static string Hash(GeneratedWorldPlanRecord p)=>Fingerprints.BuildProfileTerrainFingerprint(p);
    public static void Main(string[] args){
        var before=JsonUtility.FromJson<WorldState>(File.ReadAllText(args[0]));var after=JsonUtility.FromJson<WorldState>(File.ReadAllText(args[1]));
        var plan=before.generatedWorldPlan;var newPlan=after.generatedWorldPlan;string originalJson=JsonUtility.ToJson(plan);
        string expected=Hash(plan);string legacy=YQStateContract.Sha256Hex(Fingerprints.BuildVisualSignature(plan));
        Check(expected.StartsWith("terrain_native_inputs_v1:"),"native accepted V2 uses dedicated tagged domain");
        var paletteOnly=Clone(plan);paletteOnly.regions[0].assetStyleKey=newPlan.regions[0].assetStyleKey;
        paletteOnly.assetPalettes[0].styleKey=newPlan.assetPalettes[0].styleKey;paletteOnly.assetPalettes[0].layoutRuleProfile=newPlan.assetPalettes[0].layoutRuleProfile;
        Check(expected==Hash(paletteOnly),"isolated palette/style/layoutRule transition preserves terrain fingerprint");
        Check(expected!=Hash(newPlan),"actualrev25to26 also changes accepted continuation and correctly rejects old terrain identity");
        Check(legacy!=YQStateContract.Sha256Hex(Fingerprints.BuildVisualSignature(newPlan)),"visual signature still detects that presentation change");
        Check(expected==Hash(Clone(plan)),"fresh/restored structured plan gives identical terrain fingerprint");
        Check(Fingerprints.IsCompatibleTerrainFingerprint(legacy,expected,plan),"exact current legacy visual qualifies first restore");
        Check(!Fingerprints.IsCompatibleTerrainFingerprint(legacy,expected,newPlan),"changed visual legacy history is NOT blindly relabeled or recovered");
        Check(!Fingerprints.IsCompatibleTerrainFingerprint("terrain_native_inputs_v1:bad",expected,plan),"tagged mismatch has no legacy fallback");
        Check(!Fingerprints.IsCompatibleTerrainFingerprint("",expected,plan)&&!Fingerprints.IsCompatibleTerrainFingerprint(expected,"",plan),"empty fingerprints reject");
        foreach(string mutation in new[]{"seed","basehash","generation","validation","kit","binding","layout","selection","continuation"}){
            var copy=Clone(plan);
            if(mutation=="seed")copy.worldSeed+="changed";
            if(mutation=="basehash")copy.spatialPlanV2.contentHash+="changed";
            if(mutation=="generation")copy.spatialPlanV2.generationVersion="archived";
            if(mutation=="validation")copy.spatialPlanV2.validationVersion+="changed";
            if(mutation=="kit")copy.settlements[0].runtimeSiteKitId+="changed";
            if(mutation=="binding")copy.settlements[0].runtimeSiteBindingVersion+="changed";
            if(mutation=="layout")copy.settlements[0].proceduralLayout=new YQProceduralSettlementLayoutRecord{seed="changed"};
            if(mutation=="selection")copy.settlements[0].proceduralBlockTarget++;
            if(mutation=="continuation")copy.spatialPlanV2.acceptedContinuation=new(){contentHash="changed",worldSeed=copy.worldSeed};
            Check(Hash(copy)!=expected,"physical input change rejects: "+mutation);
        }
        int resolution=YQGeneratedWorldTerrain.HeightmapResolution;YQGeneratedWorldTerrain.HeightmapResolution=1025;
        Check(Hash(plan)!=expected,"grid resolution change rejects");YQGeneratedWorldTerrain.HeightmapResolution=resolution;
        string generation=YQGeneratedWorldTerrain.TerrainGenerationVersion;YQGeneratedWorldTerrain.TerrainGenerationVersion+="changed";
        Check(Hash(plan)!=expected,"terrain generator identity change rejects");YQGeneratedWorldTerrain.TerrainGenerationVersion=generation;
        YQWorldGenerationArchitecture.Ready=false;Check(Hash(plan)=="","unqualified native authority has no usable terrain identity");YQWorldGenerationArchitecture.Ready=true;
        var archived=Clone(plan);archived.spatialPlanV2.generationVersion="archived";
        Check(Hash(archived)==YQStateContract.Sha256Hex(Fingerprints.BuildVisualSignature(archived)),"archived V2 keeps exact original fingerprint");
        var v1=Clone(plan);v1.spatialPlanV2=null;
        Check(Hash(v1)==YQStateContract.Sha256Hex(Fingerprints.BuildVisualSignature(v1)),"V1 keeps exact original fingerprint");
        var record=new YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord{ownerProfileId=before.worldIdentity.ownerProfileId,worldId=before.worldIdentity.worldId,worldSeed=plan.worldSeed,planFingerprint=expected,heightmapChecksum="fixture_checksum"};
        string json=JsonUtility.ToJson(record);string profile=before.worldIdentity.ownerProfileId;
        Check(Fingerprints.IsCompatibleProfileTerrainSnapshot(json,profile,before,plan,expected),"native tagged record matches exact profile world seed domain");
        foreach(string mutation in new[]{"profile","world","seed","fingerprint","presence","schema","checksum"}){
            var r=JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(json);
            if(mutation=="profile")r.ownerProfileId="other";
            if(mutation=="world")r.worldId="other";
            if(mutation=="seed")r.worldSeed="other";
            if(mutation=="fingerprint")r.planFingerprint="other";
            if(mutation=="presence")r.hasTerrain=false;
            if(mutation=="schema")r.schemaVersion=2;
            if(mutation=="checksum")r.heightmapChecksum="";
            Check(!Fingerprints.IsCompatibleProfileTerrainSnapshot(JsonUtility.ToJson(r),profile,before,plan,expected),"snapshot ownership/validity gate rejects: "+mutation);
        }
        Check(originalJson==JsonUtility.ToJson(plan),"fingerprint queries do not mutate accepted plan");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");Environment.ExitCode=failures==0?0:1;
    }
}
'''
(out/'Checks.cs').write_text(text)
compiled=root/'outputs/TerrainRefactor_20261013_BankTraversal/compile_stream_site_paths';runtime=compiled/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(TargetFramework='net10.0',OutputType='Exe',NoWarn='0436').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup');refs=[('Assembly-CSharp',runtime)]
for r in ET.parse(compiled/'runtime/Compile.csproj').findall('.//Reference'):
    p=Path(r.findtext('HintPath'))
    if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((r.attrib['Include'],p))
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (candidate,Path(__file__),root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss/TerrainFingerprintHelper.proposal.cs')}
for name,p in refs:
    r=ET.SubElement(items,'Reference',Include=name);ET.SubElement(r,'HintPath').text=str(p);inputs[p]=hashlib.sha256(p.read_bytes()).hexdigest()
ET.ElementTree(project).write(out/'Checks.csproj',encoding='utf-8');(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
profile=Path('C:/Users/Garri/AppData/LocalLow/DefaultCompany/YourQuest/profiles/04de758c424e466a92158561de7ef246/revisions')
worlds=[next(profile.glob(f'r{n:08d}-*'))/'world_state.json' for n in (25,26)]
for p in worlds:inputs[p]=hashlib.sha256(p.read_bytes()).hexdigest()
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True);(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll'),*[str(p) for p in worlds]],capture_output=True,text=True) if build.returncode==0 else None
text=run.stdout+run.stderr if run else build.stdout+build.stderr;(out/'run.log').write_text(text);print(text)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',text);checks=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h];code=build.returncode or (run.returncode if run else 1) or bool(changed or not checks or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=checks,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='SOURCE_EXTRACTED_PROPOSED_FINGERPRINT/OWNERSHIP_DECISIONS; ACTUAL25/26PLAN_DTOS; STRUCTURED_SERIALIZATION_AND_AUTHORITY_SUBSTITUTED; NO_NATIVE_JSON/PAYLOAD/PROFILE_PUBLICATION'),indent=2));sys.exit(code)
