"""Record current native-reach evidence without promoting disposable fixtures to gameplay acceptance."""
import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'outputs/TerrainRefactor_20261011'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bind_unity(receipt):
    # note: Match current source and the actual loaded runtime assembly; failed fixtures remain failures in the checkpoint.
    assert all(sha(ROOT / name) == value for name, value in receipt['sourceSha256'].items()), 'Stale Unity source binding'
    assert sha(ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll') == receipt['runtimeAssemblySha256'], 'Stale Unity runtime'


parser = argparse.ArgumentParser()
for option in ('compile', 'basins', 'profiles', 'regressions', 'fields'):
    parser.add_argument('--' + option, required=True)
args = parser.parse_args()
compiled = read(EVIDENCE / args.compile / 'receipt.json')
basins = read(EVIDENCE / args.basins / 'Receipt.json')
profiles = read(EVIDENCE / args.profiles / 'Receipt.json')
regressions = read(EVIDENCE / args.regressions / 'drainage-receipt.json')
fields = read(EVIDENCE / args.fields / 'native-reach-receipt.json')
assert compiled['status'] == 'PASS' and compiled['inputs_unchanged_at_completion']
assert all(sha(ROOT / name) == value for name, value in compiled['source_sha256'].items()), 'Stale detached source'
for receipt in (basins, profiles):
    bind_unity(receipt)
assert basins['passed'] and profiles['inputsUnchanged']
assert sha(ROOT / profiles['input']) == profiles['inputSha256']
for folder in (args.regressions, args.fields):
    identity = read(EVIDENCE / folder / 'identity.json')
    assert identity['inputs_unchanged'] and identity['run_exit'] == 0
    assert identity['compiled_source_receipt'] == str((EVIDENCE / args.compile / 'receipt.json').resolve())
    assert all(sha(Path(name)) == value for name, value in identity['input_sha256'].items()), 'Stale detached inputs'
assert regressions['passed'] and fields['passed']
result = {
    'utc': datetime.now(timezone.utc).isoformat(), 'status': 'INCOMPLETE',
    'candidate': 'version-8 connected native finite reaches with final channel-detail protection',
    'sourceSha256': basins['sourceSha256'],
    'receipts': vars(args),
    'compile': {'passed': True, 'runtimeSources': compiled['runtime_sources'], 'editorSources': compiled['editor_sources']},
    'detached': {'checks': len(regressions['checks']), 'connectivityChecks': len(fields['checks']), 'passed': True},
    'unity': {'checks': len(basins['checks']), 'passed': True},
    'profiles': {key: value for key, value in profiles.items() if key not in ('sourceSha256', 'checks')},
    'mesh': read(EVIDENCE / args.basins / 'native-channel-meshes.json'),
    'detail': read(EVIDENCE / args.basins / 'final-channel-detail.json'),
    'preserved': ['accepted versions 1-7', 'seed namespaces', 'accepted controls and IDs', 'existing runtime owners', 'saves, scenes, asset GUIDs'],
    'remaining': ['resolve any reported sampler/mesh edge coverage discrepancies', 'terminal continuation mesh handoff', 'final graded terrain and collider agreement', 'waves and mixed LOD',
                  'ordinary new-draft startup and travel', 'stages A-E and all fifteen full acceptance gates'],
    'evidenceBoundary': 'Disposable Editor meshes and detached checks; not ordinary travel, standalone performance, coast or complete milestone proof.'
}
(EVIDENCE / 'NativeReachVerification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
path = ROOT / 'Docs/TerrainRefactor_2026-10-11/Checkpoint.json'
checkpoint = read(path)
checkpoint['observedOn'] = result['utc']
checkpoint['freshNativeReachEvidence'] = result
checkpoint['source']['runtimeAccess'] = 'AUTHORIZED. Current native reach fixture evidence is in freshNativeReachEvidence. The stricter mesh handoff result is ' + ('PASS' if profiles['handoffPassed'] else 'FAIL') + '. Final collider, terminal rays, ordinary travel and full staged acceptance remain incomplete.'
checkpoint['nextConcreteStep'] = 'Resolve the remaining sampler/mesh edge coverage discrepancies in the current handoff-witnesses.json, preserving archived behavior. Then verify terminal continuation meshes and final graded terrain/colliders before ordinary disposable new-draft travel. Continue stages A-E and all fifteen gates.'
checkpoint['freshVerification']['evidenceIsHistorical'] = True
checkpoint['freshWaterClearanceEvidence']['evidenceIsHistorical'] = True
for stage in checkpoint['stages']:
    if stage['id'] == 'B':
        stage['implemented'] = 'Version-8 new drafts derive connected finite native-channel wet polygons and placement clearance from the existing source height owner; final detail respects accepted channel grading. Archived versions 1-7 retain their interpretation. See freshNativeReachEvidence for current passing/failing measurements.'
        stage['nextGate'] = checkpoint['nextConcreteStep']
path.write_text(json.dumps(checkpoint, indent=2), encoding='utf-8')
print(json.dumps({'status': result['status'], 'unity': result['unity'], 'detached': result['detached'], 'profilePassed': profiles['passed']}))
