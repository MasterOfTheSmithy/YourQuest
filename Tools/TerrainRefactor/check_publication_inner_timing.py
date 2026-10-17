"""Compile output-only timing overlays and check fixed scope lifetime with native profiling substituted."""
import argparse, hashlib, json, re, shutil, subprocess, xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument('--proposal', type=Path, required=True)
ap.add_argument('--compile', type=Path, required=True)
ap.add_argument('--out', type=Path, required=True)
args = ap.parse_args()
proposal, baseline, out = [(ROOT / p).resolve() for p in (args.proposal, args.compile, args.out)]
if out.exists() or not out.is_relative_to(ROOT / 'outputs'):
    ap.error('Use a fresh workspace outputs directory')
out.mkdir(parents=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
source_identity = json.loads((proposal / 'Source_Identity.json').read_text())
inputs = {}
checks = 0
failures = []
def check(ok, name):
    global checks
    checks += 1
    print(('PASS ' if ok else 'FAIL ') + name, flush=True)
    if not ok: failures.append(name)

overlays = {}
for relative, identity in source_identity['sources'].items():
    current = ROOT / relative
    replacement = proposal / (current.name + '.proposal.cs')
    preimage = proposal / (current.name + '.before')
    check(sha(current) == identity['before_sha256'] == sha(preimage), 'exact current preimage ' + current.name)
    check(sha(replacement) == identity['proposal_sha256'], 'exact proposal identity ' + current.name)
    inputs[str(current)] = sha(current)
    inputs[str(replacement)] = sha(replacement)
    overlays[str(current)] = replacement
    old, new = preimage.read_text(encoding='utf-8-sig'), replacement.read_text()
    # note: Diagnostics may add wrappers, but these acceptance/callback invocations must neither disappear nor duplicate.
    symbols = ['TryValidateAcceptedContinuationFunctions', 'TryValidateContinuationPopulationBindings',
        'JsonEqualsCaptured', 'TryFlushPendingAutosave', 'TryStageContinuationCommit',
        'TryValidateContinuationPublication', 'TryCaptureContinuationAuxiliary', 'SaveManifest',
        'TryRefreshPublishedContinuationProjections', 'SerializeObject', 'DeserializeObject',
        'TryGetAcceptedContinuationIdentity', 'TryValidateContinuationSnapshot']
    check(all(len(re.findall(r'\b' + s + r'\s*\(', old)) == len(re.findall(r'\b' + s + r'\s*\(', new)) for s in symbols),
          'acceptance callback and serializer call multiplicity unchanged ' + current.name)

check(source_identity['reverse_edits_restore_exact_source'], 'removing reviewed edits restores complete current sources')
pure = ROOT / 'Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs'
inputs[str(pure)] = sha(pure)
changed_lines = [line for line in (proposal / 'Proposal.patch').read_text().splitlines()
                 if line.startswith(('+', '-')) and not line.startswith(('+++', '---'))]
check(str(pure.relative_to(ROOT)).replace('\\', '/') not in source_identity['sources'] and
      not any('JsonEqualsCaptured' in line for line in changed_lines), 'pure JSON helper and call semantics unchanged')
base_receipt = json.loads((baseline / 'receipt.json').read_text())
check(base_receipt['status'] == 'PASS' and base_receipt['inputs_unchanged_at_completion'], 'baseline full source compile accepted')
for part in ('runtime', 'editor'):
    tree = ET.parse(baseline / part / 'Compile.csproj')
    for node in tree.findall('.//Compile'):
        path = Path(node.attrib['Include'])
        inputs[str(path)] = sha(path)
        if str(path) in overlays: node.set('Include', str(overlays[str(path)]))
    for node in tree.findall('.//Reference/HintPath'):
        path = Path(node.text)
        inputs[str(path)] = sha(path)
    for node in tree.findall('.//ProjectReference'):
        if Path(node.attrib['Include']).name == 'Compile.csproj':
            node.set('Include', str(out / 'runtime/Compile.csproj'))
    folder = out / part
    folder.mkdir()
    tree.write(folder / 'Compile.csproj', encoding='utf-8', xml_declaration=True)
    shutil.copyfile(baseline / part / 'NuGet.Config', folder / 'NuGet.Config')
run = subprocess.run(['dotnet', 'build', str(out / 'editor/Compile.csproj'), '--nologo', '-v:q'], cwd=ROOT, capture_output=True, text=True)
(out / 'compile.log').write_text(run.stdout + run.stderr)
print((run.stdout + run.stderr)[-3000:], flush=True)
check(run.returncode == 0, 'complete proposed runtime/editor compile against installed Unity references')

builder = (proposal / 'YQGeneratedWorldRuntimeBuilder.cs.proposal.cs').read_text()
helper_start = builder.index('    // note: Fixed Editor-only scopes')
helper_end = builder.index('    internal static T MeasureStartupOperation', helper_start)
helper = builder[helper_start:helper_end]
program = '''using System;
using System.Collections.Generic;
using System.Linq;
using D = YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics;
namespace Unity.Profiling {
    public readonly struct ProfilerMarker {
        readonly string name;
        public static readonly List<string> Events = new List<string>();
        public ProfilerMarker(string name) { this.name = name; Events.Add("register:"+name); }
        public AutoScope Auto() { Events.Add("begin:"+name); return new AutoScope(name); }
        public readonly struct AutoScope : IDisposable {
            readonly string name;
            public AutoScope(string name) { this.name = name; }
            public void Dispose() { Events.Add("end:"+name); }
        }
    }
}
public class YQGeneratedWorldRuntimeBuilder {
''' + helper + '''}
static class Program {
    static int checks;
    static void Check(bool value, string name) { checks++; if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); }
    static bool Bind(bool first, bool second, bool failThrow, List<string> calls) {
        using(D.Begin(D.Operation.SiteFunctions)) { calls.Add("site"); if(failThrow) throw new Exception("provider"); if(!first)return false; }
        using(D.Begin(D.Operation.PopulationBindings)) { calls.Add("population"); if(!second)return false; }
        calls.Add("owner"); return true;
    }
    static void Main() {
#if UNITY_EDITOR
        var names = YQGeneratedWorldRuntimeBuilder.PrimeFrontierPublicationProfilerMarkersForCpuCapture();
        Check(names.Count==13 && names.Distinct().Count()==13,"fixed13 unique native names");
        Check(Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("register:"))==13,"all native handles registered before capture");
        Check(Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("begin:"))==13 && Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("end:"))==13,"priming scopes balanced before capture");
        int registrations=Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("register:"));
        YQGeneratedWorldRuntimeBuilder.PrimeFrontierPublicationProfilerMarkersForCpuCapture();
        Check(registrations==Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("register:")),"repeated prime reuses fixed handles");
        bool readOnly=false; try { ((IList<string>)names)[0]="forged"; } catch(NotSupportedException) {readOnly=true;}
        Check(readOnly,"diagnostic names exposed read-only");
#endif
        foreach(bool first in new[]{false,true})foreach(bool second in new[]{false,true}) {
            var calls=new List<string>(); bool result=Bind(first,second,false,calls);
            var expected=new List<string>{"site"};if(first)expected.Add("population");if(first&&second)expected.Add("owner");
            Check(result==(first&&second) && calls.SequenceEqual(expected),"short-circuit order "+first+"/"+second);
        }
        var thrownCalls=new List<string>();bool thrown=false;
        try{Bind(true,true,true,thrownCalls);}catch(Exception e){thrown=e.Message=="provider";}
        Check(thrown && thrownCalls.SequenceEqual(new[]{"site"}),"provider error retains propagation and later callback exclusion");
        foreach(D.Operation operation in Enum.GetValues(typeof(D.Operation)))using(D.Begin(operation)){}
#if UNITY_EDITOR
        Check(Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("begin:"))==Unity.Profiling.ProfilerMarker.Events.Count(x=>x.StartsWith("end:")),"all13 scopes balanced including early return and throw");
#else
        Check(Unity.Profiling.ProfilerMarker.Events.Count==0,"nonEditor scope path performs no native registration or timing");
#endif
        Console.WriteLine("CHECKS "+checks+" FAILURES 0");
    }
}
'''
for label, defines in [('editor_scopes', 'UNITY_EDITOR'), ('player_scopes', '')]:
    folder = out / label
    folder.mkdir()
    (folder / 'Program.cs').write_text(program)
    (folder / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><DefineConstants>' + defines + '</DefineConstants></PropertyGroup></Project>')
    (folder / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    result = subprocess.run(['dotnet','run','--project',str(folder / 'Checks.csproj'),'--nologo'],cwd=ROOT,capture_output=True,text=True)
    (folder / 'run.log').write_text(result.stdout + result.stderr)
    print((result.stdout + result.stderr)[-2300:], flush=True)
    match = re.search(r'CHECKS (\d+) FAILURES 0', result.stdout)
    check(result.returncode == 0 and match is not None, 'source-extracted fixed scope controls ' + label)
    if match: checks += int(match.group(1))
changed = [p for p, digest in inputs.items() if not Path(p).exists() or sha(Path(p)) != digest]
check(not changed, 'all current/proposed/reference inputs unchanged at completion')
receipt = dict(utc=datetime.now(timezone.utc).isoformat(), status='PASS' if not failures else 'FAIL', checks=checks,
               failures=len(failures), failure_names=failures, changed_inputs=changed, input_sha256=inputs,
               runtime_sources=base_receipt['runtime_sources'], editor_sources=base_receipt['editor_sources'],
               evidence='DETACHED_FULL_PROPOSAL_COMPILE_AND_SOURCE_EXTRACTED_SCOPE_CONTROLS; native profiler substituted; not native timing or gameplay acceptance')
(out / 'Receipt.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps({k:v for k,v in receipt.items() if k!='input_sha256'}),flush=True)
raise SystemExit(bool(failures))
