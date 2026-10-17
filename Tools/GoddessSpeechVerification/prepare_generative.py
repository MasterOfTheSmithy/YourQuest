"""Compile current prose/transaction source with explicit detached Unity/transport adapters."""
from pathlib import Path
import argparse, re, subprocess

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=ROOT / 'outputs/GoddessGenerativeRepair_20261007')
args = parser.parse_args()
args.output = args.output.resolve()
OUT = args.output / 'Harness'
OUT.mkdir(parents=True, exist_ok=True)
GEN = ROOT / 'Assets/Assets/Scripts/Generated'
LLM = ROOT / 'Assets/Assets/Scripts/LLM'
source = (GEN / 'YQGoddessGenerationDialogue.cs').read_text(encoding='utf-8-sig')

def between(text, start, end):
    begin = text.index(start)
    return text[begin:text.index(end, begin)]

# note: Lexical rules, episode budgets, request stamps, profile, parsers and speech transaction are production code, never permissive substitutes.
chunks = [
    between(source, '    public static bool IsSpokenVoiceFieldAcceptable(', '    private static bool HasPresentationMachinery('),
    between(source, '    private static bool HasPresentationMachinery(', '    private static bool ContainsDirectAddress('),
    between(source, '    private static int CountWords(', '    private static void ReportOriginVoiceQuality('),
    between(source, '    private static bool IsGeneratedGoddessLineAllowed(', '    private static bool ContainsCensorGlyphs('),
]
field = re.search(r'    private static readonly string\[\]\s+HollowPlayerImperativeOpenings\s*=.*?;', source, re.S).group()
support = 'using System;\npublic static class YQGoddessGenerationDialogue {\n' + field + '\n' + '\n'.join(chunks) + '\n}\n'
support += (Path(__file__).parent / 'GenerativeSupport.cs').read_text(encoding='utf-8')
(OUT / 'Support.cs').write_text(support, encoding='utf-8')
config = (LLM / 'LLMRuntimeConfig.cs').read_text(encoding='utf-8-sig')
config = config.replace('LLMRuntimeConfig : ScriptableObject', 'LLMRuntimeConfig').replace('CreateInstance<LLMRuntimeConfig>()', 'new LLMRuntimeConfig()')
(OUT / 'Config.cs').write_text(config, encoding='utf-8')
episode = (LLM / 'YQBoundedRepair.cs').read_text(encoding='utf-8-sig').split('public enum YQProgressionDisposition')[0]
(OUT / 'Episode.cs').write_text(episode, encoding='utf-8')
request = (LLM / 'YQLlmRequest.cs').read_text(encoding='utf-8-sig')
request = request[:request.index('// note: Central schema factories')] + request[request.index('public readonly struct YQLlmRequestResult'):]
(OUT / 'Request.cs').write_text(request, encoding='utf-8')
newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
sources = [GEN / ('YQGoddess' + name + '.cs') for name in ['SpeechPlan', 'Grounding', 'AgentProfile', 'SpeechHistory', 'Speech']]
sources += [ROOT / 'Assets/Assets/Scripts/Dialogue/YQDialogueGrounding.cs', ROOT / 'Assets/Assets/Scripts/Dialogue/DialogueThinkService.cs', LLM / 'LLMContextCompiler.cs', Path(__file__).parent / 'NpcChainHarness.cs']
sources += [OUT / name for name in ['Support.cs', 'Config.cs', 'Episode.cs', 'Request.cs', 'Program.cs']]
items = ''.join(f'<Compile Include="{path}"/>' for path in sources)
(OUT / 'Harness.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0649</NoWarn></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{newtonsoft}</HintPath></Reference>{items}</ItemGroup></Project>', encoding='utf-8')
# note: Keep each qualification's receipts separate from the earlier failed configuration.
program = (Path(__file__).parent / 'GenerativeProgram.cs').read_text(encoding='utf-8')
program = program.replace('"outputs/GoddessGenerativeRepair_20261007"', '"' + args.output.resolve().as_posix() + '"')
(OUT / 'Program.cs').write_text(program, encoding='utf-8')
(OUT / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
subprocess.run(['dotnet', 'build', str(OUT / 'Harness.csproj'), '--nologo', '-v:q'], check=True, cwd=ROOT)
subprocess.run(['dotnet', str(OUT / 'bin/Debug/net10.0/Harness.dll'), 'pure'], check=True, cwd=ROOT)
