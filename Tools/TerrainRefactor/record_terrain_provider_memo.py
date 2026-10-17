"""Archive the exact tested compatibility memo patch; never edits production or profile files."""
from pathlib import Path
import difflib,hashlib,json,subprocess
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_StreamingRetest/TerrainProviderCompatibilityMemo'
receipt=json.loads((out/'checks_1/Receipt.json').read_text())
source=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs'
before=out/'YQGeneratedWorldRuntimeBuilder.cs.before'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert receipt['status']=='PASS' and receipt['checks']>0 and not receipt['failures'] and not receipt['changed_inputs']
assert digest(source)==receipt['input_sha256'][str(source)]
assert not (out/'Repair.patch').exists() and not (out/'Final_Receipt.json').exists()
after=out/'YQGeneratedWorldRuntimeBuilder.cs.after';after.write_bytes(source.read_bytes())
label='Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs'
(out/'Repair.patch').write_text(''.join(difflib.unified_diff(before.read_text().splitlines(True),after.read_text().splitlines(True),fromfile='a/'+label,tofile='b/'+label)))
result=subprocess.run(['git','apply','--ignore-space-change','--reverse','--check',str(out/'Repair.patch')],cwd=root,capture_output=True,text=True)
(out/'patch_check.log').write_text(result.stdout+result.stderr);assert not result.returncode
(out/'Final_Receipt.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),status='PASS',checks=receipt['checks'],failures=0,preimage_sha256=digest(before),tested_source_sha256=digest(after),patch_sha256=digest(out/'Repair.patch'),patch_reverse_check='PASS',receipt=str(out/'checks_1/Receipt.json'),evidence=receipt['evidence'],performance='Hypothesis only: three unchanged provider captures decode once instead of three; cache-warm captures decode zero times. Native time savings and total hitch reduction unmeasured.'),indent=2))
print('Saved exact tested two-entry terrain-provider memo patch')
