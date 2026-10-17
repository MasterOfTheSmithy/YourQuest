"""One verified temporary weight copy; originals are never moved or deleted."""
import hashlib
import shutil
import time
from contextlib import contextmanager
from pathlib import Path


@contextmanager
def staged_model(model, stage_directory, record, target_name='current-model.gguf'):
    # note: Exclusive ownership rejects leftovers rather than deleting an unknown prior session's weights.
    folder = Path(stage_directory).resolve()
    folder.mkdir(parents=True, exist_ok=True)
    if any(folder.iterdir()):
        raise RuntimeError('Staging directory is not empty; inspect it before continuing: ' + str(folder))
    source = Path(model['localPath']).resolve(strict=True)
    if source.stat().st_size != model['bytes']:
        raise ValueError('Source size differs from the audited inventory')
    if shutil.disk_usage(folder).free < model['bytes'] + 5 * 1024**3:
        raise RuntimeError('Staging would leave less than 5 GiB on the main drive')
    # note: Native frameworks require a conventional weight filename; callers cannot escape this owned directory.
    if Path(target_name).name != target_name or target_name not in ('current-model.gguf', 'model.safetensors'):
        raise ValueError('Unsupported staging weight filename')
    target = folder / target_name
    if source == target:
        raise ValueError('Source and staging target must differ')
    owned = False
    try:
        start = time.perf_counter()
        digest = hashlib.sha256()
        with target.open('xb') as destination:
            owned = True
            with source.open('rb') as original:
                while block := original.read(8 * 1024**2):
                    destination.write(block)
                    digest.update(block)
        copy_seconds = time.perf_counter() - start
        if digest.hexdigest() != model['sha256']:
            raise ValueError('Source content differs from the audited hash')
        start = time.perf_counter()
        copied_digest = hashlib.sha256()
        with target.open('rb') as copied:
            while block := copied.read(8 * 1024**2):
                copied_digest.update(block)
        actual = copied_digest.hexdigest()
        if actual != model['sha256']:
            raise ValueError('Staged copy hash mismatch')
        record.update(originalPath=str(source), stagedPath=str(target), sha256=actual,
                      copySeconds=copy_seconds, verifySeconds=time.perf_counter() - start,
                      storageCache='Recently copied and verified file; not an OS-cold disk-load measurement')
        yield dict(model, localPath=str(target), sizeBytes=model['bytes'])
    finally:
        # note: Delete only the exact file created by this context, after the caller's owned server context exits.
        if owned:
            if target.parent.resolve() != folder or target.is_symlink():
                raise RuntimeError('Staging cleanup target changed; refusing deletion')
            target.unlink()
            record['temporaryCopyDeleted'] = not target.exists()
        record['originalStillPresent'] = source.is_file() and source.stat().st_size == model['bytes']
