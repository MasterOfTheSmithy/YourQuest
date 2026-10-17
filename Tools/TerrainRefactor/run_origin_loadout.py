"""Compile actual pure origin loadout schema/merge code, without Unity/model/save access."""
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[2]
output = (root / sys.argv[1]).resolve()
assert output.is_relative_to(root / 'outputs/TerrainRefactor_20261013_LoadFailure') and not output.exists()
sources = {name: root / path for name, path in {
    'helper': 'Assets/Assets/Scripts/LLM/YQBoundedRepair.cs',
    'schema': 'Assets/Assets/Scripts/LLM/YQLlmRequest.cs',
    'policy': 'Assets/Assets/Scripts/Generated/YQItemAuthoringPolicy.cs',
    'service': 'Assets/Assets/Scripts/Generated/YQOriginGenerationService.cs',
    'checks': 'Tools/TerrainRefactor/OriginLoadoutChecks.cs',
}.items()}
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
inputs = {path: digest(path) for path in sources.values()}
helper = sources['helper'].read_text()
schema = sources['schema'].read_text()
pure = 'using System;\nusing System.Collections.Generic;\nusing Newtonsoft.Json;\nusing Newtonsoft.Json.Linq;\n'
pure += helper[helper.index('public static class YQOriginLoadoutRepair'):]
pure += schema[schema.index('public static class YQLlmJsonSchema'):schema.index('public readonly struct YQLlmRequestResult')]
# note: Assert the actual queued owner uses the tested grammar; compilation below does not execute transport or inference.
service = sources['service'].read_text().split('private void SubmitOriginLoadoutRepair(', 1)[1].split('private static string BuildPrompt(', 1)[0]
assert 'jsonSchema = YQOriginLoadoutRepair.BuildSchema(),' in service
assert 'requireJson = true, maxRetries = 0' in service and 'ownerStillCurrent = originalRequest.ownerStillCurrent' in service
assert 'TryParseOrigin(merged, seed, mode' in service and 'episode.Bind(request)' in service
refs = ET.parse(root / 'outputs/TerrainRefactor_20261012/detached_streaming/compile_checkpoint/runtime/Compile.csproj')
newtonsoft = next(Path(node.findtext('HintPath')) for node in refs.findall('.//Reference') if 'Newtonsoft' in node.attrib['Include'])
inputs[newtonsoft] = digest(newtonsoft)
output.mkdir(parents=True)
(output / 'PureProduction.cs').write_text(pure)
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for key, value in {'TargetFramework': 'net10.0', 'OutputType': 'Exe', 'EnableDefaultCompileItems': 'false'}.items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(project, 'ItemGroup')
for path in [output / 'PureProduction.cs', sources['policy'], sources['checks']]:
    ET.SubElement(items, 'Compile', Include=str(path))
ref = ET.SubElement(items, 'Reference', Include='Newtonsoft.Json')
ET.SubElement(ref, 'HintPath').text = str(newtonsoft)
ET.ElementTree(project).write(output / 'Checks.csproj', encoding='utf-8')
(output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
build = subprocess.run(['dotnet', 'build', str(output / 'Checks.csproj'), '--nologo', '-v:q'], capture_output=True, text=True)
(output / 'compile.log').write_text(build.stdout + build.stderr)
run = subprocess.run(['dotnet', str(output / 'bin/Debug/net10.0/Checks.dll')], capture_output=True, text=True) if build.returncode == 0 else None
if run:
    (output / 'run.log').write_text(run.stdout + run.stderr)
print(run.stdout + run.stderr if run else build.stdout + build.stderr)
summary = re.search(r'^RESULT checks=(\d+) failures=(\d+)\s*$', run.stdout, re.MULTILINE) if run else None
changed = [str(path) for path, value in inputs.items() if digest(path) != value]
code = build.returncode or (run.returncode if run else 1) or (2 if not summary or changed else 0)
(output / 'Receipt.json').write_text(json.dumps({
    'utc': datetime.now(timezone.utc).isoformat(), 'status': 'PASS' if code == 0 else 'FAIL',
    'checks': int(summary.group(1)) if summary else 0, 'failures': int(summary.group(2)) if summary else 1,
    'input_sha256': {str(path): value for path, value in inputs.items()}, 'changed_inputs': changed,
    'evidence': 'SOURCE_EXTRACTED_COMPILED_ACTUAL_PURE_SCHEMA_AND_MERGE; STATIC_REQUEST_WIRING; NO_BACKEND_GRAMMAR_OR_MODEL_OR_UNITY_PROOF',
}, indent=2))
sys.exit(code)
