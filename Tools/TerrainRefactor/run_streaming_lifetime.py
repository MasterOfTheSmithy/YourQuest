"""Compile and exercise the production structural terrain lifetime rules without Unity or profile writes."""
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
assert not output.exists() and output.is_relative_to(root / 'outputs/TerrainRefactor_20261012')
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
inputs = {root / path: value for path, value in receipt['source_sha256'].items()}
assert all(digest(path) == value for path, value in inputs.items())
source = root / 'Tools/TerrainRefactor/StreamingLifetimeChecks.cs'
inputs[source] = digest(source)
output.mkdir(parents=True)
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for key, value in dict(TargetFramework='net10.0', OutputType='Exe', EnableDefaultCompileItems='false').items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(project, 'ItemGroup')
ET.SubElement(items, 'Compile', Include=str(source))
runtime = compiled / 'runtime/bin/Debug/netstandard2.1/Assembly-CSharp.dll'
refs = [('Assembly-CSharp', runtime)]
for ref in ET.parse(compiled / 'runtime/Compile.csproj').findall('.//Reference'):
    path = Path(ref.findtext('HintPath'))
    if path.name.startswith('UnityEngine.') or 'Newtonsoft' in path.name:
        refs.append((ref.attrib['Include'], path))
for name, path in refs:
    item = ET.SubElement(items, 'Reference', Include=name)
    ET.SubElement(item, 'HintPath').text = str(path)
    inputs[path] = digest(path)
project_path = output / 'Checks.csproj'
ET.ElementTree(project).write(project_path, encoding='utf-8')
(output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
build = subprocess.run(['dotnet', 'build', str(project_path), '--nologo', '-v:q'], capture_output=True, text=True)
(output / 'compile.log').write_text(build.stdout + build.stderr)
run = None
if build.returncode == 0:
    run = subprocess.run(['dotnet', str(output / 'bin/Debug/net10.0/Checks.dll')], capture_output=True, text=True)
    (output / 'run.log').write_text(run.stdout + run.stderr)
    print(run.stdout + run.stderr)
else:
    print(build.stdout + build.stderr)
changed = [str(path) for path, value in inputs.items() if digest(path) != value]
# note: Require the executed harness's actual check count and verdict; a successful launch without a result is not verification.
summary = re.search(r'^RESULT checks=(\d+) failures=(\d+)\s*$', run.stdout, re.MULTILINE) if run else None
checks = int(summary.group(1)) if summary else 0
failures = int(summary.group(2)) if summary else 1
code = build.returncode or (run.returncode if run else 1) or (2 if changed or not checks or failures else 0)
(output / 'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),
    status='PASS' if code == 0 else 'FAIL', checks=checks, failures=failures, changed_inputs=changed,
    input_sha256={str(path): value for path, value in inputs.items()},
    evidence='DETACHED_COMPILED_STREAMING_IDENTITY_AND_DISPOSAL; NO_NATIVE_LIFETIME_OR_PLAYER_PROOF'), indent=2))
sys.exit(code)
