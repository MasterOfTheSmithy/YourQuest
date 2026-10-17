"""Compile current origin acceptance and repair source for a paired diagnostic retest."""
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmLatencyImplementation_20261007/LoadoutChecks'
OUT.mkdir(exist_ok=True)
spec = importlib.util.spec_from_file_location('extractor', ROOT / 'outputs/Llm8GBBatch2_20261007_2025/GoddessSourceHarness/prepare.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
records = []
origin = ROOT / 'Assets/Assets/Scripts/Generated/YQOriginGenerationService.cs'
code = ['using System;', 'using System.Collections.Generic;', 'using Newtonsoft.Json;', 'using Newtonsoft.Json.Linq;']
for name in ['YQOriginGenerationDto','YQOriginGeneratedStatsDto','YQOriginGeneratedAbilityDto','YQOriginGeneratedQuestDto','YQOriginGeneratedObjectiveDto','YQOriginGeneratedItemDto']:
    code.append(helper.extract(origin,'public sealed class '+name,records))
for name in ['YQGoddessGenerationVoiceDto','YQGoddessLocationVoiceDto']:
    code.append(helper.extract(ROOT/'Assets/Assets/Scripts/Generated/YQGoddessGenerationDialogue.cs','public sealed class '+name,records))
code.append('internal static class CurrentOrigin {')
for marker in ['private static bool Validate(', 'private static bool HasUsableGeneratedLoadout(', 'private static bool IsSupportedLoadoutSlot(', 'private static bool IsAllowedDirectionKey(', 'private static string NormalizeKey(']:
    code.append(helper.extract(origin,marker,records))
code.append('internal static bool Check(YQOriginGenerationDto dto,out string error) => Validate(dto,out error); }')
code.append('internal static class CurrentTransport {')
for marker in ['private static bool TryNormalizeJsonObject(', 'private static string StripReasoningBlocks(']:
    code.append(helper.extract(ROOT/'Assets/Assets/Scripts/LLM/LLMClient.cs',marker,records))
code.append('internal static bool Normalize(string raw,out string normalized,out string error) => TryNormalizeJsonObject(StripReasoningBlocks(raw),out normalized,out error); }')
(OUT/'SourceChecks.cs').write_text('\n'.join(code),encoding='utf-8')
library=next((ROOT/'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll'))
sources=[OUT/'SourceChecks.cs',ROOT/'Tools/LLM8GBBenchmark/loadout_retest.cs',
         ROOT/'Assets/Assets/Scripts/LLM/YQBoundedRepair.cs',ROOT/'Assets/Assets/Scripts/Generated/YQItemAuthoringPolicy.cs']
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
project+=''.join(f'<Compile Include="{p}"/>' for p in sources)
project+=f'<Reference Include="Newtonsoft.Json"><HintPath>{library}</HintPath></Reference></ItemGroup></Project>'
(OUT/'LoadoutChecks.csproj').write_text(project,encoding='utf-8')
(OUT/'SourceIdentity.json').write_text(json.dumps(dict(extracts=records,files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}),indent=2),encoding='utf-8')
print(OUT)
