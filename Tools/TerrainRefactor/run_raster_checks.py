"""Compare the actual old/new raster bodies with deterministic managed dependencies; no Unity."""
import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[2]
before = root / 'outputs/TerrainRefactor_20261013_Raster/Before_YQGeneratedWorldEnvironment.cs'
after = root / 'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs'
checks = root / 'Tools/TerrainRefactor/RasterChecks.cs'
output = root / 'outputs/TerrainRefactor_20261013_Raster/checks' / (sys.argv[1] if len(sys.argv) > 1 else 'baseline')
assert output.resolve().is_relative_to((root / 'outputs/TerrainRefactor_20261013_Raster/checks').resolve())
assert not output.exists(), 'Keep earlier evidence; use a fresh directory for a new check run.'
output.mkdir(parents=True)
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
inputs = {path: digest(path) for path in (before, after, checks, Path(__file__))}

def body(source):
    method = source.index('public static IEnumerator RepairLivedPathTerrainRoutine(')
    start = source.index('var roadTargets =', method)
    end = source.index('// note: Mark the transition out of route rasterization', start)
    return source[start:end]

old = before.read_text(encoding='utf-8-sig')
new = after.read_text(encoding='utf-8-sig')
cache_name = 'LivedPathRasterWaterSample'
cache_start = new.index('private struct ' + cache_name)
brace = new.index('{', cache_start)
depth = 1
end = brace + 1
while depth:
    depth += (new[end] == '{') - (new[end] == '}')
    end += 1
cache = new[cache_start:end]
template = checks.read_text(encoding='utf-8-sig')
source = template.replace('/*__CACHE__*/', cache).replace('/*__OLD_BODY__*/', body(old)).replace('/*__NEW_BODY__*/', body(new))
(output / 'ExtractedChecks.cs').write_text(source)
(output / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="ExtractedChecks.cs" /></ItemGroup></Project>')
(output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
build = subprocess.run(['dotnet', 'build', str(output / 'Checks.csproj'), '--nologo', '-v:q'], capture_output=True, text=True)
(output / 'compile.log').write_text(build.stdout + build.stderr)
run = None
if build.returncode == 0:
    run = subprocess.run(['dotnet', str(output / 'bin/Debug/net10.0/Checks.dll')], capture_output=True, text=True)
    (output / 'run.log').write_text(run.stdout + run.stderr)
    print(run.stdout + run.stderr)
else:
    print(build.stdout + build.stderr)
summary = re.search(r'^RESULT checks=(\d+) failures=(\d+)\s*$', run.stdout, re.MULTILINE) if run else None
changed = [str(path) for path, value in inputs.items() if digest(path) != value]
count = int(summary.group(1)) if summary else 0
failures = int(summary.group(2)) if summary else 1
code = build.returncode or (run.returncode if run else 1) or (2 if changed or not count or failures else 0)
(output / 'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(), status='PASS' if code == 0 else 'FAIL', checks=count, failures=failures, changed_inputs=changed,
    input_sha256={str(path): value for path, value in inputs.items()},
    extraction='Entire production raster loop body from preimage and current source, plus current production cache struct.',
    evidence='SOURCE_EXTRACTED_COMPILED_RASTER_EQUALITY_QUERY_REUSE_YIELD_FAIRNESS; deterministic dependency stubs; no native Unity, accepted-save runtime, or wall-clock proof.'), indent=2))
sys.exit(code)
