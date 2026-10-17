"""Compile only the bounded detached harness against an explicitly supplied fresh assembly."""
import argparse, json, shutil, subprocess, xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--assembly', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--baseline', action='store_true')
    p.add_argument('--source', type=Path, help='Focused detached check source; defaults to Checks.cs')
    args = p.parse_args()
    out = args.out.resolve()
    if not out.is_relative_to(ROOT / 'outputs') or out.exists():
        raise ValueError('Use a fresh workspace outputs folder')
    out.mkdir(parents=True)
    project = ET.parse(ROOT / 'Assembly-CSharp.csproj').getroot()
    refs = []
    for item in project.findall('.//Reference'):
        hint = item.findtext('HintPath')
        if hint:
            path = (ROOT / hint).resolve()
            if path.name.startswith('UnityEngine.') or 'Newtonsoft' in path.name or (
                    args.source and path.name in ('Unity.TextMeshPro.dll', 'Unity.InputSystem.dll')):
                refs.append(path)
    source = args.source.resolve() if args.source else Path(__file__).with_name('Checks.cs')
    if args.baseline:
        # note: Baseline has no repaired helper; remove only post-packet checks from this test-owned source copy.
        text = source.read_text(encoding='utf-8')
        text = text.replace('YQDialogueGrounding.ReplySchema(220, false, input.text)', 'YQDialogueGrounding.ReplySchema(220)')
        text = text[:text.index('        foreach (var text in new[]')] + '        return 0;\n    }\n}\n'
        source = out / 'Baseline.cs'
        source.write_text(text, encoding='utf-8')
    sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
    framework = sorted(Path('C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref').glob('10.*'))[-1]
    binary = out / 'Checks.dll'
    assembly = args.assembly.resolve()
    shutil.copy2(assembly, out / 'Assembly-CSharp.dll')
    for ref in refs:
        shutil.copy2(ref, out / ref.name)
    arguments = ['/nologo', '/target:exe', '/langversion:latest', f'/out:"{binary}"',
        *[f'/reference:"{r}"' for r in (framework / 'ref/net10.0').glob('*.dll')],
        f'/reference:"{assembly}"', *[f'/reference:"{r}"' for r in refs], f'"{source}"']
    rsp = out / 'checks.rsp'; rsp.write_text('\n'.join(arguments), encoding='utf-8')
    subprocess.run(['dotnet', f'C:/Program Files/dotnet/sdk/{sdk}/Roslyn/bincore/csc.dll', '@'+str(rsp)], check=True)
    (out / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net10.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '10.0.0'}, 'rollForward': 'LatestPatch'}}))
    subprocess.run(['dotnet', str(binary), str(out), *(['baseline'] if args.baseline else [])], check=True)

if __name__ == '__main__':
    main()
