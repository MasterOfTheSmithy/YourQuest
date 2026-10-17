"""Inspect reachable compiled WorldState JSON contracts without Unity or profile actions."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,subprocess,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--compile',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);a=ap.parse_args()
compiled=(ROOT/a.compile).resolve();out=(ROOT/a.out).resolve()
if out.exists() or not out.is_relative_to(ROOT/'outputs'):ap.error('Use fresh outputs directory')
out.mkdir(parents=True);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=ROOT/'Tools/TerrainRefactor/WorldCloneContractAudit.cs';inputs={str(source):sha(source)}
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
for k,v in dict(OutputType='Exe',TargetFramework='net10.0',EnableDefaultCompileItems='false').items():ET.SubElement(props,k).text=v
items=ET.SubElement(project,'ItemGroup');ET.SubElement(items,'Compile',Include=str(source))
refs=[('Assembly-CSharp',compiled/'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll')]
for ref in ET.parse(compiled/'runtime/Compile.csproj').findall('.//Reference'):
    p=Path(ref.findtext('HintPath'))
    if p.name.startswith('UnityEngine.') or 'Newtonsoft' in p.name:refs.append((ref.attrib['Include'],p))
for name,p in refs:
    inputs[str(p)]=sha(p);node=ET.SubElement(items,'Reference',Include=name);ET.SubElement(node,'HintPath').text=str(p)
ET.ElementTree(project).write(out/'Audit.csproj',encoding='utf-8');(out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
run=subprocess.run(['dotnet','run','--project',str(out/'Audit.csproj'),'--nologo'],cwd=ROOT,capture_output=True,text=True)
(out/'run.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr)
changed=[p for p,h in inputs.items() if not Path(p).exists() or sha(Path(p))!=h]
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='CAPTURED' if run.returncode==0 and not changed else 'FAIL',exit_code=run.returncode,changed_inputs=changed,input_sha256=inputs,evidence='READ_ONLY_COMPILED_DTO_CONTRACT_METADATA; NOT_THREAD_SAFETY_OR_NATIVE_ACCEPTANCE')
(out/'Receipt.json').write_text(json.dumps(receipt,indent=2))
raise SystemExit(run.returncode or bool(changed))
