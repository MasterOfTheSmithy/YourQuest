"""Reviewed package recipes for the downloaded inventory, not remote leaderboard claims."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/LlmRequalification_20261008'


def recipe(model):
    name, package = model['id'], model['package']
    result = dict(thinking='off', sampling=None, system=None, suite='general',
                  engine='E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe',
                  notes='Native embedded template; standard project-like sampling unless a published recipe overrides it.')
    cards = [BASE / 'ModelCards' / (package.replace('/', '__') + '.md')]
    def base_card(name): cards.append(BASE / 'ModelCards' / (name.replace('/', '__') + '.md'))
    if 'APUS-' in name:
        result.update(suite='decision', sampling=dict(temperature=0), notes='Candidate-label decision model; use the published OpenJev contract, never ordinary chat grading.')
        base_card('apus-ailab/APUS-OpenJev-v1-4B')
    elif 'Bonsai' in name:
        result.update(engine=str(ROOT/'outputs/Llm8GBBatch2_20261007_2025/PrismRuntime/llama-server.exe'),
                      sampling=dict(temperature=.5,top_k=20,top_p=.9,repeat_penalty=1,presence_penalty=0))
    elif name.startswith('LFM2-'):
        result['sampling'] = dict(temperature=0)
        if 'Extract' in name:
            result.update(suite='extract', system='Extract only supplied facts. Return JSON matching the requested schema; preserve unknowns as null. Do not invent facts.')
            base_card('LiquidAI/LFM2-350M-Extract' if '350M' in name else 'LiquidAI/LFM2-1.2B-Extract')
        elif 'RAG' in name:
            result.update(suite='rag'); base_card('LiquidAI/LFM2-1.2B-RAG')
        else:
            result.update(suite='tool'); base_card('LiquidAI/LFM2-1.2B-Tool')
    elif name.startswith('LFM2.5-2.6B'):
        result.update(thinking='on',sampling=dict(temperature=.1,top_k=50,top_p=1,repeat_penalty=1.1))
        base_card('LiquidAI/LFM2.5-2.6B')
    elif name.startswith('LFM-2.5'):
        result['sampling'] = dict(temperature=.1,top_k=50,top_p=1,repeat_penalty=1.05)
        base_card('Indexnusrefather/LFM-2.5-1.2b-Instruct-roleplay-tuned-v2')
        base_card('LiquidAI/LFM2.5-1.2B-Instruct')
    elif 'SmolLM' in name:
        result.update(system='/no_think',sampling=dict(temperature=.6,top_p=.95))
        base_card('HuggingFaceTB/SmolLM3-3B')
    elif 'granite' in name:
        result['sampling'] = dict(temperature=1,top_p=.95)
        base_card('ibm-granite/granite-4.2-3b')
    elif 'MiniCPM5-1B' in name:
        result['sampling'] = dict(temperature=.7,top_p=.95,min_p=0)
    elif 'MiniCPM5-2B' in name:
        result.update(thinking='on', sampling=dict(temperature=1,top_p=.95,min_p=0,repeat_penalty=1.05))
    elif 'SmallThinker' in name:
        result.update(thinking='on'); base_card('PowerInfer/SmallThinker-4BA0.6B-Instruct')
    elif 'Hivemind' in name:
        result['sampling'] = dict(temperature=.7,top_p=.9,top_k=20,repeat_penalty=1.1)
        result['notes'] = 'README repetition-penalty alternative to unsupported quadratic smoothing; native Qwen template, non-thinking condition.'
    elif 'EmperoAI' in name:
        result.update(thinking='on',sampling=dict(temperature=.6,top_p=.95,top_k=20),
                      notes='Published reasoning recipe; bounded 768-token thought budget is a latency condition, not the recommended 16K maximum reasoning run.')
    elif 'Nemotron' in name:
        result['sampling'] = dict(temperature=.6,top_p=.95)
        base_card('nvidia/NVIDIA-Nemotron-3-Nano-4B-BF16')
    elif 'nsfw-6b' in name:
        result['sampling'] = dict(temperature=.6,top_p=.9)
    elif 'Ministral' in name:
        base_card('mistralai/Ministral-3-3B-Instruct-2512')
    elif model['metadata'].get('general.architecture') == 'qwen35':
        size = '0.8B' if '0.8B' in name else '2B' if '2B' in name or '2b' in name else '4B'
        base_card('Qwen/Qwen3.5-' + size)
    if not cards[0].exists(): raise ValueError('Missing package README: '+str(cards[0]))
    result['reviewedCards'] = {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in cards if p.exists()}
    return result


def cases_for(recipe, all_cases):
    # note: Specialists keep applicable factual/grounding probes; ordinary creative generation is not their trained role.
    cases = copy.deepcopy(all_cases)
    suite = recipe['suite']
    if suite == 'extract': cases = [c for c in cases if c['track'] in ('reconstruction','summary','reasoning')]
    elif suite == 'rag': cases = [c for c in cases if c['track'] in ('reconstruction','summary','reasoning','roleplay','goddess_current_source')]
    elif suite == 'tool':
        cases = [c for c in cases if c['track'] in ('reconstruction','summary','reasoning')]
        tools = [dict(name='read_quest',description='Read the accepted quest status, without changing it.',parameters=dict(type='object',properties=dict(quest_id=dict(type='string')),required=['quest_id'])),
                 dict(name='read_npc_memory',description='Read known NPC memories, without changing them.',parameters=dict(type='object',properties=dict(npc_id=dict(type='string')),required=['npc_id']))]
        for id, query, expected in [('quest','What is the current status of quest_ledger_009?', 'read_quest(quest_id="quest_ledger_009")'),
                                    ('npc','What does npc_vey_001 remember?', 'read_npc_memory(npc_id="npc_vey_001")')]:
            cases.append(dict(id='native_tool_'+id,track='native_tool',max_tokens=160,temperature=0,schema=None,
                              messages=[dict(role='system',content='List of tools: <|tool_list_start|>'+json.dumps(tools)+'<|tool_list_end|>'),dict(role='user',content=query)],
                              expectedTool=expected,evidence='Native Pythonic tool-call selection only; calls are never executed.'))
    for case in cases:
        if recipe.get('system'): case['messages'].insert(0,dict(role='system',content=recipe['system']))
    return cases
