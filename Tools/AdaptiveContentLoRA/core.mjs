import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { recordSchema, outputSchema, TASKS, RARITIES } from './schema.mjs';

export const VERSION = '0.2.0';
export const canonical = value => JSON.stringify(value, (_key, item) => item && typeof item === 'object' && !Array.isArray(item)
  ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0)) : item);
export const hash = value => createHash('sha256').update(typeof value === 'string' ? value : canonical(value)).digest('hex');
export const normalizeName = value => value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^\p{L}\p{N} ]/gu, ' ').replace(/\s+/g, ' ').trim();
const tokens = value => new Set(normalizeName(value).split(' ').filter(Boolean));
const jaccard = (a, b) => { const x = tokens(a), y = tokens(b); return [...x].filter(t => y.has(t)).length / Math.max(1, new Set([...x, ...y]).size); };
const grammars=JSON.parse(readFileSync(new URL('./naming-grammars.json',import.meta.url),'utf8'));
const budgets = grammars.soft_word_budgets;

// note: Soundex is an advisory English-name approximation, never a cross-culture semantic duplicate authority.
export function phoneticKey(value) {
  const normalized=normalizeName(value); if(!/^[a-z ]+$/.test(normalized))return null;
  const codes={b:1,f:1,p:1,v:1,c:2,g:2,j:2,k:2,q:2,s:2,x:2,z:2,d:3,t:3,l:4,m:5,n:5,r:6};
  return normalized.split(' ').map(word=>{
    let out=word[0],previous=codes[word[0]]??0;
    for(const char of word.slice(1)){const code=codes[char]??0;if(code&&code!==previous)out+=code;previous=code;}
    return(out+'000').slice(0,4);
  }).join('-');
}

// note: The dependency-free schema interpreter implements only the keywords used above; unknown keywords fail closed.
export function schemaErrors(value, schema, path = '$') {
  const errors = [];
  const supported = new Set(['$schema', '$id', 'type', 'properties', 'required', 'additionalProperties', 'items', 'uniqueItems', 'minItems', 'maxItems', 'minLength', 'pattern', 'minimum', 'maximum', 'enum', 'const']);
  for (const key of Object.keys(schema)) if (!supported.has(key)) errors.push(`${path}: unsupported schema keyword ${key}`);
  if ('const' in schema && canonical(value) !== canonical(schema.const)) errors.push(`${path}: const mismatch`);
  if (schema.enum && !schema.enum.some(x => canonical(x) === canonical(value))) errors.push(`${path}: enum mismatch`);
  const actual = value === null ? 'null' : Array.isArray(value) ? 'array' : typeof value;
  const types = schema.type ? (Array.isArray(schema.type) ? schema.type : [schema.type]) : [];
  if (types.length && !types.some(t => t === actual || t === 'integer' && actual === 'number' && Number.isInteger(value))) return [...errors, `${path}: type mismatch`];
  if (value === null) return errors;
  if (actual === 'object') {
    for (const key of schema.required ?? []) if (!Object.hasOwn(value, key)) errors.push(`${path}.${key}: required`);
    for (const [key, child] of Object.entries(value)) {
      if (schema.properties && Object.hasOwn(schema.properties, key)) errors.push(...schemaErrors(child, schema.properties[key], `${path}.${key}`));
      else if (schema.additionalProperties === false) errors.push(`${path}.${key}: additional property`);
    }
  }
  if (actual === 'array') {
    if (schema.minItems !== undefined && value.length < schema.minItems) errors.push(`${path}: too few items`);
    if (schema.maxItems !== undefined && value.length > schema.maxItems) errors.push(`${path}: too many items`);
    if (schema.uniqueItems && new Set(value.map(canonical)).size !== value.length) errors.push(`${path}: duplicate items`);
    if (schema.items) value.forEach((child, i) => errors.push(...schemaErrors(child, schema.items, `${path}[${i}]`)));
  }
  if (actual === 'string') {
    if (schema.minLength !== undefined && value.length < schema.minLength) errors.push(`${path}: too short`);
    if (schema.pattern && !new RegExp(schema.pattern).test(value)) errors.push(`${path}: pattern mismatch`);
  }
  if (actual === 'number') {
    if (!Number.isFinite(value)) errors.push(`${path}: nonfinite number`);
    if (schema.minimum !== undefined && value < schema.minimum) errors.push(`${path}: below minimum`);
    if (schema.maximum !== undefined && value > schema.maximum) errors.push(`${path}: above maximum`);
  }
  return errors;
}

// note: These are explicit input-fact gates, never heuristic eligibility derived from generated names.
export function validateOutput(record, output) {
  const schema = schemaErrors(output, outputSchema);
  if (schema.length) return { schema_valid: false, errors: schema, warnings: [] };
  const errors = [], warnings = [];
  const evidence = new Set(record.player_evidence.map(x => x.id));
  const entities = new Map(record.known_entities.map(x => [x.id, x]));
  const content = new Map(record.existing_content.map(x => [x.id, x]));
  const facts = new Map(record.world_context.facts.map(x => [x.id, x]));
  const refs = (items, allowed, kind) => { for (const id of items) if (!allowed.has(id)) errors.push(`unknown_${kind}:${id}`); };
  refs(output.interpretation.supporting_evidence_ids, evidence, 'evidence');
  if (output.decision !== 'propose' && output.opportunity !== null) errors.push('opportunity_without_proposal');
  if (output.decision === 'propose' && output.opportunity === null) errors.push('proposal_without_destination');
  if (output.opportunity && !output.naming) errors.push('proposal_missing_name_record');
  if (record.task === 'BEHAVIOR_INTERPRETATION' && output.opportunity !== null) errors.push('interpretation_task_reward');
  if (record.task === 'NPC_NAME_GENERATION' && (output.opportunity || output.response || output.enrichment)) errors.push('npc_name_task_scope');
  if (output.decision === 'propose') {
    const opportunity = output.opportunity;
    if (opportunity) {
      const capability = record.available_capabilities.find(x => x.template_id === opportunity.mechanical_template_id);
      if (!capability || !capability.opportunity_kinds.includes(opportunity.kind)) errors.push('unsupported_mechanic');
      // note: Supplied destinations bind facts/objectives to the fixed capability; IDs alone cannot invent a repair or follow-up job.
      const destination = record.world_context.destinations.find(x => x.id === opportunity.destination_id);
      if (!destination) errors.push('unknown_destination');
      else {
        if (destination.template_id !== opportunity.mechanical_template_id || destination.kind !== opportunity.kind) errors.push('destination_contract_mismatch');
        for (const field of ['canonical_entity_ids','objective_ids','mechanic_tags']) {
          const permitted = destination[field === 'mechanic_tags' ? 'permitted_mechanic_tags' : field];
          if (canonical([...opportunity[field]].sort()) !== canonical([...permitted].sort())) errors.push(`destination_binding_mismatch:${field}`);
        }
        for (const required of destination.required_fact_ids) if (!opportunity.canonical_fact_ids.includes(required)) errors.push(`missing_destination_fact:${required}`);
        for (const requirement of destination.npc_knowledge_requirements) {
          const npc = entities.get(requirement.npc_entity_id);
          if (!npc || npc.kind !== 'npc' || npc.status !== 'alive') errors.push('invalid_destination_npc');
          refs(requirement.fact_ids, new Set(npc?.knowledge_fact_ids ?? []), 'destination_npc_knowledge');
        }
      }
      refs(opportunity.canonical_fact_ids, facts, 'fact');
      if (opportunity.canonical_fact_ids.some(id => facts.get(id)?.consistency !== 'consistent')) errors.push('contradictory_destination_fact');
      refs(opportunity.objective_ids, new Set(record.world_context.objective_records.map(x => x.id)), 'objective');
      refs(opportunity.mechanic_tags, new Set(capability?.mechanic_tags ?? []), 'mechanic_tag');
      if (opportunity.kind === 'quest' && !opportunity.objective_ids.length) errors.push('quest_without_supplied_objective');
      if (!record.rarity_constraints.eligibility_validated) errors.push('eligibility_not_validated');
      if (record.behavior_summary.low_diversity_farming) errors.push('farming_reward');
      if (record.behavior_summary.contradictions.length || record.world_context.missing_required_facts.length || record.world_context.facts.some(x => x.consistency !== 'consistent')) errors.push('unresolved_required_facts');
      if (RARITIES.indexOf(opportunity.rarity) > RARITIES.indexOf(record.rarity_constraints.ceiling)) errors.push('rarity_ceiling');
      if (['unique', 'legendary_chain'].includes(opportunity.rarity) && record.rarity_constraints.uniqueness_status !== 'reserved') errors.push('uniqueness_not_reserved');
      if (opportunity.rarity === 'legendary_chain' && !record.rarity_constraints.legendary_authorized) errors.push('legendary_not_authorized');
      if (record.player_state.prior_choices.some(x => x.state === 'declined') && record.task === 'OPPORTUNITY_CHAIN') errors.push('declined_chain');
      if (!output.interpretation.supporting_evidence_ids.length) errors.push('ungrounded_proposal');
      refs(output.interpretation.supporting_evidence_ids, new Set(record.rarity_constraints.eligible_evidence_ids), 'eligible_evidence');
      refs(opportunity.canonical_entity_ids, entities, 'entity');
      for (const id of opportunity.canonical_entity_ids) if (['dead', 'unavailable'].includes(entities.get(id)?.status)) errors.push(`unavailable_destination:${id}`);
      refs(opportunity.future_hooks, new Set(record.world_context.hook_ids), 'hook');
      for (const hookId of opportunity.future_hooks) {
        const hook = record.world_context.hook_records.find(x => x.id === hookId);
        if (!hook) errors.push('hook_without_supplied_context');
        else {
          if (['dead','unavailable'].includes(entities.get(hook.target_entity_id)?.status)) errors.push('unavailable_hook_destination');
          for (const factId of hook.required_fact_ids) if (!opportunity.canonical_fact_ids.includes(factId)) errors.push(`missing_hook_fact:${factId}`);
        }
      }
    }
  }
  if (output.response) {
    const npc = entities.get(output.response.npc_entity_id);
    if (!npc || npc.kind !== 'npc' || npc.status !== 'alive') errors.push('invalid_reacting_npc');
    refs(output.response.knowledge_fact_ids, new Set(npc?.knowledge_fact_ids ?? []), 'npc_knowledge');
    refs(output.response.knowledge_fact_ids, facts, 'fact');
    if (!output.response.knowledge_fact_ids.length) errors.push('response_without_supplied_knowledge');
    if (output.response.knowledge_fact_ids.some(id=>facts.get(id)?.consistency!=='consistent')) errors.push('contradictory_response_fact');
  }
  if (output.enrichment) {
    if (!entities.has(output.enrichment.entity_id) && !content.has(output.enrichment.entity_id)) errors.push('unknown_enriched_entity');
    refs(output.enrichment.canonical_fact_ids, facts, 'fact');
    if (output.enrichment.canonical_fact_ids.some(id => facts.get(id)?.consistency !== 'consistent')) errors.push('contradictory_enrichment');
  }
  if (output.naming) {
    const n = output.naming;
    if (output.opportunity && n.selected_name !== output.opportunity.identity) errors.push('name_identity_mismatch');
    if (!n.candidates.includes(n.selected_name)) errors.push('selected_name_missing_candidate');
    if (output.opportunity && ['rare','unique','legendary_chain'].includes(output.opportunity.rarity) && (n.candidates.length < 2 || n.candidate_families.length < 2)) errors.push('important_name_missing_candidates');
    if (n.parent_identity_id && !record.player_state.accepted_identity_ids.includes(n.parent_identity_id)) errors.push('unknown_parent_identity');
    if (n.parent_identity_id && (!n.evolution_reason || !n.behavioral_delta || !n.mechanical_delta)) errors.push('missing_evolution_delta');
    const parent = content.get(n.parent_identity_id);
    if (parent?.naming_lineage_id && n.naming_lineage_id !== parent.naming_lineage_id) errors.push('lineage_retcon');
    if (parent && output.opportunity && RARITIES.indexOf(output.opportunity.rarity) < RARITIES.indexOf(parent.rarity)) errors.push('ancestry_rarity_downgrade');
    if (n.naming_lineage_id && !content.has(n.naming_lineage_id)) errors.push('unknown_naming_lineage');
    if (n.culture_id && !entities.has(n.culture_id)) errors.push('invented_culture');
    if(record.task==='NPC_NAME_GENERATION') {
      const context=record.naming_context;
      if(!context||!n.npc_parts) errors.push('missing_npc_naming_context_or_parts');
      else {
        if(n.culture_id!==context.culture_id)errors.push('npc_culture_mismatch');
        if(context.existing_identity_immutable)errors.push('immutable_npc_rename');
        if(context.forbidden_existing_names.some(x=>normalizeName(x)===normalizeName(n.selected_name)))errors.push('forbidden_npc_name');
        for(const part of ['family_name','clan_name','honorific'])if(n.npc_parts[part]&&!context.applicable_name_parts.includes(part))errors.push(`inapplicable_npc_name_part:${part}`);
        const assembled=[n.npc_parts.honorific,n.npc_parts.given_name,n.npc_parts.family_name,n.npc_parts.clan_name].filter(Boolean).join(' ');
        if(assembled!==n.selected_name)errors.push('npc_parts_name_mismatch');
      }
    }
    const cap = record.available_capabilities.find(x => x.template_id === output.opportunity?.mechanical_template_id);
    if (n.presentation_only && cap && cap.effect_kind !== 'presentation') errors.push('presentation_effect_mismatch');
    for (const prior of record.existing_content) {
      const sameLineage = n.naming_lineage_id && n.naming_lineage_id === prior.naming_lineage_id;
      if (normalizeName(n.selected_name) === normalizeName(prior.name) && !(sameLineage && prior.id === n.parent_identity_id)) errors.push(`name_collision:${prior.id}`);
      else if (jaccard(n.selected_name, prior.name) >= 0.75 && !sameLineage) warnings.push(`token_name_similarity:${prior.id}`);
      else if (!sameLineage && phoneticKey(n.selected_name) && phoneticKey(n.selected_name) === phoneticKey(prior.name)) warnings.push(`phonetic_name_similarity_review:${prior.id}`);
      if(!sameLineage && prior.semantic_identity && jaccard(n.semantic_kernel.identity,prior.semantic_identity)>=0.75) warnings.push(`semantic_identity_overlap_review:${prior.id}`);
    }
    const wordCount = n.selected_name.trim().split(/\s+/).length;
    if (budgets[n.category] && (wordCount < budgets[n.category][0] || wordCount > budgets[n.category][1])) warnings.push('soft_word_budget');
    if ([...tokens(n.selected_name)].some(token=>grammars.tracked_generic_tokens.includes(token))) warnings.push('generic_grandeur_review');
    for (const term of record.eval_labels.unsupported_name_terms) if (n.candidates.some(candidate => normalizeName(candidate).includes(normalizeName(term)))) errors.push(`unsupported_name_implication:${term}`);
  }
  const prose = [output.interpretation.summary, output.opportunity?.identity, output.opportunity?.diegetic_reason, output.response?.text, output.enrichment?.text, output.naming?.naming_reason].filter(Boolean).join('\n');
  if (/hidden prerequisite\s*\d|\b\d+\s*\/\s*\d+\s*(complete|requirements)|\bthreshold\s*[:=]\s*\d/i.test(prose)) errors.push('hidden_checklist_exposure');
  if (/\bAssets[\\/]|\b[A-Z]:[\\/]|\.prefab\b|\bpublic\s+(?:class|void)\b|\busing\s+UnityEngine\b/i.test(prose)) errors.push('executable_or_asset_path');
  if (/\b(?:you have|you've|player has)\s+(?:received|unlocked|been granted)\b/i.test(prose)) errors.push('grant_before_commit');
  if (record.world_context.missing_required_facts.length && output.constraints.requires_validation.length === 0) errors.push('missing_fact_not_deferred');
  if (record.world_context.facts.some(x => x.consistency !== 'consistent') && output.constraints.requires_validation.length === 0) errors.push('canon_conflict_not_deferred');
  return { schema_valid: true, errors, warnings };
}

export function validateRecord(record) {
  const errors = schemaErrors(record, recordSchema);
  if (errors.length) return errors;
  const unique = (rows, label) => { if (new Set(rows.map(x => x.id)).size !== rows.length) errors.push(`duplicate_${label}_id`); };
  unique(record.player_evidence, 'evidence'); unique(record.known_entities, 'entity'); unique(record.existing_content, 'content'); unique(record.world_context.facts, 'fact');
  for (const e of record.player_evidence) if (e.successes + e.failures > e.attempts) errors.push(`outcomes_exceed_attempts:${e.id}`);
  for (const e of record.player_evidence) if (e.sessions > record.player_state.session_count) errors.push(`evidence_sessions_exceed_history:${e.id}`);
  const evidence=new Set(record.player_evidence.map(x=>x.id)),entities=new Set(record.known_entities.map(x=>x.id)),content=new Set(record.existing_content.map(x=>x.id));
  const facts = new Set(record.world_context.facts.map(x=>x.id)), objectives = new Map(record.world_context.objective_records.map(x=>[x.id,x]));
  const refs = (items, allowed, label) => { for (const ref of items) if (!allowed.has(ref)) errors.push(`invalid_${label}_input:${ref}`); };
  unique(record.world_context.destinations,'destination'); unique(record.world_context.objective_records,'objective'); unique(record.world_context.hook_records,'hook');
  for (const fact of record.world_context.facts) refs(fact.entity_ids, new Set([...entities,...content]), 'fact_entity');
  for (const entity of record.known_entities) refs(entity.knowledge_fact_ids, facts, 'entity_knowledge');
  refs(record.world_context.hook_ids,new Set(record.world_context.hook_records.map(x=>x.id)),'hook');
  for (const hook of record.world_context.hook_records) { refs([hook.target_entity_id],entities,'hook_entity'); refs(hook.required_fact_ids,facts,'hook_fact'); }
  for (const objective of objectives.values()) { refs([objective.target_entity_id],entities,'objective_entity'); refs(objective.canonical_fact_ids,facts,'objective_fact'); }
  for (const destination of record.world_context.destinations) {
    const cap = record.available_capabilities.find(x=>x.template_id===destination.template_id);
    if (!cap || !cap.opportunity_kinds.includes(destination.kind)) errors.push('invalid_destination_capability_input');
    refs(destination.canonical_entity_ids,entities,'destination_entity'); refs(destination.required_fact_ids,facts,'destination_fact');
    refs(destination.objective_ids,objectives,'destination_objective'); refs(destination.permitted_mechanic_tags,new Set(cap?.mechanic_tags??[]),'destination_mechanic');
    for (const objectiveId of destination.objective_ids) { const objective = objectives.get(objectiveId);
      if (objective && (objective.template_id !== destination.template_id || !destination.canonical_entity_ids.includes(objective.target_entity_id) || objective.canonical_fact_ids.some(x=>!destination.required_fact_ids.includes(x)))) errors.push('objective_destination_contract_mismatch'); }
    for (const knowledge of destination.npc_knowledge_requirements) {
      refs([knowledge.npc_entity_id],entities,'destination_npc'); refs(knowledge.fact_ids,facts,'destination_knowledge');
      if (knowledge.fact_ids.some(x=>!destination.required_fact_ids.includes(x))) errors.push('destination_knowledge_fact_not_bound');
    }
  }
  for(const ref of record.rarity_constraints.eligible_evidence_ids)if(!evidence.has(ref))errors.push(`invalid_eligible_evidence_input:${ref}`);
  for(const ref of record.player_state.accepted_identity_ids)if(!content.has(ref))errors.push(`invalid_accepted_identity_input:${ref}`);
  if(record.naming_context)for(const key of ['species_id','people_id','culture_id','region_id'])if(record.naming_context[key]&&!entities.has(record.naming_context[key]))errors.push(`invalid_naming_context_input:${key}`);
  if (record.expected_output.decision !== record.eval_labels.decision || (record.expected_output.opportunity?.rarity ?? null) !== record.eval_labels.rarity) errors.push('expected_label_mismatch');
  errors.push(...validateOutput(record, record.expected_output).errors);
  return errors;
}

export function namingStatistics(records) {
  const named=records.filter(r=>r.expected_output.naming),names=named.map(r=>r.expected_output.naming),motifs={},generic={},keys=new Map(),duplicates=[];
  for(const r of named){const n=r.expected_output.naming,key=normalizeName(n.selected_name);if(keys.has(key))duplicates.push([keys.get(key),r.id]);else keys.set(key,r.id);
    for(const motif of n.motifs)motifs[motif]=(motifs[motif]??0)+1;
    for(const token of tokens(n.selected_name))if(grammars.tracked_generic_tokens.includes(token))generic[token]=(generic[token]??0)+1;
  }
  return{named_records:names.length,normalized_name_collisions:duplicates,motif_counts:motifs,generic_token_counts:generic,
    important_name_candidate_counts:named.filter(r=>['rare','unique','legendary_chain'].includes(r.eval_labels.rarity)).map(r=>({id:r.id,count:r.expected_output.naming.candidates.length})),
    memorability:null,semantic_distinctiveness:null,pronounceability:null,policy:'Lexical counts only; fixture names are not model evaluation and saturation quality needs human review.'};
}

const common = ['task', 'negative_constraints'];
export const PROJECTIONS = {
  BEHAVIOR_INTERPRETATION: [...common, 'player_evidence', 'behavior_summary', 'player_state'],
  ABSTAIN_INCUBATE: [...common, 'player_evidence', 'behavior_summary', 'player_state', 'world_context', 'available_capabilities', 'rarity_constraints'],
  OPPORTUNITY_PROPOSAL: [...common, 'player_evidence', 'behavior_summary', 'player_state', 'world_context', 'known_entities', 'available_capabilities', 'existing_content', 'rarity_constraints'],
  IDENTITY_GENERATION: [...common, 'player_evidence', 'behavior_summary', 'player_state', 'world_context', 'known_entities', 'available_capabilities', 'existing_content', 'rarity_constraints', 'naming_context'],
  UNIQUE_OPPORTUNITY: [...common, 'player_evidence', 'behavior_summary', 'player_state', 'world_context', 'known_entities', 'available_capabilities', 'existing_content', 'rarity_constraints'],
  OPPORTUNITY_CHAIN: [...common, 'player_evidence', 'behavior_summary', 'player_state', 'world_context', 'known_entities', 'available_capabilities', 'existing_content', 'rarity_constraints'],
  NPC_WORLD_REACTION: [...common, 'player_state', 'world_context', 'known_entities', 'existing_content'],
  LORE_ENRICHMENT: [...common, 'world_context', 'known_entities', 'existing_content'],
  NPC_NAME_GENERATION: [...common, 'naming_context', 'world_context', 'known_entities', 'existing_content']
};
export function project(record) {
  const input = Object.fromEntries(PROJECTIONS[record.task].map(key => [key, record[key]]));
  if (record.task === 'BEHAVIOR_INTERPRETATION') input.output_rule = 'Interpret only; no reward proposal.';
  // note: A reaction projection excludes facts not authorized for a supplied NPC, even if world canon knows them.
  if (record.task === 'NPC_WORLD_REACTION') {
    const allowed = new Set(record.known_entities.filter(x => x.kind === 'npc').flatMap(x => x.knowledge_fact_ids));
    const visibleFacts=record.world_context.facts.filter(x => allowed.has(x.id)),visibleIdentities=new Set(visibleFacts.flatMap(x=>x.entity_ids));
    input.world_context = { facts:visibleFacts, missing_required_facts:record.world_context.missing_required_facts };
    input.existing_content=record.existing_content.filter(x=>visibleIdentities.has(x.id));
    input.player_state={accepted_identity_ids:record.player_state.accepted_identity_ids.filter(x=>visibleIdentities.has(x))};
  }
  return input;
}
export const trainingEligible = record => record.split === 'train' && record.source.human_review === 'approved' && ['approved_original', 'licensed_verified'].includes(record.source.rights_status) && validateRecord(record).length === 0;

// note: Candidate filtering reports cross-family similarities; it does not erase legitimate counterfactual pairs.
export function deduplicate(records) {
  const fingerprints = new Map(), duplicates = [], near = [];
  records.forEach(r => {
    const fingerprint = hash(project(r));
    if (fingerprints.has(fingerprint)) duplicates.push([fingerprints.get(fingerprint), r.id]);
    else fingerprints.set(fingerprint, r.id);
  });
  for (let i = 0; i < records.length; i++) for (let j = i + 1; j < records.length; j++) {
    const a = records[i], b = records[j];
    const left = a.player_evidence.map(x => `${x.verb} ${x.note}`).join(' '), right = b.player_evidence.map(x => `${x.verb} ${x.note}`).join(' ');
    if (left && right && jaccard(left, right) >= 0.8) near.push({ ids: [a.id, b.id], same_family: a.scenario_family === b.scenario_family, cross_split: a.split !== b.split, action: 'manual_review_before_training' });
  }
  return { exact_input_duplicates: duplicates, lexical_near_duplicates: near, semantic_deduplication: 'REVIEW_REQUIRED_NO_EMBEDDING_MODEL' };
}

export function verifySplits(records, seal) {
  const errors = [], seen = new Map();
  for (const r of records) {
    if (seal.families[r.scenario_family] !== r.split) errors.push(`unsealed_or_wrong_family:${r.id}`);
    if (seen.has(r.scenario_family) && seen.get(r.scenario_family) !== r.split) errors.push(`family_leakage:${r.scenario_family}`);
    seen.set(r.scenario_family, r.split);
    if (seal.reserved_hidden_families.includes(r.scenario_family)) errors.push(`reserved_hidden_family_in_public_catalog:${r.id}`);
  }
  return errors;
}

export function filterCandidates(candidates, existingTrain, seal) {
  const quarantine=[],filtered=[],seen=new Set(existingTrain.map(r=>hash(project(r))));
  for(const r of candidates){const errors=validateRecord(r);
    if(!errors.length){errors.push(...verifySplits([r],seal));if(r.split!=='train')errors.push('synthetic_expansion_train_only');}
    if(!errors.length){const fingerprint=hash(project(r));if(seen.has(fingerprint))errors.push('duplicate_model_input');else seen.add(fingerprint);}
    if(errors.length)quarantine.push({record_id:r?.id??null,errors,record:r});else filtered.push(r);
  }
  return{quarantine,filtered};
}

export function evaluate(records, predictions, metadata = {}) {
  const lookup = new Map(records.map(r => [r.id, r])), seen = new Set(), results = [];
  for (const p of predictions) {
    if (seen.has(p.record_id)) throw new Error(`duplicate_prediction:${p.record_id}`);
    seen.add(p.record_id);
    const r = lookup.get(p.record_id);
    if (!r) throw new Error(`unknown_prediction:${p.record_id}`);
    if (['train', 'hidden_test'].includes(r.split)) throw new Error(`evaluation_split_not_authorized:${r.split}`);
    if (p.input_sha256 !== hash(project(r))) throw new Error(`input_hash_mismatch:${p.record_id}`);
    let parsed = p.output, parseError = false;
    if (typeof parsed === 'string') { try { parsed = JSON.parse(parsed); } catch { parseError = true; } }
    const validation = parseError ? { schema_valid: false, errors: ['invalid_json'], warnings: [] } : validateOutput(r, parsed);
    results.push({ record_id: r.id, scenario_family: r.scenario_family, domain: r.eval_labels.domain, expected_decision: r.eval_labels.decision,
      decision: validation.schema_valid ? parsed.decision : null, expected_rarity: r.eval_labels.rarity, rarity: validation.schema_valid ? parsed.opportunity?.rarity ?? null : null,
      ...validation, accepted_by_runtime: p.accepted_by_runtime === true, latency_ms: p.latency_ms ?? null, prompt_tokens: p.prompt_tokens ?? null,
      completion_tokens: p.completion_tokens ?? null, peak_ram_mib: p.peak_ram_mib ?? null, peak_vram_mib: p.peak_vram_mib ?? null, repair_count: p.repair_count ?? null });
  }
  const rate = predicate => results.length ? results.filter(predicate).length / results.length : null;
  const positives = results.filter(x => x.expected_decision === 'propose'), negatives = results.filter(x => x.expected_decision === 'none');
  const correct = (rows, predicate) => rows.length ? rows.filter(predicate).length / rows.length : null;
  const percentile = key => { const x = results.map(r => r[key]).filter(Number.isFinite).sort((a,b) => a-b); return x.length ? x[Math.ceil(x.length * 0.95)-1] : null; };
  const f1 = decision => {
    const tp = results.filter(x => x.decision === decision && x.expected_decision === decision).length;
    const fp = results.filter(x => x.decision === decision && x.expected_decision !== decision).length;
    const fn = results.filter(x => x.decision !== decision && x.expected_decision === decision).length;
    return 2 * tp + fp + fn ? 2 * tp / (2 * tp + fp + fn) : null;
  };
  const f1s = ['none', 'incubate', 'propose'].map(f1).filter(x => x !== null);
  const hasError = (r, prefixes) => r.errors.some(e => prefixes.some(p => e.startsWith(p)));
  return {
    schema_version: VERSION, status: predictions.length ? 'OFFLINE_SCORED_NOT_ADOPTION' : 'NOT_EVALUATED', model_invocation_performed_by_harness: false,
    ...metadata, predictions_count: results.length, expected_case_count: records.length,
    complete_coverage: records.every(r => seen.has(r.id)), independent_family_count: new Set(results.map(x => x.scenario_family)).size,
    metrics: { schema_valid_rate: rate(x => x.schema_valid), deterministic_compliance_rate: rate(x => x.errors.length === 0), decision_accuracy: rate(x => x.decision === x.expected_decision),
      decision_macro_f1: f1s.length ? f1s.reduce((a,b) => a+b,0) / f1s.length : null,
      abstention_accuracy: correct(negatives, x => x.decision === 'none'), incubation_accuracy: correct(results.filter(x => x.expected_decision === 'incubate'), x => x.decision === 'incubate'),
      meaningful_opportunity_recall: correct(positives, x => x.decision === 'propose'),
      false_special_rate_ordinary: correct(negatives, x => x.decision === 'propose'), rarity_label_accuracy: rate(x => x.rarity === x.expected_rarity),
      rare_inflation_on_negative_rate: correct(negatives, x => ['rare','unique','legendary_chain'].includes(x.rarity)),
      canonical_reference_valid_rate: rate(x => x.schema_valid && !hasError(x, ['unknown_entity:', 'unknown_fact:', 'unknown_enriched_entity', 'unknown_hook:', 'unknown_destination', 'unknown_objective:', 'missing_destination_fact:', 'missing_hook_fact:', 'invalid_reacting_npc', 'contradictory_destination_fact', 'contradictory_response_fact'])),
      evidence_id_grounding_rate: rate(x => x.schema_valid && !hasError(x, ['unknown_evidence:', 'unknown_eligible_evidence:', 'ungrounded_proposal'])),
      supported_mechanic_compliance_rate: rate(x => x.schema_valid && !hasError(x, ['unsupported_mechanic', 'proposal_without_destination', 'unknown_mechanic_tag:', 'destination_contract_mismatch', 'destination_binding_mismatch:mechanic_tags', 'quest_without_supplied_objective'])),
      unsupported_mechanic_rate: rate(x => hasError(x, ['unsupported_mechanic','unknown_mechanic_tag:','destination_contract_mismatch','destination_binding_mismatch:mechanic_tags'])), hallucinated_entity_id_rate: rate(x => hasError(x, ['unknown_entity:', 'unknown_enriched_entity', 'invalid_reacting_npc'])),
      uniqueness_conflict_rate: rate(x => hasError(x, ['uniqueness_not_reserved', 'name_collision:'])),
      combat_opportunity_recall: correct(positives.filter(x => x.domain === 'combat'), x => x.decision === 'propose'),
      noncombat_opportunity_recall: correct(positives.filter(x => ['crafting','exploration','social'].includes(x.domain)), x => x.decision === 'propose'),
      accepted_runtime_authority_violations: results.filter(x => x.accepted_by_runtime && x.errors.length).length,
      p95_latency_ms: percentile('latency_ms'), p95_prompt_tokens: percentile('prompt_tokens'), p95_completion_tokens: percentile('completion_tokens'),
      peak_ram_mib: results.some(x => Number.isFinite(x.peak_ram_mib)) ? Math.max(...results.map(x => x.peak_ram_mib ?? 0)) : null,
      peak_vram_mib: results.some(x => Number.isFinite(x.peak_vram_mib)) ? Math.max(...results.map(x => x.peak_vram_mib ?? 0)) : null,
      repair_rate: results.some(x => Number.isFinite(x.repair_count)) ? correct(results.filter(x => Number.isFinite(x.repair_count)), x => x.repair_count > 0) : null,
      repeated_name_template_rate: null, retry_rate: null },
    human_review_metrics: { causal_grounding: null, coherence: null, memorability: null, pronounceability: null, naming_distinctiveness: null, semantic_collisions: null },
    adoption_verdict: 'NOT_ELIGIBLE_PENDING_PAIRED_MODEL_RUNS_RUNTIME_PROOF_AND_HUMAN_REVIEW', results
  };
}

// note: Pair checks fail closed on missing cases and mismatched conditions; human and runtime evidence remain separate gates.
export function compareReports(baseline, adapter) {
  for (const field of ['input_manifest_sha256', 'generation_config_sha256', 'hardware_id', 'backend_sha256', 'base_model_sha256']) {
    if (!baseline[field] || baseline[field] !== adapter[field]) throw new Error(`paired_condition_mismatch:${field}`);
  }
  if (!baseline.complete_coverage || !adapter.complete_coverage || baseline.predictions_count === 0) throw new Error('incomplete_paired_coverage');
  if (canonical(baseline.results.map(x => x.record_id).sort()) !== canonical(adapter.results.map(x => x.record_id).sort())) throw new Error('paired_case_mismatch');
  const deltas = {};
  for (const key of Object.keys(baseline.metrics)) deltas[key] = typeof baseline.metrics[key] === 'number' && typeof adapter.metrics[key] === 'number' ? adapter.metrics[key] - baseline.metrics[key] : null;
  return { status: 'PAIRED_OFFLINE_SCORED_NOT_ADOPTION', metric_deltas_adapter_minus_baseline: deltas,
    cluster_confidence_intervals: null, causal_quality_improvement: 'PENDING_BLIND_HUMAN_REVIEW', runtime_persistence_proof: 'NOT_RUN', adoption_verdict: 'NOT_ELIGIBLE' };
}
