// note: These offline schemas describe fixtures, not a new Unity/save contract.
const text = { type: 'string', minLength: 1 };
const string = { type: 'string' };
const strings = { type: 'array', items: text, uniqueItems: true };
const nullableText = { type: ['string', 'null'] };
const bool = { type: 'boolean' };
const count = { type: 'integer', minimum: 0 };
const object = properties => ({ type: 'object', properties, required: Object.keys(properties), additionalProperties: false });
const array = items => ({ type: 'array', items });
const values = list => ({ type: 'string', enum: list });
const nullable = schema => ({ ...schema, type: ['object', 'null'] });
export const TASKS = ['BEHAVIOR_INTERPRETATION', 'OPPORTUNITY_PROPOSAL', 'IDENTITY_GENERATION', 'UNIQUE_OPPORTUNITY', 'OPPORTUNITY_CHAIN', 'NPC_WORLD_REACTION', 'LORE_ENRICHMENT', 'ABSTAIN_INCUBATE', 'NPC_NAME_GENERATION'];
export const RARITIES = ['none', 'personalized', 'rare', 'unique', 'legendary_chain'];
export const CATEGORIES = ['ability', 'skill', 'spell', 'technique', 'title', 'class', 'class_evolution', 'item', 'weapon', 'armor', 'artifact', 'quest', 'opportunity', 'unique_opportunity', 'legendary_chain', 'faction', 'settlement', 'npc', 'npc_epithet', 'discovery', 'world_event'];
export const SPLITS = ['train', 'validation', 'hidden_test', 'adversarial', 'regression', 'review_only', 'development'];
const fixtureId = { type: 'string', pattern: '^fixture:[a-z0-9:_-]+$' };
const name = object({
  category: values(CATEGORIES), selected_name: text,
  semantic_kernel: object({ identity: text, method: text, motif: text, consequence: text, context: text, tone: text }),
  candidate_families: strings, candidates: { type: 'array', items: text, minItems: 1, maxItems: 3, uniqueItems: true },
  parent_identity_id: nullableText, naming_lineage_id: nullableText, evolution_reason: string,
  behavioral_delta: string, mechanical_delta: string, culture_id: nullableText, naming_reason: text,
  presentation_only: bool, motifs: strings, accepted: { const: false },
  npc_parts: nullable(object({ given_name: text, family_name: nullableText, clan_name: nullableText, honorific: nullableText, pronunciation_hint: nullableText }))
});
export const outputSchema = {
  $schema: 'https://json-schema.org/draft/2020-12/schema', $id: 'urn:yourquest:offline:adaptive-output:0.2.0',
  ...object({
    decision: values(['none', 'incubate', 'propose']),
    interpretation: object({ summary: text, confidence: { type: 'number', minimum: 0, maximum: 1 }, supporting_evidence_ids: strings }),
    opportunity: nullable(object({ kind: values(['skill', 'class_evolution', 'title', 'item', 'quest', 'npc_interaction', 'relationship', 'world_event', 'identity']),
      rarity: values(RARITIES.slice(1)), identity: text, mechanical_template_id: fixtureId, destination_id: fixtureId,
      canonical_entity_ids: strings, canonical_fact_ids: strings, objective_ids: strings, mechanic_tags: strings,
      diegetic_reason: text, future_hooks: strings, offer_state: { const: 'proposal_only' } })),
    naming: nullable(name),
    response: nullable(object({ npc_entity_id: fixtureId, knowledge_fact_ids: strings, text })),
    enrichment: nullable(object({ entity_id: fixtureId, canonical_fact_ids: strings, text })),
    constraints: object({ requires_validation: strings, unsupported_requests: strings })
  })
};
export const recordSchema = {
  $schema: 'https://json-schema.org/draft/2020-12/schema', $id: 'urn:yourquest:offline:adaptive-record:0.2.0',
  ...object({
    schema_version: { const: '0.2.0' }, id: fixtureId, task: values(TASKS), scenario_family: text, split: values(SPLITS),
    source: object({ type: values(['original_hand_authored', 'original_teacher_candidate', 'sanitized_owned_trace']),
      author: text, rights_status: values(['original_pending_owner_review', 'approved_original', 'licensed_verified']),
      human_review: values(['pending', 'approved', 'rejected']), source_refs: strings, contains_private_text: { const: false }, hidden_reasoning: { const: false } }),
    player_evidence: array(object({ id: fixtureId, verb: text, attempts: count, successes: count, failures: count,
      sessions: count, distinct_contexts: count, distinct_targets: count, tool_ids: strings, recency_days: count,
      provenance: { const: 'offline_fixture_not_g15' }, note: text })),
    behavior_summary: object({ signals: strings, contradictions: strings, low_diversity_farming: bool, identity_hypothesis: string }),
    player_state: object({ accepted_identity_ids: strings, prior_choices: array(object({ offer_id: fixtureId,
      state: values(['accepted', 'declined', 'ignored']), identity_id: nullableText })), session_count: count, canonical_revision: { const: 'offline-fixture-v1' } }),
    world_context: object({ facts: array(object({ id: fixtureId, text, entity_ids: strings, consistency: values(['consistent', 'contradictory']) })),
      missing_required_facts: strings, hook_ids: strings, first_discovery_verified: bool,
      hook_records: array(object({ id: fixtureId, target_entity_id: fixtureId, required_fact_ids: strings, description: text })),
      objective_records: array(object({ id: fixtureId, template_id: fixtureId, target_entity_id: fixtureId, canonical_fact_ids: strings,
        supplied_objective: text, authority: { const: 'offline_mock_not_runtime_certified' } })),
      destinations: array(object({ id: fixtureId, template_id: fixtureId, kind: text, canonical_entity_ids: strings,
        required_fact_ids: strings, objective_ids: strings, permitted_mechanic_tags: strings,
        npc_knowledge_requirements: array(object({ npc_entity_id: fixtureId, fact_ids: strings })) })) }),
    known_entities: array(object({ id: fixtureId, kind: text, status: values(['alive', 'dead', 'available', 'unavailable']), culture_id: nullableText, knowledge_fact_ids: strings })),
    available_capabilities: array(object({ id: fixtureId, template_id: fixtureId, opportunity_kinds: strings,
      effect_kind: values(['presentation', 'skill', 'quest', 'interaction', 'relationship', 'world_event']), mechanic_tags: strings })),
    existing_content: array(object({ id: fixtureId, name: text, category: values(CATEGORIES), rarity: values(RARITIES.slice(1)), naming_lineage_id: nullableText, canonical_entity_ids: strings, semantic_identity: text, motifs: strings })),
    rarity_constraints: object({ ceiling: values(RARITIES), eligibility_validated: bool, eligible_evidence_ids: strings,
      uniqueness_status: values(['not_requested', 'reserved', 'conflict']), legendary_authorized: bool }),
    negative_constraints: strings,
    naming_context: nullable(object({ species_id: nullableText, people_id: nullableText, culture_id: nullableText, region_id: nullableText,
      social_role: nullableText, age_band: nullableText, pronoun_context: nullableText, local_naming_examples: strings,
      phonetic_tendencies: strings, common_roots: strings, honorific_rules: strings, allowed_compounds: strings, taboo_vocabulary: strings,
      applicable_name_parts: strings, forbidden_existing_names: strings, existing_identity_immutable: bool })),
    eval_labels: object({ decision: values(['none', 'incubate', 'propose']), rarity: { type: ['string', 'null'], enum: [null, ...RARITIES.slice(1)] },
      domain: values(['ordinary', 'combat', 'crafting', 'exploration', 'social', 'mixed']), coverage_ids: strings,
      safety_traps: strings, review_only_measures: strings, unsupported_name_terms: strings }),
    expected_output: outputSchema
  })
};
