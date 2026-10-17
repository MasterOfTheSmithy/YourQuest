"""Build current source against installed Unity references without starting or refreshing Unity."""
import json, subprocess, xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/InitialModelStack/compile'
OUT.mkdir(parents=True,exist_ok=True)
original=ET.parse(ROOT/'Assembly-CSharp.csproj').getroot()
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk')
props=ET.SubElement(project,'PropertyGroup')
values={'TargetFramework':'netstandard2.1','AssemblyName':'Assembly-CSharp','EnableDefaultCompileItems':'false',
        'GenerateAssemblyInfo':'false','LangVersion':'9.0','AllowUnsafeBlocks':'true','NoWarn':'0169;0649;0436',
        'DefineConstants':original.findtext('.//DefineConstants')}
for key,value in values.items():ET.SubElement(props,key).text=value
items=ET.SubElement(project,'ItemGroup')
included=set()
missing=[]
for item in original.findall('.//Compile'):
    path=(ROOT/item.attrib['Include']).resolve()
    if not path.is_file():missing.append(str(path));continue
    ET.SubElement(items,'Compile',Include=str(path));included.add(path)
for name in ['YQLocalSpeech','YQSpeechPcmBuffer','YQNpcVoice','YQVoiceRouteRules','YQDialogueMemorySelection']:
    path=ROOT/f'Assets/Assets/Scripts/Dialogue/{name}.cs'
    if path not in included:ET.SubElement(items,'Compile',Include=str(path));included.add(path)
for item in original.findall('.//Reference'):
    hint=item.findtext('HintPath')
    if not hint:continue
    path=(ROOT/hint).resolve()
    if not path.is_file():missing.append(str(path));continue
    ref=ET.SubElement(items,'Reference',Include=item.attrib['Include'])
    ET.SubElement(ref,'HintPath').text=str(path)
    # note: Compilation needs the installed reference, not another copied DLL in each output directory.
    ET.SubElement(ref,'Private').text='false'
for item in original.findall('.//ProjectReference'):
    path=ROOT/'Library/ScriptAssemblies'/(Path(item.attrib['Include']).stem+'.dll')
    if not path.is_file():missing.append(str(path));continue
    ref=ET.SubElement(items,'Reference',Include=path.stem)
    ET.SubElement(ref,'HintPath').text=str(path)
    ET.SubElement(ref,'Private').text='false'
ET.ElementTree(project).write(OUT/'Compile.csproj',encoding='utf-8',xml_declaration=True)
(OUT/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
result=subprocess.run(['dotnet','build',str(OUT/'Compile.csproj'),'--nologo','-v:q'],cwd=ROOT,capture_output=True,text=True)
(OUT/'build.log').write_text(result.stdout+result.stderr,encoding='utf-8')
(OUT/'receipt.json').write_text(json.dumps({'exit_code':result.returncode,'source_count':len(included),
    'missing_generated_project_entries':missing,'evidence':'Detached .NET Standard 2.1 compile against installed Unity references. No Unity import, PlaySafe, microphone or gameplay proof.'},indent=2),encoding='utf-8')
print(result.stdout[-7000:]);print(result.stderr[-1000:])
raise SystemExit(result.returncode)
