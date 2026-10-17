"""Record the exact authorized detached-body actor adapters and current-source checks."""
import difflib,hashlib,json,subprocess
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_StreamingRetest/Grounding/Landing_Actor_Repair'
checkPath=out/'checks_1/Receipt.json';checks=json.loads(checkPath.read_text())
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert checks['status']=='PASS' and checks['checks']>0 and not checks['failures'] and not checks['changed_inputs']
assert all(digest(Path(p))==h for p,h in checks['input_sha256'].items())
owned=('Tutorial/YQPlayerRagdoll.cs','Tutorial/YQInvestorPlayerMotor.cs','Tutorial/YQInvestorEnemy.cs','Tutorial/YQInvestorEnemySpawner.cs','Tutorial/YQDeveloperToolkitCommands.cs','Tutorial/YQInvestorDialogueUI.cs','Tutorial/YQGeneratedRuntimeVfx.cs','Dialogue/DialogueBoxUI.cs')
sources={};patch=''
for relative in owned:
    label='Assets/Assets/Scripts/'+relative;current=root/label;before=out/(current.name+'.before')
    sources[label]={'before_sha256':digest(before),'current_sha256':digest(current)}
    difference=''.join(difflib.unified_diff(before.read_text().splitlines(True),current.read_text().splitlines(True),fromfile='a/'+label,tofile='b/'+label))
    assert difference,'No owned change: '+relative
    patch+=difference
assert not (out/'Integration.patch').exists() and not (out/'Final_Receipt.json').exists()
(out/'Integration.patch').write_text(patch)
command=subprocess.run(['git','apply','--ignore-space-change','--reverse','--check',str(out/'Integration.patch')],cwd=root,capture_output=True,text=True)
(out/'patch_check.log').write_text(command.stdout+command.stderr);assert not command.returncode
(out/'Final_Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=checks['checks'],failures=0,patch_reverse_check='PASS',production_sources=sources,patch_sha256=digest(out/'Integration.patch'),detached_receipt=str(checkPath),physical_handoff_patch=str(out.parent/'Landing_Repair/Repair.patch'),evidence=checks['evidence'],limitations='Native/rig/animation/physics/streaming substituted; direct current resolver and VFX damage dispatch exercised; full integrated compile/runtime owned by parent; source caller adapters inspected, native imported rig and all-speed contact pending'),indent=2))
print('Saved 105-check actor integration receipt and exact eight-file adapter patch')
