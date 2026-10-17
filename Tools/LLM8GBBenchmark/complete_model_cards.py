"""Complete the bounded card audit using already hashed files; no weight reread or inference."""
import ast
import concurrent.futures
import hashlib
import json
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmRequalification_20261008'
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
inventory = read(OUT / 'Inventory.json')
previous = {row['filename']: row for row in read(ROOT / 'outputs/LlmRequalificationPlan_20261008/LocalInventory.json')}
cards = read(OUT / 'ModelCards.json')
hints = {'LFM2.5-2.6B-Q4_K_M.gguf': 'LiquidAI/LFM2.5-2.6B-GGUF',
         'LFM2.5-2.6B-Q8_0.gguf': 'LiquidAI/LFM2.5-2.6B-GGUF',
         'nsfw-6b-q2_k.gguf': 'OEvortex/HelpingAI2-6B'}
for row in inventory:
    if not row['package']:
        old = previous.get(row['id'], {})
        matches = old.get('sourceMatchesByNameAndSize', [])
        if old.get('bytes') == row['bytes'] and matches:
            row['package'] = matches[0]['repo']
        else:
            row['package'] = hints.get(row['id'])

# note: Reuse only public metadata retrieval, not the inventory module's hashing entrypoint.
tree = ast.parse((ROOT / 'Tools/LLM8GBBenchmark/requalify_inventory.py').read_text(encoding='utf-8'))
functions = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in ('get', 'card')]
namespace = dict(urllib=__import__('urllib'), json=json, OUT=OUT, hashlib=hashlib)
exec(compile(ast.Module(body=functions, type_ignores=[]), 'model_card_reader', 'exec'), namespace)
repos = {row['package'] for row in inventory if row['package']} - {row['repo'] for row in cards}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    cards += list(pool.map(namespace['card'], sorted(repos)))
upstream = {repo for row in cards for repo in row.get('baseModels', []) if '/' in repo} - {row['repo'] for row in cards}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    cards += list(pool.map(namespace['card'], sorted(upstream)))
by_repo = {row['repo']: row for row in cards}
for row in inventory:
    card = by_repo.get(row['package'], {})
    row['publisherHashMatch'] = any(item.get('sha256') == row['sha256'] for item in card.get('files', []))
    row['cardRevision'] = card.get('revision')
    row['readmeStatus'] = 'AVAILABLE_REVIEW_PENDING' if card.get('cardPath') else 'UNRESOLVED'
for name, value in [('Inventory.json', inventory), ('ModelCards.json', cards)]:
    (OUT / name).write_text(json.dumps(value, indent=2), encoding='utf-8')
print(json.dumps(dict(files=len(inventory), publisherHashMatches=sum(row['publisherHashMatch'] for row in inventory),
                     unresolved=[row['id'] for row in inventory if row['readmeStatus'] == 'UNRESOLVED'],
                     cardErrors=[dict(repo=row['repo'], error=row['error']) for row in cards if 'error' in row])))
