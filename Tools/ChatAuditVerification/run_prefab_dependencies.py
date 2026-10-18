#!/usr/bin/env python3
"""Compile the actual prefab dependency builder against controlled Editor APIs; never Unity certification."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_BUILDER = ROOT / "Assets/Assets/Scripts/Generated/Editor/YQRuntimePrefabDependencyBuilder.cs"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet-root", type=pathlib.Path, required=True)
    parser.add_argument("--builder-source", type=pathlib.Path, default=DEFAULT_BUILDER,
                        help="Optional original production source for the same moved-binding regression.")
    parser.add_argument("--case", help="Run one named group; the default runs the complete focused suite.")
    parser.add_argument("--output", type=pathlib.Path,
                        help="Retain compile/test logs, source hashes, commands, and the managed executable in this directory.")
    args = parser.parse_args()
    dotnet = args.dotnet_root.resolve()
    sdk = sorted((dotnet / "sdk").iterdir())[-1]
    refs = sorted((dotnet / "packs/Microsoft.NETCore.App.Ref").iterdir())[-1] / "ref/net8.0"
    runtime = sorted((dotnet / "shared/Microsoft.NETCore.App").iterdir())[-1].name
    temporary = tempfile.TemporaryDirectory(prefix="yourquest-prefab-contracts-") if args.output is None else None
    out = pathlib.Path(temporary.name) if temporary else args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    sources = [args.builder_source.resolve(), ROOT / "Assets/Assets/Scripts/Generated/YQRuntimePrefabDependency.cs",
               ROOT / "Tools/ChatAuditVerification/PrefabDependencyBuilderStubs.cs",
               ROOT / "Tools/ChatAuditVerification/PrefabDependencyBuilderTests.cs"]
    # note: The complete actual builder is compiled; only Unity's Editor/import boundary is replaced by explicit fixture contracts.
    response = ["-nologo", "-langversion:9", "-define:UNITY_EDITOR", "-target:exe", '-out:"' + str(out / "contracts.dll") + '"']
    response += ['-r:"' + str(path) + '"' for path in sorted(refs.glob("*.dll"))]
    response += ['-r:"' + str(sdk / "Newtonsoft.Json.dll") + '"']
    response += ['"' + str(path) + '"' for path in sources]
    (out / "compile.rsp").write_text("\n".join(response) + "\n")
    compile_command = [str(dotnet / "dotnet"), str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(out / "compile.rsp")]
    test_command = [str(dotnet / "dotnet"), str(out / "contracts.dll")] + ([args.case] if args.case else [])
    receipt = {"evidence": "Actual production C# against controlled Editor API contracts; no Unity import, native loading, or performance claim.",
               "sdk": sdk.name, "runtime": runtime, "case": args.case or "all", "compile_command": compile_command,
               "test_command": test_command,
               "source_sha256": {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in sources}}
    try:
        compiled = subprocess.run(compile_command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (out / "compile.log").write_text(compiled.stdout)
        print(compiled.stdout, end="")
        receipt["compile_exit_code"] = compiled.returncode
        code = compiled.returncode
        if code == 0:
            (out / "contracts.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
                "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
            shutil.copyfile(sdk / "Newtonsoft.Json.dll", out / "Newtonsoft.Json.dll")
            tested = subprocess.run(test_command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            (out / "test.log").write_text(tested.stdout)
            print(tested.stdout, end="")
            receipt["test_exit_code"] = tested.returncode
            code = tested.returncode
        receipt["exit_code"] = code
        (out / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
        if args.output:
            print("Managed verification receipt: " + str(out / "receipt.json"))
        return code
    finally:
        if temporary:
            temporary.cleanup()


if __name__ == "__main__":
    raise SystemExit(main())
