import hashlib
import tempfile
import unittest
from pathlib import Path
from staged_model import staged_model


class StagingTests(unittest.TestCase):
    def test_cleanup_on_success_and_failure_preserves_source(self):
        # note: Exercise exception cleanup and the original-file boundary without copying real model weights.
        with tempfile.TemporaryDirectory(dir=Path(__file__).resolve().parents[2] / 'outputs') as temp:
            root = Path(temp); source = root / 'original.gguf'; content = b'audited weights'
            source.write_bytes(content)
            model = dict(localPath=str(source), bytes=len(content), sha256=hashlib.sha256(content).hexdigest())
            for fail in (False, True):
                record = {}
                try:
                    with staged_model(model, root / 'stage', record) as staged:
                        self.assertEqual(Path(staged['localPath']).read_bytes(), content)
                        if fail: raise ValueError('simulated model failure')
                except ValueError:
                    self.assertTrue(fail)
                self.assertTrue(record['temporaryCopyDeleted'])
                self.assertEqual(source.read_bytes(), content)
            (root / 'stage' / 'foreign.gguf').write_bytes(b'preserve')
            with self.assertRaises(RuntimeError):
                with staged_model(model, root / 'stage', {}): pass
            self.assertEqual((root / 'stage' / 'foreign.gguf').read_bytes(), b'preserve')


if __name__ == '__main__': unittest.main()
