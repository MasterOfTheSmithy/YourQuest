"""Verify the additive resource layout against Unity YAML using an independent parser."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / 'Assets/Assets/Resources'
OUT = ROOT / 'outputs/UnityMemoryRepair_20261008'


class UnityLoader(yaml.SafeLoader):
    pass


UnityLoader.add_multi_constructor('tag:unity3d.com,2011:', lambda loader, tag, node: loader.construct_mapping(node))


def document(path):
    return yaml.load(path.read_text(encoding='utf-8-sig'), Loader=UnityLoader)['MonoBehaviour']


def normalized_hash(path):
    return hashlib.sha256(path.read_text(encoding='utf-8-sig').replace('\r\n', '\n').encode()).hexdigest()


def verify():
    receipt = json.loads((OUT / 'GranularBuildReceipt.json').read_text(encoding='utf-8-sig'))
    manifest = json.loads((RESOURCES / 'YQWorldAssetManifest.json').read_text(encoding='utf-8-sig'))
    assert manifest['version'] == 1
    expected = {}
    for path, digest in receipt['sourceShards'].items():
        source = ROOT / path
        assert normalized_hash(source) == digest, f'Legacy shard changed: {path}'
        for entry in document(source)['entries']:
            key = entry['assetPath']
            assert key not in expected, f'Duplicate parsed source path: {key}'
            expected[key] = entry
    seen = set()
    guids = set()
    overrides = 0
    # note: Compare parsed prefab/material file IDs, GUIDs, and every override against the original authoring shards.
    for item in manifest['assets']:
        path = item['path']
        assert path in expected and path not in seen, f'Unexpected or duplicate manifest path: {path}'
        seen.add(path)
        leaf_path = RESOURCES / (item['resource'] + '.asset')
        leaf = document(leaf_path)
        assert leaf['entries'] == [expected[path]], f'Binding changed: {path}'
        assert leaf['useLazyResourceShards'] == 0 and leaf['useGranularResourceShards'] == 0
        assert normalized_hash(leaf_path) == item['leafSha256']
        guid = yaml.safe_load(Path(str(leaf_path) + '.meta').read_text())['guid']
        assert guid not in guids, f'Duplicate new GUID: {guid}'
        guids.add(guid)
        overrides += len(expected[path].get('materialOverrides') or [])
    assert seen == set(expected), 'Manifest omitted existing bindings'
    before = document(OUT / 'RootBeforeGranular.asset.txt')
    after = document(RESOURCES / 'YQRuntimeWorldAssetRegistry.asset')
    before['useGranularResourceShards'] = 1
    assert before == after, 'Root changed beyond granular routing flag'
    preimages = json.loads((OUT / 'Preimages/Hashes.json').read_text(encoding='utf-8-sig'))
    root_meta = RESOURCES / 'YQRuntimeWorldAssetRegistry.asset.meta'
    original_meta = next(item for item in preimages if Path(item['Path']) == root_meta)
    assert hashlib.sha256(root_meta.read_bytes()).hexdigest().upper() == original_meta['Hash']
    return dict(status='PASS', bindings=len(seen), legacyShards=len(receipt['sourceShards']), materialOverrides=overrides,
                existingRootGuidPreserved=True, rootOnlyChange='useGranularResourceShards: 1',
                evidence='Independent YAML parser: every serialized binding equals its legacy source; no visual/gameplay claim.')


if __name__ == '__main__':
    try:
        result = verify()
    except Exception as error:
        result = dict(status='FAIL', error=str(error))
    result['utc'] = datetime.now(timezone.utc).isoformat()
    (OUT / 'BindingConservation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))
    raise SystemExit(0 if result['status'] == 'PASS' else 1)
