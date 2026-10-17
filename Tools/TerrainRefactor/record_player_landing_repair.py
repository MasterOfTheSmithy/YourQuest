"""Save exact owned ragdoll repair patch and source-bound detached evidence without changing production files."""
import difflib,hashlib,json,subprocess
from datetime import datetime,timezone
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_StreamingRetest/Grounding/Landing_Repair'
source=root/'Assets/Assets/Scripts/Tutorial/YQPlayerRagdoll.cs'
before=out/'YQPlayerRagdoll.cs.before'
test=out/'checks_final/Receipt.json'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads(test.read_text())
assert receipt['status']=='PASS' and receipt['checks']>0 and not receipt['failures'] and not receipt['changed_inputs']
assert all(digest(Path(p))==h for p,h in receipt['input_sha256'].items())
assert not (out/'Final_Receipt.json').exists()
label='Assets/Assets/Scripts/Tutorial/YQPlayerRagdoll.cs'
patch=''.join(difflib.unified_diff(before.read_text().splitlines(True),source.read_text().splitlines(True),fromfile='a/'+label,tofile='b/'+label))
assert patch
if (out/'Repair.patch').exists():assert (out/'Repair.patch').read_text()==patch,'Existing patch differs; retain it and choose a fresh output'
else:(out/'Repair.patch').write_text(patch)
check=subprocess.run(['git','apply','--ignore-space-change','--reverse','--check',str(out/'Repair.patch')],cwd=root,capture_output=True,text=True)
(out/'patch_check.log').write_text(check.stdout+check.stderr)
assert not check.returncode
preserved={}
for name in ('YQInvestorPlayerMotor.cs','YQInvestorPlayerMotor.Traversal.cs','YQPlayerMotionRules.cs'):
    a=out/(name+'.before');b=root/'Assets/Assets/Scripts/Tutorial'/name
    assert digest(a)==digest(b),'Unexpected change outside owned ragdoll: '+name
    preserved[name]=digest(b)
(out/'Final_Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=receipt['checks'],failures=0,patch_reverse_check='PASS',production_source=str(source),preimage_sha256=digest(before),current_sha256=digest(source),patch_sha256=digest(out/'Repair.patch'),detached_receipt=str(test),preserved_player_sources=preserved,evidence=receipt['evidence'],limitations='Native solver/animation/rig admission substituted; full integrated compile owned by parent; no Unity/physical drop/tunneling/speed acceptance claim'),indent=2))
print('Saved exact repair patch and detached receipt: '+str(out))
