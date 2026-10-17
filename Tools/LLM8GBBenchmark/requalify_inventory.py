"""Pin local packages and archive public model instructions; never load model weights into an inference engine."""
import ast
import concurrent.futures
import hashlib
import json
import re
import struct
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmRequalification_20261008'
LOCAL = Path('E:/GPT LLM TEST/untested')
OUT.mkdir(parents=True, exist_ok=True)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


# note: Reuse only the metadata parser, avoiding any model-server initialization from the previous harness.
tree = ast.parse((ROOT / 'Tools/LLM8GBBenchmark/run.py').read_text(encoding='utf-8-sig'))
member = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'metadata')
namespace = dict(Path=Path, struct=struct, re=re)
exec(compile(ast.Module(body=[member], type_ignores=[]), 'gguf_metadata_reader', 'exec'), namespace)
sources = read(ROOT / 'outputs/Llm8GBBakeoff_20261007_170119/HuggingFaceSweep_SourceIndex.json')
for row in read(ROOT / 'outputs/LlmRequalificationPlan_20261008/FreshSourceMetadata.json')['sources']:
    sources[row['repo']] = row
previous = {row['filename']: row for row in read(ROOT / 'outputs/LlmRequalificationPlan_20261008/LocalInventory.json')}
package_hints = {
    'LFM2.5-Encoder-350M-Prompt-Router.safetensors': 'LiquidAI/LFM2.5-Encoder-350M-Prompt-Router',
    'gliner2-base-v1': 'fastino/gliner2-base-v1',
    'gliclass-small-v1.0': 'knowledgator/gliclass-small-v1.0',
    'Pleias-RAG-1B': 'PleIAs/Pleias-RAG-1B',
    'Ministral-3-3B-Instruct-2512-roleplay-tuned-ERP-tolerant.Q4_K_M.gguf': 'mradermacher/Ministral-3-3B-Instruct-2512-roleplay-tuned-ERP-tolerant-GGUF'
}
models = []
for path in sorted(LOCAL.rglob('*')):
    if not path.is_file() or path.suffix.lower() not in ('.gguf', '.safetensors'):
        continue
    size = path.stat().st_size
    row = dict(id=re.sub(r'[^A-Za-z0-9_.-]', '_', str(path.relative_to(LOCAL))), localPath=str(path), bytes=size,
               package=package_hints.get(path.parent.name) or package_hints.get(path.name), status='PACKAGE_AUDIT_PENDING')
    if path.suffix.lower() == '.gguf':
        meta = namespace['metadata'](path)
        template = meta.pop('tokenizer.chat_template', '')
        template_file = OUT / 'Templates' / (row['id'] + '.jinja')
        template_file.parent.mkdir(parents=True, exist_ok=True)
        template_file.write_text(template, encoding='utf-8')
        row.update(metadata=meta, templatePath=str(template_file), templateSha256=hashlib.sha256(template.encode()).hexdigest())
        matches = [(repo, src) for repo, src in sources.items() if any(Path(f['filename']).name == path.name and f.get('bytes') == size for f in src.get('files', []))]
        if matches:
            row['package'] = matches[0][0]
        if not row['package']:
            previous_matches = previous.get(path.name, {}).get('sourceMatchesByNameAndSize', [])
            if previous.get(path.name, {}).get('bytes') == size and previous_matches:
                row['package'] = previous_matches[0]['repo']
        if not row['package']:
            historical = previous.get(path.name, {}).get('historicalPublisherMatches', [])
            if historical:
                row['package'] = historical[0]['repository']
        row['nativeControlHints'] = {key: key in template for key in ('enable_thinking', 'reasoning_effort', '{REASON:', 'keep_past_thinking')}
    print('Hashing ' + row['id'], flush=True)
    row['sha256'] = digest(path)
    # note: A file modified during hashing is not a completed, pinned download.
    if path.stat().st_size != size:
        raise RuntimeError('Download changed during inventory: ' + str(path))
    models.append(row)
    save(OUT / 'Inventory.json', models)


def get(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'YourQuest-local-model-audit/1.0'})
    with urllib.request.urlopen(request, timeout=25) as response:
        return response.read(2 * 1024 * 1024).decode('utf-8')


def card(repo):
    result = dict(repo=repo, url='https://huggingface.co/' + repo)
    try:
        api = json.loads(get('https://huggingface.co/api/models/' + repo + '?blobs=true'))
        revision = api['sha']
        raw = get('https://huggingface.co/' + repo + '/raw/' + revision + '/README.md')
        target = OUT / 'ModelCards' / (repo.replace('/', '__') + '.md')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(raw, encoding='utf-8')
        data = api.get('cardData') or {}
        bases = data.get('base_model') or []
        if isinstance(bases, str):
            bases = [bases]
        result.update(revision=revision, cardPath=str(target), cardSha256=hashlib.sha256(raw.encode()).hexdigest(),
                      license=data.get('license'), baseModels=bases,
                      files=[dict(name=f['rfilename'], bytes=f.get('size'), sha256=(f.get('lfs') or {}).get('sha256')) for f in api.get('siblings', [])])
    except Exception as error:
        result['error'] = str(error)
    return result


repos = sorted({row['package'] for row in models if row['package']})
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    cards = list(pool.map(card, repos))
# note: Quantizer readmes often delegate prompting to the training author. Fetch one explicit upstream level too.
upstream = sorted({base for entry in cards for base in entry.get('baseModels', []) if '/' in base} - set(repos))
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    cards += list(pool.map(card, upstream))
by_repo = {entry['repo']: entry for entry in cards}
for row in models:
    source = by_repo.get(row['package'], {})
    publisher_files = source.get('files', [])
    row['publisherHashMatch'] = any(entry.get('sha256') == row['sha256'] for entry in publisher_files)
    row['cardRevision'] = source.get('revision')
    row['readmeStatus'] = 'AVAILABLE_REVIEW_PENDING' if source.get('cardPath') else 'UNRESOLVED'
save(OUT / 'Inventory.json', models)
save(OUT / 'ModelCards.json', cards)
duplicates = {}
for row in models:
    duplicates.setdefault(row['sha256'], []).append(row['localPath'])
summary = dict(utc=datetime.now(timezone.utc).isoformat(), weightFiles=len(models), uniqueWeights=len(duplicates),
               ggufFiles=sum(row['localPath'].endswith('.gguf') for row in models), totalGiB=round(sum(row['bytes'] for row in models) / 2**30, 2),
               publisherHashMatches=sum(row['publisherHashMatch'] for row in models),
               duplicateGroups=[paths for paths in duplicates.values() if len(paths) > 1],
               cardErrors=[dict(repo=entry['repo'], error=entry['error']) for entry in cards if 'error' in entry],
               unmatched=[row['id'] for row in models if not row['package']],
               status='PINNED_INVENTORY_CARDS_AWAITING_REVIEW_NO_INFERENCE')
save(OUT / 'InventorySummary.json', summary)
print(json.dumps(summary, indent=2), flush=True)
