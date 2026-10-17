"""Bounded integration checks against exported production payloads; no state is committed."""
import copy
import argparse
import json
import time
from pathlib import Path

import psutil
import run as bench
from coexistence import ready, read, source_check, validate
from staged_model import staged_model
from schema_prompt import with_visible_schema

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmRoleIntegration_20261008'


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--adaptive', action='store_true'); args = parser.parse_args()
    bench.OUT = OUT / ('AdaptiveInference' if args.adaptive else 'Inference'); bench.OUT.mkdir(exist_ok=True)
    inventory = read(ROOT / 'outputs/LlmRequalification_20261008/Inventory.json')
    routes = read(OUT / 'Routes.json')
    production = read(OUT / 'ProductionCases.json')
    fixtures = read(ROOT / 'outputs/LlmRequalification_20261008/FullSweep_20261008_172243/Fixtures.json')
    tracks = dict(Equipment=['equipment'], Consumable=['consumable'], SkillSpell=['skill_spell'],
                  TitleClass=['title_class'], Lore=['world_player_lore'], NpcPopulation=['npc_identity'],
                  Summarization=['summary'], Dialogue=['roleplay'], GoddessCommentary=['goddess_current_source'])
    selected = {}
    for route in routes:
        if route['role'] not in tracks and route['role'] != 'QuestGeneration': continue
        if args.adaptive and route['role'] == 'GoddessCommentary': continue  # Failed semantic role remains gated; do not repeat it.
        selected.setdefault(route['model'], []).append(route)
    rows, stages, replies = [], [], []
    for ordinal, (path, roles) in enumerate(selected.items()):
        ready()
        if any((p.info['name'] or '').lower() == 'llama-server.exe' for p in psutil.process_iter(['name'])):
            raise RuntimeError('A server is already running; refusing competing residency')
        model = next(m for m in inventory if m['localPath'] == path)
        stage = dict(model=model['id']); stages.append(stage)
        route = roles[0]; profile = route['profile']
        probes = []
        for item in production:
            if item['model'] != path: continue
            wire = item['payload']
            probes.append(dict(id=item['id'], track='production_progression', messages=wire['messages'],
                max_tokens=wire['max_tokens'], temperature=wire['temperature'], schema=wire.get('json_schema'),
                response_format=wire.get('response_format'), production=True,
                evidence=item['evidence']))
        if args.adaptive and any(role['role'] == 'NpcPopulation' for role in roles):
            npc = read(OUT / 'NpcCase.json'); wire = npc['payload']
            probes.append(dict(id='production_npc_batch', track='production_npc', messages=wire['messages'],
                max_tokens=wire['max_tokens'], temperature=wire['temperature'], schema=None,
                response_format=wire.get('response_format'), production=True, evidence='Current production NPC batch prompt and detached batch parser, no publication.'))
        for role in roles:
            for case in fixtures:
                if case['track'] in tracks.get(role['role'], []):
                    probe = copy.deepcopy(case)
                    if role['profile'].get('systemPrompt'):
                        probe['messages'].insert(0, dict(role='system', content=role['profile']['systemPrompt']))
                    probes.append(probe)
        # note: The control also covers composite source contracts; creative category winners do not certify those transactions.
        if route['role'] == 'QuestGeneration' and not args.adaptive:
            probes += [copy.deepcopy(c) for c in fixtures if c['track'].startswith('structured_')]
        sampling = dict(temperature=profile['temperature'], top_p=profile['topP'], top_k=profile['topK'],
                        presence_penalty=profile['presencePenalty'], repeat_penalty=profile['repeatPenalty'])
        if profile['minP'] >= 0: sampling['min_p'] = profile['minP']
        if args.adaptive and route['role'] in ('NpcPopulation', 'Equipment', 'SkillSpell'):
            # note: Recheck the exact sampling recipe that won the sweep; lower-temperature generic defaults changed NPC grounding.
            sampling.update(temperature=.7, top_p=.8, top_k=20, presence_penalty=1.5, repeat_penalty=1, min_p=0)
        try:
            with staged_model(model, OUT / 'SSDStage', stage) as staged:
                layers = route['layers'] if route['layers'] >= 0 else None
                if args.adaptive:
                    # note: Mirror the exported production estimate; compare actual free VRAM again after loading.
                    free = bench.gpu()['freeMiB']; blocks = profile['modelBlockCount']
                    layer_mb = max(1, model['bytes'] / ((blocks + 1) * 1024**2)) + 16
                    budget = max(0, int((free - 2048 - 576) // layer_mb))
                    layers = min(blocks, budget, route['layers'] if route['layers'] >= 0 else blocks)
                    stage.update(freeBeforeLoadMiB=free, budgetedLayers=layers)
                with bench.Server(staged, Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                    context=route['context'], headroom=2048, cache=True, cache_ram_mib=256, checkpoints=2,
                    checkpoint_min_step=256, thinking='on' if profile['reasoningMode'] else 'off',
                    reasoning_budget=profile['reasoningBudget'], kv_type='q8_0',
                    gpu_layers=layers, cpu=layers == 0, label=('adaptive' if args.adaptive else 'role') + str(ordinal)) as server:
                    if args.adaptive and bench.gpu()['freeMiB'] < 2048: raise RuntimeError('Measured reserve below 2048 MiB; unload without inference')
                    for case in probes:
                        # note: Production fixtures retain their exact exported messages; synthetic probes expose their synthetic schema explicitly.
                        before = ready(); original = copy.deepcopy(case) if case.get('production') else with_visible_schema(case)
                        for attempt in range(3):
                            probe = copy.deepcopy(original); probe['id'] += '_try' + str(attempt)
                            if attempt:
                                probe['messages'].append(dict(role='user', content='Correct the previous response. Validation: ' + diagnostic + '\nPrevious response:\n' + row.get('raw', '')))
                            row = bench.call(server, probe, sample=0, native_sampling=False,
                                output_extra=0 if case.get('production') else max(0, profile['reasoningBudget']), sampling_overrides=sampling)
                            check = source_check(case, row.get('raw', ''), 'role_integration_' + str(ordinal) + '_' + case['id'] + '_' + str(attempt)) if row.get('success') else None
                            accepted, diagnostic = validate(case, row, check)
                            rows.append(dict(model=model['id'], id=case['id'], attempt=attempt, measurement=row,
                                acceptedByDetachedChecks=accepted, sourceCheck=check, unity=ready(before['renderedFrames'])))
                            bench.save(OUT / ('AdaptiveCases.json' if args.adaptive else 'Cases.json'), rows)
                            if accepted or case.get('production'): break
                        if case.get('production'):
                            if case['id'] == 'production_npc_batch':
                                bench.save(OUT / 'NpcReply.json', dict(raw=row.get('raw', '')))
                                continue
                            replies.append(dict(id=case['id'], model=model['id'], raw=row.get('raw', '')))
                            bench.save(OUT / 'ProductionReplies.json', replies)
            stage['status'] = 'COMPLETE'
        except Exception as error:
            stage.update(status='BLOCKED', error=str(error))
        bench.save(OUT / ('AdaptiveStaging.json' if args.adaptive else 'Staging.json'), stages)
    bench.save(OUT / ('AdaptiveComplete.json' if args.adaptive else 'InferenceComplete.json'), dict(utc=bench.utc(), stages=stages, calls=len(rows),
        evidence='Detached inference beside rendered Unity test world; actual progression prompts and evaluator reviewed separately.'))


if __name__ == '__main__': main()
