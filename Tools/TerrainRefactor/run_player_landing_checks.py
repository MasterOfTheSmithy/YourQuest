"""Source-extracted current ragdoll lifecycle checks; native physics/rig creation explicitly substituted."""
import argparse, hashlib, json, re, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path
root=Path(__file__).resolve().parents[2]
ap=argparse.ArgumentParser(description=__doc__)
ap.add_argument('--out',required=True)
args=ap.parse_args();out=(root/args.out).resolve()
if not out.is_relative_to(root/'outputs') or out.exists():ap.error('--out must be a fresh outputs directory')
source=root/'Assets/Assets/Scripts/Tutorial/YQPlayerRagdoll.cs'
checks=root/'Tools/TerrainRefactor/PlayerLandingChecks.cs'
preimage=root/'outputs/TerrainRefactor_20261014_StreamingRetest/Grounding/Landing_Repair/YQPlayerRagdoll.cs.before'
rules=root/'Assets/Assets/Scripts/Tutorial/YQPlayerMotionRules.cs'
motor=root/'Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs'
vfx=root/'Assets/Assets/Scripts/Tutorial/YQGeneratedRuntimeVfx.cs'
inputs={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in (source,checks,preimage,rules,motor,vfx,Path(__file__))}
text=source.read_text();out.mkdir(parents=True)
def declaration(marker,text=text):
    a=text.index(marker);opening=text.index('{',a);semi=text.find(';',a)
    if '=>' in text[a:opening] and semi<opening:return text[a:semi+1]+'\n'
    i=opening+1;d=1
    while d:d+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[a:i]+'\n'
header=text[:text.index('    private static readonly HumanBodyBones[] Bones')]
body=header+'    private bool EnsureRig() => TestRigReady;\n    public bool TestRigReady=true;\n'
for marker in ('    public bool TryBegin(', '    private void FixedUpdate(', '    public bool TryRecover(',
    '    public void ObserveContact(', '    private bool TryGroundedPosition(', '    public void StopPhysics(',
    '    private bool IsOwnedTransform(', '    internal YQInvestorPlayerMotor ResolveDetachedMotor(', '    private void DetachSimulationRoot(', '    private void RestoreSimulationRoot(',
    '    private void RestoreCapsuleOwnership(', '    public void ResetForRespawn(', '    internal void ResetForTerrainRecovery(',
    '    private static bool Finite(', '    internal static bool IsPhysicsStepSafe(', '    private void OnDisable()', '    private void OnDestroy()'):
    body+=declaration(marker)
for marker,name in (('    public bool TryBegin(','OriginalTryBegin'),('    private void FixedUpdate(','OriginalFixedUpdate'),('    public void ResetForRespawn(','OriginalResetForRespawn')):
    old=declaration(marker,preimage.read_text())
    old=old.replace(marker,marker[:marker.rfind(' ')+1]+name+'(')
    body+=old
body+='}\n'
body+=declaration('public sealed class YQPlayerRagdollContact',text)
body+='public partial class YQInvestorPlayerMotor {\n'+declaration('    public static YQInvestorPlayerMotor ResolveFromTransform(',motor.read_text())+declaration('    public static YQInvestorPlayerMotor ResolveFromCollider(',motor.read_text())+'}\n'
body+='public class VfxDamageProbe { public UnityEngine.GameObject source; public int damage;\n'+declaration('    private bool TryApplyDamage(',vfx.read_text()).replace('private bool','public bool',1)+'}\n'
(out/'Extracted.cs').write_text(body)
(out/'Checks.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Extracted.cs"/><Compile Include="{checks}"/><Compile Include="{rules}"/></ItemGroup></Project>')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/></packageSources></configuration>')
build=subprocess.run(['dotnet','build',str(out/'Checks.csproj'),'--nologo','-v:q'],capture_output=True,text=True)
(out/'compile.log').write_text(build.stdout+build.stderr)
run=subprocess.run(['dotnet',str(out/'bin/Debug/net10.0/Checks.dll')],capture_output=True,text=True) if not build.returncode else None
log=(run.stdout+run.stderr) if run else build.stdout+build.stderr
(out/'run.log').write_text(log);print(log)
m=re.search(r'RESULT checks=(\d+) failures=(\d+)',log);count=int(m[1]) if m else 0;failures=int(m[2]) if m else 1
changed=[str(p) for p,h in inputs.items() if hashlib.sha256(p.read_bytes()).hexdigest()!=h]
code=build.returncode or (run.returncode if run else 1) or bool(changed or not count or failures)
(out/'Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS' if not code else 'FAIL',checks=count,failures=failures,changed_inputs=changed,input_sha256={str(p):h for p,h in inputs.items()},evidence='CURRENT_SOURCE_EXTRACTED_RAGDOLL_HANDOFF_HIERARCHY_AND_CONTACT_OWNERSHIP; RIG_CREATION/NATIVE_PHYSICS/ANIMATION/STREAMING_SUBSTITUTED; NO_UNITY_OR_PHYSICAL_LANDING_CERTIFICATION'),indent=2))
sys.exit(code)
