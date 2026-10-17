"""Extract actual handoff/packet/launch members for bounded detached regression checks."""
import hashlib
import importlib.util
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = (ROOT / (sys.argv[1] if len(sys.argv) > 1 else 'outputs/LlmLatencyImplementation_20261007/Checks')).resolve()
if not OUT.is_relative_to(ROOT):
    raise ValueError('Checks output must remain inside the project.')
OUT.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('extractor', ROOT / 'outputs/Llm8GBBatch2_20261007_2025/GoddessSourceHarness/prepare.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
records = []
llm = ROOT / 'Assets/Assets/Scripts/LLM'


def member(file, marker):
    return helper.extract(llm / file, marker, records)


def expression(file, marker):
    # note: Expression-bodied key helpers end at a semicolon; preserve their exact production text.
    raw = (llm / file).read_text(encoding='utf-8-sig')
    assert raw.count(marker) == 1
    start = raw.rfind('\n', 0, raw.index(marker)) + 1
    end = raw.index(';', raw.index(marker)) + 1
    text = raw[start:end]
    records.append(dict(source=str(llm / file), declaration=marker, memberUtf8Sha256=hashlib.sha256(text.encode()).hexdigest()))
    return text


code = ['using System;', 'using System.Collections;', 'using System.Collections.Generic;', 'using System.Text;',
        'using Newtonsoft.Json;', 'using Newtonsoft.Json.Linq;', 'using UnityEngine;',
        member('LLMClient.cs', 'public sealed class OllamaRequest')]
code.append('partial class ClientUnderTest {')
for marker in ['private IEnumerator PrepareRoutedModelResidency(', 'private IEnumerator CanRetainResidentCpuHelper(',
               'private static string OllamaBaseUrl(', 'private Dictionary<string, object> BuildOllamaOptions(',
               'private static void ApplyOllamaLivePresentationPolicy(', 'private string BuildRequestJson(',
               'private static int ReadIntOption(', 'private static float ReadFloatOption(']:
    source = member('LLMClient.cs', marker)
    # note: OS free RAM is an explicit fixture boundary; pure policy cases cover low-memory rejection separately.
    if marker == 'private IEnumerator CanRetainResidentCpuHelper(':
        source = source.replace('YQLlmResidencyPolicy.AvailableSystemMemoryBytes()', 'AvailableMemoryBytes')
    code.append(source)
for marker in ['private string LlamaResidencyKey(', 'private static string BuildLlamaResidencyKey(']:
    code.append(expression('LLMClient.cs', marker))
code.append('}')
code.append('partial class LlamaCppServerProcess {')
for marker in ['private static bool TryBuildArguments(', 'internal static bool CanUseBoundedPromptReuse(',
               'private static void AppendArgument(', 'private static string Quote(']:
    code.append(member('LlamaCppServerProcess.cs', marker))
code.append('}')
(OUT / 'SourceMembers.cs').write_text('\n'.join(code), encoding='utf-8')
library = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll'))
includes = [OUT / 'SourceMembers.cs', ROOT / 'Tools/LLM8GBBenchmark/latency_checks.cs',
            llm / 'LLMRuntimeConfig.cs', llm / 'YQLlmResidencyPolicy.cs']
project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
project += ''.join(f'<Compile Include="{p}" />' for p in includes)
project += f'<Reference Include="Newtonsoft.Json"><HintPath>{library}</HintPath></Reference></ItemGroup></Project>'
(OUT / 'LatencyChecks.csproj').write_text(project, encoding='utf-8')
(OUT / 'SourceIdentity.json').write_text(json.dumps(dict(extracts=records, files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in includes}), indent=2), encoding='utf-8')
print(OUT)
