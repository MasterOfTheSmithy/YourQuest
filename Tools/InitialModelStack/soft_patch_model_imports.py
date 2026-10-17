"""Apply only reviewed, hash-backed wardrobe avatar metadata corrections."""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/ModelImportSoftPatch'


def main():
    paths = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    manifest = json.loads(Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
    backups = {Path(r['source']).resolve(): r for r in manifest['files']}
    inspected = {r['path']: r for r in json.loads((OUT / 'errors-before.json').read_text())}
    changes = []
    # note: Validate every preimage before replacing any setting; never infer missing required bones as optional.
    for name in paths:
        row = inspected[name]
        missing = {h['humanName'] for h in row['human'] if h['boneName'] not in row['transforms']}
        assert missing == {'LeftEye', 'RightEye'} and row['avatarSetup'] == 'CopyFromOther'
        path = (ROOT / (name + '.meta')).resolve()
        assert path.is_relative_to(ROOT / 'Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Models')
        backup = backups[path]
        data = path.read_bytes()
        before = hashlib.sha256(data).hexdigest()
        assert before.upper() == backup['sha256'].upper()
        assert hashlib.sha256(Path(backup['destination']).read_bytes()).hexdigest() == before
        text = data.decode('utf-8-sig')
        text, count = re.subn(r'(?m)^  copyAvatar: 1\r?$', '  copyAvatar: 0', text)
        assert count == 1
        # note: These models contain every other mapped humanoid bone; preserve all authored mappings and limits.
        for bone, human in [('EyeL', 'LeftEye'), ('EyeR', 'RightEye')]:
            pattern = rf'(?m)^    - boneName: {bone}\r?\n      humanName: {human}\r?\n.*?(?=^    - boneName:|^    skeleton:)'
            text, count = re.subn(pattern, '', text, flags=re.S)
            assert count == 1
        text, count = re.subn(r'  lastHumanDescriptionAvatarSource: \{.*?\}',
                              '  lastHumanDescriptionAvatarSource: {fileID: 0}', text, flags=re.S)
        assert count == 1
        assert re.search(r'^guid: (.+)$', text, re.M).group(1) == row['guid']
        changes.append((path, text.encode('utf-8'), before, backup['destination']))
    receipt = []
    for path, data, before, backup in changes:
        assert hashlib.sha256(path.read_bytes()).hexdigest() == before
        path.write_bytes(data)
        receipt.append(dict(path=str(path.relative_to(ROOT)), before=before,
                            after=hashlib.sha256(data).hexdigest(), backup=backup))
    (OUT / ('patch-' + Path(sys.argv[1]).stem + '.json')).write_text(json.dumps(receipt, indent=2))
    print(json.dumps(dict(patched=len(receipt), backup_manifest=sys.argv[2])))


if __name__ == '__main__':
    main()
