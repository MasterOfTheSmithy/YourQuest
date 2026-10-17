"""Compare two committed terrain snapshots without opening Unity or modifying a profile."""
import argparse
import array
import base64
import hashlib
import json
import sys
import zlib
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read_snapshot(path):
    source = path.read_bytes()
    record = json.loads(source.decode('utf-8-sig'))
    # note: Verify the saved payload before measuring geometry; a matching label cannot certify corrupted terrain.
    raw = zlib.decompress(base64.b64decode(record['compressedHeightmapBase64'], validate=True), -15)
    digest = hashlib.sha256(raw).hexdigest()
    expected = record['heightmapResolution'] ** 2 * 2
    if not record['hasTerrain'] or len(raw) != expected or len(raw) != record['uncompressedByteCount'] or digest != record['heightmapChecksum']:
        raise ValueError('Invalid terrain snapshot: ' + str(path))
    heights = array.array('H')
    heights.frombytes(raw)
    if sys.byteorder != 'little':
        heights.byteswap()
    return record, heights, hashlib.sha256(source).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--before', required=True, type=Path)
    parser.add_argument('--after', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    output = args.out.resolve()
    if output.exists() or not output.is_relative_to((ROOT / 'outputs/TerrainRefactor_20261011').resolve()):
        raise ValueError('Choose a new receipt under outputs/TerrainRefactor_20261011')
    before, first, first_hash = read_snapshot(args.before.resolve())
    after, second, second_hash = read_snapshot(args.after.resolve())
    keys = ['schemaVersion', 'ownerProfileId', 'worldId', 'worldSeed', 'planFingerprint',
            'terrainGenerationVersion', 'heightmapResolution', 'sizeX', 'sizeY', 'sizeZ',
            'positionX', 'positionY', 'positionZ']
    mismatches = [key for key in keys if before[key] != after[key]]
    # note: Different identities or grids are rejected, rather than comparing unrelated terrain samples.
    if mismatches:
        raise ValueError('Snapshot identity/grid mismatch: ' + ', '.join(mismatches))
    changed = 0
    maximum_units = 0
    for a, b in zip(first, second):
        delta = abs(int(a) - int(b))
        changed += delta != 0
        maximum_units = max(maximum_units, delta)
    maximum_metres = maximum_units * before['sizeY'] / 65535
    unchanged = changed == 0 and before['heightmapChecksum'] == after['heightmapChecksum']
    receipt = dict(utc=datetime.now(timezone.utc).isoformat(), status='PASS' if unchanged else 'FAIL',
                   evidence='READONLY_DECODED_COMMITTED_HEIGHTFIELD_COMPARISON',
                   before=str(args.before.resolve()), after=str(args.after.resolve()),
                   beforeFileSha256=first_hash, afterFileSha256=second_hash,
                   beforeChecksum=before['heightmapChecksum'], afterChecksum=after['heightmapChecksum'],
                   identity={key: before[key] for key in keys}, changedSamples=changed,
                   totalSamples=len(first), maximumDeltaMetres=maximum_metres,
                   withinOneCentimetre=maximum_metres <= .01, unchangedChecksum=unchanged,
                   limits='Committed quantized heightfield only; no streamed seams, collision, visual, traversal or all-profile persistence certification.')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: receipt[key] for key in ['status', 'changedSamples', 'maximumDeltaMetres', 'unchangedChecksum']}))
    return 0 if unchanged else 1


if __name__ == '__main__':
    raise SystemExit(main())
