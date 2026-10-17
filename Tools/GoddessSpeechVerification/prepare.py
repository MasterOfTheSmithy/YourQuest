"""Build a detached speech harness from current production source and explicit Unity-only shims."""
from pathlib import Path
import json, re, subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/GoddessSpeechRepair_20261007'
OUT.mkdir(parents=True, exist_ok=True)
GEN = ROOT / 'Assets/Assets/Scripts/Generated'
source = (GEN / 'YQGoddessGenerationDialogue.cs').read_text(encoding='utf-8-sig')

def between(start, end):
    return source[source.index(start):source.index(end, source.index(start))]

# note: Extract the actual lexical validators and speaker contract; do not replace their behavior with a permissive test stub.
chunks = [
    between('    public static bool IsSpokenVoiceFieldAcceptable(', '    // note: Require accepted speech'),
    between('    private static bool HasPresentationMachinery(', '    private static bool AreSpokenAmbientLinesAcceptable('),
    between('    private static int CountWords(', '    private static void ReportOriginVoiceQuality('),
    between('    private static bool ContainsFirstPersonPronoun(', '    private static string BuildCensoredGoddessLine('),
    between('    private static bool IsGeneratedGoddessLineAllowed(', '    private static bool TryRememberGeneratedLine('),
    between('    private const string SpeakerIdentity =', '    private static string BuildArchivedVerboseVoiceContract('),
]
field = re.search(r'    private static readonly string\[\]\s+HollowPlayerImperativeOpenings\s*=.*?;', source, re.S).group()
support = 'using System;\npublic static class YQGoddessGenerationDialogue {\n' + field + '\n' + '\n'.join(chunks) + '\n}\n'
support = support.replace('Mathf.Min', 'Math.Min')
support += '''
public static class YQRepairEpisode {
 public static string Hash(string value) {
  using (var sha = System.Security.Cryptography.SHA256.Create())
   return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
 }
}
public static class YQGoddessGrounding { public static string ReviewContractHash => "UNUSED_HEADLESS_VERIFIER"; }
namespace UnityEngine {
 public class HeaderAttribute : Attribute { public HeaderAttribute(string value) {} }
 public class RangeAttribute : Attribute { public RangeAttribute(float low, float high) {} }
 public class CreateAssetMenuAttribute : Attribute { public string menuName; }
 public static class Mathf { public static int Clamp(int value,int low,int high) => Math.Clamp(value,low,high); }
}
'''
(OUT / 'Support.cs').write_text(support, encoding='utf-8')
config = (ROOT / 'Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs').read_text(encoding='utf-8-sig')
# note: Only the Unity native ScriptableObject constructor is replaced; configuration fields, profile factories and hash code remain production source.
config = config.replace('LLMRuntimeConfig : ScriptableObject', 'LLMRuntimeConfig').replace('CreateInstance<LLMRuntimeConfig>()', 'new LLMRuntimeConfig()')
(OUT / 'Config.cs').write_text(config, encoding='utf-8')
newtonsoft = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
(OUT / 'Harness.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0649</NoWarn></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{newtonsoft}</HintPath></Reference><Compile Include="{GEN / 'YQGoddessSpeechPlan.cs'}"/><Compile Include="Support.cs"/><Compile Include="Config.cs"/><Compile Include="Program.cs"/></ItemGroup></Project>''', encoding='utf-8')
(OUT / 'Program.cs').write_text((Path(__file__).parent / 'Program.cs').read_text(encoding='utf-8'), encoding='utf-8')
(OUT / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
subprocess.run(['dotnet', 'build', str(OUT / 'Harness.csproj'), '--nologo', '-v:q'], check=True, cwd=ROOT)
subprocess.run(['dotnet', str(OUT / 'bin/Debug/net10.0/Harness.dll'), 'export'], check=True, cwd=ROOT)
