"""Run unchanged source validators on saved proposals; never apply game state."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

import run as bench


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    parser.add_argument('--kind', choices=['goddess', 'structured'], required=True)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--prefix', default='')
    args = parser.parse_args()
    out = Path(args.out).resolve()
    if bench.ROOT not in out.parents or not re.fullmatch('[a-z0-9_-]+', args.tag):
        raise ValueError('Use a workspace output directory and a safe unique tag')
    name = 'GoddessSourceHarness' if args.kind == 'goddess' else 'StructuredSourceHarness'
    home = out / name
    executable = home / 'bin/Release/net10.0' / (name + '.dll')
    raw_directory = home / ('Raw_' + args.tag)
    check_directory = home / ('Checks_' + args.tag)
    raw_directory.mkdir(exist_ok=False)
    check_directory.mkdir(exist_ok=False)
    mappings = dict(origin_current_schema_synthetic='origin', world_current_schema_synthetic='world',
        frontier_settlement_compatible_replay='frontier', quest_empty_evidence_compatible_replay='quest')
    measurements = [json.loads(line) for line in (out / 'measurements.jsonl').read_text(encoding='utf-8').splitlines()]
    rows = []
    # note: Validators read exact saved raw text. They check only their documented detached source boundary.
    for measurement in measurements:
        fixture = measurement['fixture']
        if args.prefix and not measurement['serverLabel'].startswith(args.prefix):
            continue
        if args.kind == 'goddess' and measurement.get('track') != 'goddess_current_source':
            continue
        if args.kind == 'structured' and fixture not in mappings:
            continue
        identity = measurement['serverLabel'] + '_' + fixture + '_' + str(measurement.get('sample', 0))
        if not re.fullmatch('[A-Za-z0-9_-]+', identity):
            raise ValueError('Unsafe diagnostic response identity')
        raw_file = raw_directory / (identity + '.json')
        result_file = check_directory / (identity + '.json')
        raw = measurement.get('raw', '')
        with raw_file.open('x', encoding='utf-8', newline='') as handle:
            handle.write(raw)
        command = ['dotnet', str(executable), str(home), 'check',
            fixture if args.kind == 'goddess' else mappings[fixture], str(raw_file), str(result_file)]
        completed = subprocess.run(command, capture_output=True, text=True, timeout=30)
        if completed.returncode not in [0, 2] or not result_file.exists():
            raise RuntimeError('Source checker failed: ' + identity + ': ' + completed.stderr)
        report = json.loads(result_file.read_text(encoding='utf-8-sig'))
        rows.append(dict(modelId=measurement['modelId'], condition=measurement['serverLabel'],
            fixture=fixture, sample=measurement.get('sample', 0), rawSha256=hashlib.sha256(raw.encode('utf-8')).hexdigest(),
            transportCompleted=measurement.get('success'), terminalDone=measurement.get('streamDone',
                measurement.get('completionMetadata', {}).get('done')), result=report,
            resultFile=str(result_file.relative_to(out))))
    destination = out / ('SourceChecks_' + args.tag + '.json')
    with destination.open('x', encoding='utf-8') as handle:
        json.dump(dict(rows=rows, evidence='Unchanged, fingerprint-verified partial source validators; '
            'not full production acceptance, factual approval, or game-state mutation.'), handle, ensure_ascii=False, indent=2)
    print(json.dumps(dict(rows=len(rows), destination=str(destination), kind=args.kind)))


if __name__ == '__main__':
    main()
