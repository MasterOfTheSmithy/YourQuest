"""Capture the existing 79-check Editor suite without changing tests or source."""
import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from update_joined_native_checkpoint import ROOT, EVIDENCE, read, sha

parser = argparse.ArgumentParser()
parser.add_argument('phase', choices=('before', 'after'))
parser.add_argument('--out', default='joined_native_contract_suite')
parser.add_argument('--basins', default='20261011_141344_796_unity_basins')
args = parser.parse_args()
destination = EVIDENCE / args.out
log = Path('C:/Users/Garri/AppData/Local/Unity/Editor/Editor.log')
if args.phase == 'before':
    destination.mkdir()
    binding = read(EVIDENCE / args.basins / 'Receipt.json')
    binding['sourceSha256']['Assets/Assets/Scripts/Generated/Editor/YQWorldGenerationV2ContractTests.cs'] = sha(
        ROOT / 'Assets/Assets/Scripts/Generated/Editor/YQWorldGenerationV2ContractTests.cs')
    binding['logStart'] = log.stat().st_size
    (destination / 'Before.json').write_text(json.dumps(binding, indent=2), encoding='utf-8')
    print(binding['logStart'])
else:
    binding = read(destination / 'Before.json')
    with log.open('rb') as stream:
        stream.seek(binding['logStart'])
        fresh = stream.read().decode('utf-8', errors='replace')
    terminal = re.findall(r'\[YQWorldGenV2Tests\].*?Tested\s+(\d+).*?failures\s*=\s*(\d+)', fresh, re.I)
    if not terminal:
        raise ValueError('No new completed suite terminal yet')
    checks, failures = map(int, terminal[-1])
    reasons = [line.strip() for line in fresh.splitlines() if line.startswith('[YQWorldGenV2Tests]') and
               'Tested' not in line and (': ' in line)]
    prior = read(EVIDENCE / 'wet_footprint_contract_suite/Receipt.json')
    unchanged = all(sha(ROOT / path) == expected for path, expected in binding['sourceSha256'].items())
    unchanged &= sha(ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll') == binding['runtimeAssemblySha256']
    # note: Retain the complete fresh log and exact failures, even when they match the earlier baseline; a known failure is still a failure.
    (destination / 'Editor.log').write_text(fresh, encoding='utf-8')
    receipt = dict(utc=datetime.now(timezone.utc).isoformat(), checks=checks, failures=failures,
        verdict='PASS' if failures == 0 else 'FAIL', reasons=reasons, inputsUnchanged=unchanged,
        baselineFailuresUnchanged=reasons == prior['reasons'], sourceSha256=binding['sourceSha256'],
        runtimeAssemblySha256=binding['runtimeAssemblySha256'], runtimeAssemblyMvid=binding['runtimeAssemblyMvid'],
        logStart=binding['logStart'], logEnd=log.stat().st_size, evidence='UNITY_EDITOR_EXISTING_V2_CONTRACT_SUITE')
    (destination / 'Receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    print(json.dumps(receipt, indent=2))
