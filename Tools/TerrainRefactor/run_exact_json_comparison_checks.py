"""Verify output-only exact JSON comparison proposal using real Json.NET and compiled production DTOs."""
import argparse,hashlib,json,re,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);mode=ap.add_mutually_exclusive_group(required=True);mode.add_argument('--proposal');mode.add_argument('--current',action='store_true');ap.add_argument('--out',required=True);args=ap.parse_args()
proposal=(root/args.proposal).resolve() if args.proposal else None;out=(root/args.out).resolve()
if (proposal and not proposal.is_relative_to(root/'outputs')) or not out.is_relative_to(root/'outputs') or out.exists():ap.error('Require existing proposal and fresh outputs folder')
source=(root/'Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs') if args.current else proposal/'YQProfileCommitStore.cs.proposal';text=source.read_text()
a=text.index('    internal static bool JsonEqualsCaptured(');b=text.index('    public static bool TryCommit(',a)
extracted='using System;using Newtonsoft.Json;\npublic static class YQProfileCommitStore {\n'+text[a:b]+'}\n'
compiled=root/'outputs/TerrainRefactor_20261014_StreamingRetest/IntegratedCompile_09';runtime=compiled/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
checks=root/'Tools/TerrainRefactor/ExactJsonComparisonChecks.cs'
scope=(root/'Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs',root/'Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs') if args.current else (proposal/'YQProfileSaveSystem.cs.proposal',proposal/'WorldStateManager.cs.proposal')
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (source,checks,Path(__file__),*scope)}
if args.current:
    oldWorld=root/'outputs/TerrainRefactor_20261014_StreamingRetest/ExactJsonComparison_Reviewed/WorldStateManager.cs.before'
    inputs[oldWorld]=hashlib.sha256(oldWorld.read_bytes()).hexdigest()
    def declaration(sourceText,marker):
        start=sourceText.index(marker);i=sourceText.index('{',start)+1;depth=1
        while depth:depth+=(sourceText[i]=='{')-(sourceText[i]=='}');i+=1
        return sourceText[start:i]
    marker='    private static bool TryPreserveContinuationLocationReferences('
    currentPrefix=declaration(scope[1].read_text(),marker).replace('private static bool TryPreserveContinuationLocationReferences','internal static bool PreserveCurrentPrefix',1)
    oldPrefix=declaration(oldWorld.read_text(),marker).replace('private static bool TryPreserveContinuationLocationReferences','internal static bool PreserveOriginalPrefix',1)
    extracted+='public static class WsmPrefixProbe { private static readonly Newtonsoft.Json.JsonSerializerSettings JsonSettings = new(){Formatting=Newtonsoft.Json.Formatting.None,ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore,Converters={new Vector3JsonConverter(),new Vector2JsonConverter(),new QuaternionJsonConverter()}};\n'+currentPrefix+oldPrefix+'}\n'
else:
    extracted+='public static class WsmPrefixProbe { public static bool PreserveCurrentPrefix(GeneratedSpatialContinuationV2Record a,GeneratedSpatialContinuationV2Record b,out string e){e="proposal";return true;}public static bool PreserveOriginalPrefix(GeneratedSpatialContinuationV2Record a,GeneratedSpatialContinuationV2Record b,out string e){e="proposal";return true;} }\n'
profile=Path('C:/Users/Garri/AppData/LocalLow/DefaultCompany/YourQuest/profiles/04de758c424e466a92158561de7ef246/revisions')
fixture=next(profile.glob('r00000025-*'))/'world_state.json';inputs[fixture]=hashlib.sha256(fixture.read_bytes()).hexdigest()
out.mkdir(parents=True);(out/'Extracted.cs').write_text(extracted)
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(TargetFramework='net10.0',OutputType='Exe',EnableDefaultCompileItems='false',NoWarn='0436').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup')
for p in (out/'Extracted.cs',checks):ET.SubElement(items,'Compile',Include=str(p))
refs=[('Assembly-CSharp',runtime)]
for reference in ET.parse(compiled/'runtime/Compile.csproj').findall('.//Reference'):
    p=Path(reference.findtext('HintPath'))
    if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((reference.attrib['Include'],p))
for name,p in refs:
    ref=ET.SubElement(items,'Reference',Include=name);ET.SubElement(ref,'HintPath').text=str(p);inputs[p]=hashlib.sha256(p.read_bytes()).hexdigest()
ET.ElementTree(project).write(out/'Checks.csproj',encoding='utf-8');(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True)
(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll'),str(fixture)],cwd=out,capture_output=True,text=True) if not build.returncode else None
log=(run.stdout+run.stderr) if run else build.stdout+build.stderr;(out/'run.log').write_text(log);print(log)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',log);count=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h]
code=build.returncode or (run.returncode if run else 1) or bool(changed or not count or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=count,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},source_state='CURRENT_PRODUCTION' if args.current else 'OUTPUT_ONLY_PROPOSAL',evidence='SOURCE_EXTRACTED_'+('CURRENT_PRODUCTION' if args.current else 'OUTPUT_ONLY_PROPOSAL')+'; REAL_JSONNET/PRODUCTION_VALUE_CONVERTERS_AND_WORLD_DTO; HISTORICAL_IMMUTABLE_SAVEDWORLD; DETACHED_ALLOCATION/TIME_ONLY; NO_UNITY_OR_PUBLICATION_RUNTIME'),indent=2))
sys.exit(code)
