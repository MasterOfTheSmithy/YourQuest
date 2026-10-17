"""Compile current runtime and editor sources together against installed Unity references."""
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/InitialModelStack/editor-compile'


def project(name, extra):
    original = ET.parse(ROOT / (name + '.csproj')).getroot()
    folder = OUT / ('editor' if name.endswith('-Editor') else 'runtime')
    folder.mkdir(parents=True, exist_ok=True)
    target = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(target, 'PropertyGroup')
    for key, value in dict(TargetFramework='netstandard2.1', AssemblyName=name,
                           EnableDefaultCompileItems='false', GenerateAssemblyInfo='false',
                           LangVersion='9.0', AllowUnsafeBlocks='true', NoWarn='0169;0649;0436',
                           DefineConstants=original.findtext('.//DefineConstants')).items():
        ET.SubElement(properties, key).text = value
    items = ET.SubElement(target, 'ItemGroup')
    sources, missing = set(), []
    # note: Unity's generated project may predate new scripts; include the known additions explicitly.
    for entry in [item.attrib['Include'] for item in original.findall('.//Compile')] + extra:
        path = (ROOT / entry).resolve()
        if not path.is_file():
            missing.append(str(path))
        elif path not in sources:
            ET.SubElement(items, 'Compile', Include=str(path))
            sources.add(path)
    for reference in original.findall('.//Reference'):
        hint = reference.findtext('HintPath')
        if not hint:
            continue
        path = (ROOT / hint).resolve()
        # note: SDK framework references replace Unity's BCL facade set; mixing both causes type-forwarder cycles.
        if 'UnityReferenceAssemblies' in path.parts:
            continue
        if path.stem == 'Assembly-CSharp':
            continue
        if not path.is_file():
            missing.append(str(path))
            continue
        item = ET.SubElement(items, 'Reference', Include=reference.attrib['Include'])
        ET.SubElement(item, 'HintPath').text = str(path)
        # note: Compile-only checks use installed references directly instead of duplicating Unity DLLs per receipt.
        ET.SubElement(item, 'Private').text = 'false'
    for reference in original.findall('.//ProjectReference'):
        assembly = Path(reference.attrib['Include']).stem
        # note: The editor must reference the freshly compiled runtime, never a stale Library DLL.
        if assembly == 'Assembly-CSharp':
            ET.SubElement(items, 'ProjectReference', Include=str(OUT / 'runtime/Compile.csproj'))
            continue
        path = ROOT / 'Library/ScriptAssemblies' / (assembly + '.dll')
        if not path.is_file():
            missing.append(str(path))
            continue
        item = ET.SubElement(items, 'Reference', Include=assembly)
        ET.SubElement(item, 'HintPath').text = str(path)
        ET.SubElement(item, 'Private').text = 'false'
    ET.ElementTree(target).write(folder / 'Compile.csproj', encoding='utf-8', xml_declaration=True)
    (folder / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
    return sources, missing


def main():
    additions = [f'Assets/Assets/Scripts/Dialogue/{name}.cs' for name in
                 ['YQLocalSpeech', 'YQSpeechPcmBuffer', 'YQNpcVoice', 'YQVoiceRouteRules',
                  'YQDialogueMemorySelection']]
    runtime, runtime_missing = project('Assembly-CSharp', additions)
    editor, editor_missing = project('Assembly-CSharp-Editor', [])
    verifier = ROOT / 'Assets/Assets/Scripts/Generated/Editor/YQOriginLagVerification.cs'
    assert verifier in editor
    if runtime_missing or editor_missing:
        raise RuntimeError('Required source/reference missing: ' + str(runtime_missing + editor_missing))
    result = subprocess.run(['dotnet', 'build', str(OUT / 'editor/Compile.csproj'), '--nologo', '-v:q'],
                            cwd=ROOT, capture_output=True, text=True)
    (OUT / 'build.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    receipt = dict(exit_code=result.returncode, runtime_sources=len(runtime), editor_sources=len(editor),
                   verifier_included=str(verifier), fresh_runtime_reference=True,
                   evidence='Detached runtime AND editor compilation against installed Unity references. Not a Unity Console or PlaySafe run.',
                   source_sha256={str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest()
                                  for path in [verifier, ROOT / 'Assets/Assets/Scripts/Dialogue/NpcDialogueAgent.cs',
                                               ROOT / 'Assets/Assets/Scripts/Dialogue/DialogueThinkService.cs',
                                               ROOT / 'Assets/Assets/Scripts/Dialogue/YQNpcConversationRewards.cs']})
    (OUT / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    print(result.stdout[-9000:])
    print(result.stderr[-1000:])
    raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
