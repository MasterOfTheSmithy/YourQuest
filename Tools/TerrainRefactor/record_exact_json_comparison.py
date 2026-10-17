"""Archive the final applied exact-comparison change and current-source detached receipt."""
from pathlib import Path
import difflib,hashlib,json,subprocess
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_StreamingRetest/ExactJsonComparison_Reviewed'
checksPath=out/'final_current_checks_fixed/Receipt.json';checks=json.loads(checksPath.read_text())
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert checks['status']=='PASS' and checks['checks']>0 and not checks['failures'] and not checks['changed_inputs']
assert all(digest(Path(p))==h for p,h in checks['input_sha256'].items())
paths=('Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs','Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs','Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs')
assert not (out/'Repair.patch').exists() and not (out/'Final_Receipt.json').exists()
patch='';sources={}
for relative in paths:
    current=root/relative;before=out/(current.name+'.before');after=out/(current.name+'.after')
    after.write_bytes(current.read_bytes());sources[relative]={'before_sha256':digest(before),'current_sha256':digest(after)}
    patch+=''.join(difflib.unified_diff(before.read_text().splitlines(True),after.read_text().splitlines(True),fromfile='a/'+relative,tofile='b/'+relative))
(out/'Repair.patch').write_text(patch)
result=subprocess.run(['git','apply','--ignore-space-change','--reverse','--check',str(out/'Repair.patch')],cwd=root,capture_output=True,text=True)
(out/'patch_check.log').write_text(result.stdout+result.stderr);assert not result.returncode
(out/'Final_Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=checks['checks'],failures=0,source_state='CURRENT_PRODUCTION',source_sha256=sources,patch_sha256=digest(out/'Repair.patch'),patch_reverse_check='PASS',test_receipt=str(checksPath),review=str(out/'Final_Review.md'),evidence=checks['evidence'],limitations='No full integrated compilation/import/runtime claim; full serializer traversal remains; allocation reduction is not a hitch cure.'),indent=2))
print('Final exact comparison patch, source receipt and review saved')
