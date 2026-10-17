"""Extra detached role probes; production schemas are tested by the linked harness separately."""
import json
from fixtures import object_schema


def build_role_cases():
    # note: Shared facts make fabrication and protected identity drift reviewable across creative categories.
    context = dict(playerId='mira_001', playerName='Mira', level=8, location='Alderfen',
                   vow='Protect the river crossings', gold=0, knownFacts=['Mira carried a miner to the chapel.',
                   'Vey heard about the rescue from a courier.', 'The ferry ledger is missing.'],
                   unknownFacts=['Whether the miner recovered', 'Whether the north crossing is safe'])
    string = {'type': 'string', 'minLength': 1}
    roles = [
        ('equipment', 'Create one practical equipped defensive item suited to Mira. Give concrete material, shape and use; invent no owned resources or reward.',
         dict(name=string, slot={'type':'string','enum':['head','body','hands','feet','offhand']}, description=string, material=string)),
        ('consumable', 'Create one mundane consumable for river travel. Describe a concrete container, contents and limited use; no resurrection or guaranteed cures.',
         dict(name=string, container=string, contents=string, use=string)),
        ('skill_spell', 'Create one defensive ability concept with clear targeting, cost and cooldown. No arbitrary executable code or guaranteed invulnerability.',
         dict(name=string, kind={'type':'string','enum':['skill','spell']}, target={'type':'string','enum':['self','ally']},
              resourceCost={'type':'integer','minimum':1,'maximum':30}, cooldownSeconds={'type':'integer','minimum':1,'maximum':60}, description=string)),
        ('title_class', 'Propose a title and class reflecting the recorded rescue and vow. Neither confers political authority, known ranks, or completed quests.',
         dict(title=string, className=string, rationale=string)),
        ('world_player_lore', 'Propose original local folklore about the river, explicitly as a tale, plus a factual short player biography. Preserve all unknowns; no invented past deeds.',
         dict(localFolklore=string, playerBiography=string, unresolvedQuestion=string)),
        ('npc_identity', 'Create a new ferrymender NPC for this world, with a practical motive and distinctive voice. The NPC has not met Mira and knows no private events.',
         dict(name=string, occupation=string, appearance=string, motive=string, voiceExample=string)),
        ('quest_proposal', 'Propose a new offered quest to locate the missing ferry ledger. No completion or rewards have occurred. All objectives must be explicit records.',
         dict(name=string, status={'type':'string','enum':['offered']}, description=string,
              objectives={'type':'array','minItems':1,'maxItems':3,'items':object_schema(dict(
                  kind={'type':'string','enum':['talk','collect','visit']}, targetId=string, count={'type':'integer','minimum':1,'maximum':3}))})),
    ]
    result = []
    for role, task, properties in roles:
        schema = object_schema(dict(playerId={'type':'string','enum':['mira_001']}, **properties))
        result.append(dict(id='role_' + role, track=role, schema=schema, max_tokens=600, temperature=.55,
                           messages=[dict(role='user', content=task+'\nCreate a proposal only. Return JSON matching the supplied schema.\nAUTHORITATIVE_CONTEXT\n'+json.dumps(context))],
                           evidence='Synthetic role screening, not the production DTO or mechanical acceptance. Requires independent prose/context review.'))
    return result
