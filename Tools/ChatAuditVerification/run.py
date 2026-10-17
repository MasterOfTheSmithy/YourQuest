#!/usr/bin/env python3
"""Compile focused source checks with an installed .NET SDK; does not run Unity."""
import argparse
import json
import pathlib
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk-root', type=pathlib.Path, required=True)
    parser.add_argument('--baseline', default='ee485e38ab6d66610e5ce3293b615b6b1a9dc1b8')
    parser.add_argument('--output', type=pathlib.Path)
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[2]
    sdk = sorted((args.sdk_root / 'sdk').iterdir())[-1]
    framework = sorted((args.sdk_root / 'packs/Microsoft.NETCore.App.Ref').iterdir())[-1]
    runtime = sorted((args.sdk_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    refs = next((framework / 'ref').iterdir())
    dotnet = args.sdk_root / ('dotnet.exe' if (args.sdk_root / 'dotnet.exe').exists() else 'dotnet')
    output = args.output.resolve() if args.output else pathlib.Path(tempfile.mkdtemp(prefix='yourquest-chat-verification-'))
    output.mkdir(parents=True, exist_ok=True)
    # note: Capture the exact changed source set; no Unity caches, scenes, profiles or imported assets are accessed.
    changed = subprocess.check_output(['git', 'diff', '--name-only', args.baseline], cwd=root, text=True).splitlines()
    changed += subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard'], cwd=root, text=True).splitlines()
    paths = sorted({p for p in changed if p.startswith('Assets/') and p.endswith('.cs') and (root / p).is_file()})
    (output / 'changed-sources.json').write_text(json.dumps(paths), encoding='utf-8')
    extra = [sdk / 'Roslyn/bincore/Microsoft.CodeAnalysis.dll', sdk / 'Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll', sdk / 'Newtonsoft.Json.dll']
    compiler_args = ['/nologo', '/target:exe', '/langversion:9.0', '/out:' + str(output / 'Verify.dll')]
    compiler_args += ['/reference:' + str(p) for p in sorted(refs.glob('*.dll'))] + ['/reference:' + str(p) for p in extra]
    compiler_args.append(str(pathlib.Path(__file__).with_name('Verify.cs')))
    compiler_args.append(str(pathlib.Path(__file__).with_name('VerifyPopulation.cs')))
    compiler_args.append(str(pathlib.Path(__file__).with_name('VerifyMovement.cs')))
    (output / 'compile.rsp').write_text('\n'.join('"' + a + '"' for a in compiler_args), encoding='utf-8')
    subprocess.run([str(dotnet), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(output / 'compile.rsp')], check=True)
    for assembly in extra:
        shutil.copy2(assembly, output / assembly.name)
    (output / 'Verify.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}), encoding='utf-8')
    result = subprocess.run([str(dotnet), str(output / 'Verify.dll'), str(root), str(output)], check=False)
    print('Verification output:', output)
    return result.returncode


if __name__ == '__main__':
    raise SystemExit(main())
