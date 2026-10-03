import test from 'node:test';
import assert from 'node:assert/strict';
import { developmentFixtures } from './catalog.mjs';
import { validateOutput, validateRecord, project, hash, trainingEligible, verifySplits, deduplicate, filterCandidates, evaluate, compareReports, schemaErrors, normalizeName, phoneticKey } from './core.mjs';
const base=()=>structuredClone(developmentFixtures()[1]);
const mutate=(edit)=>{const r=base(),o=structuredClone(r.expected_output);edit(r,o);return validateOutput(r,o);};
const has=(result,key)=>assert.ok(result.errors.some(x=>x.startsWith(key)),result.errors.join('\n'));

// note: All fixtures below belong to development_only; public/hidden evaluation answers are not test inputs.
test('development fixtures satisfy strict schemas and domain gates',()=>{for(const r of developmentFixtures())assert.deepEqual(validateRecord(r),[]);});
test('schema rejects numeric authority and unknown validator keywords',()=>{
  const r=base(),o=structuredClone(r.expected_output);o.opportunity.damage=900;assert.equal(validateOutput(r,o).schema_valid,false);
  const inheritedKey=structuredClone(r.expected_output);inheritedKey.toString='bypass';assert.equal(validateOutput(r,inheritedKey).schema_valid,false);
  assert.match(schemaErrors({}, {type:'object', unimplementedKeyword:true})[0],/unsupported schema keyword/);
});
test('proposal requires deterministic eligibility, diverse evidence and supplied destination',()=>{
  has(mutate(r=>r.rarity_constraints.eligibility_validated=false),'eligibility_not_validated');
  has(mutate(r=>r.behavior_summary.low_diversity_farming=true),'farming_reward');
  has(mutate((r,o)=>o.opportunity.mechanical_template_id='fixture:template:teleport'),'unsupported_mechanic');
  has(mutate((r,o)=>o.naming=null),'proposal_missing_name_record');
});
test('rarity and uniqueness cannot be upgraded by model',()=>{
  has(mutate((r,o)=>o.opportunity.rarity='unique'),'rarity_ceiling');
  has(mutate((r,o)=>o.opportunity.rarity='unique'),'uniqueness_not_reserved');
  has(mutate((r,o)=>o.opportunity.rarity='legendary_chain'),'legendary_not_authorized');
});
test('dead NPC, hallucinated entity and unsupported evidence IDs are rejected',()=>{
  has(mutate(r=>r.known_entities[0].status='unavailable'),'unavailable_destination:');
  has(mutate((r,o)=>o.opportunity.canonical_entity_ids=['fixture:entity:invented']),'unknown_entity:');
  has(mutate((r,o)=>o.interpretation.supporting_evidence_ids=['fixture:evidence:invented']),'unknown_evidence:');
});
test('missing and contradictory facts force deferral',()=>{
  has(mutate(r=>r.world_context.missing_required_facts=['reachability']),'unresolved_required_facts');
  has(mutate(r=>r.world_context.facts[0].consistency='contradictory'),'canon_conflict_not_deferred');
});
test('NPC knowledge is explicit and unauthorized facts are removed from projection',()=>{
  const r=base();r.task='NPC_WORLD_REACTION';r.world_context.facts.push({id:'fixture:fact:secret',text:'Private fact',entity_ids:[],consistency:'consistent'});
  assert.equal(project(r).world_context.facts.some(x=>x.id==='fixture:fact:secret'),false);
  const o=structuredClone(r.expected_output);o.decision='none';o.opportunity=null;o.naming=null;o.response={npc_entity_id:'fixture:entity:mara',knowledge_fact_ids:['fixture:fact:secret'],text:'I know your secret.'};
  has(validateOutput(r,o),'unknown_npc_knowledge:');
});
test('names cannot collide, invent ancestry or imply forbidden mechanics',()=>{
  has(mutate((r,o)=>r.existing_content.push({id:'fixture:identity:prior',name:o.naming.selected_name,category:'title',rarity:'unique',naming_lineage_id:null,canonical_entity_ids:[]})),'name_collision:');
  has(mutate((r,o)=>o.naming.parent_identity_id='fixture:identity:invented'),'unknown_parent_identity');
  has(mutate((r,o)=>r.eval_labels.unsupported_name_terms=['Small Returns']),'unsupported_name_implication:');
});
test('prose cannot announce grants, expose checklists or supply asset paths',()=>{
  has(mutate((r,o)=>o.opportunity.diegetic_reason='You have unlocked a title.'),'grant_before_commit');
  has(mutate((r,o)=>o.opportunity.diegetic_reason='Hidden prerequisite 4/5 complete.'),'hidden_checklist_exposure');
  has(mutate((r,o)=>o.opportunity.diegetic_reason='Load Assets/Reward.prefab'),'executable_or_asset_path');
});
test('projections exclude labels, expected answers and split metadata',()=>{
  const p=project(base());for(const field of ['source','expected_output','eval_labels','split','scenario_family'])assert.equal(Object.hasOwn(p,field),false);
});
test('family seals reject leakage and hidden family injection',()=>{
  const r=base();assert.deepEqual(verifySplits([r],{families:{development_only:'development'},reserved_hidden_families:[]}),[]);
  assert.ok(verifySplits([r],{families:{development_only:'train'},reserved_hidden_families:[]}).length);
  assert.ok(verifySplits([r],{families:{development_only:'development'},reserved_hidden_families:['development_only']}).length);
});
test('deduplication finds identical model inputs and pending examples cannot train',()=>{
  const r=base();assert.equal(deduplicate([r,structuredClone(r)]).exact_input_duplicates.length,1);
  r.split='train';assert.equal(trainingEligible(r),false);
});
test('normalization preserves non-Latin identity and phonetic warnings are only advisory',()=>{
  assert.equal(normalizeName('Élan — Keeper'),'elan keeper');assert.equal(normalizeName('渡守'),'渡守');
  assert.equal(phoneticKey('Smith'),phoneticKey('Smyth'));assert.equal(phoneticKey('渡守'),null);
});
test('candidate filtering quarantines malformed and duplicate input without auto-approval',()=>{
  const r=base();r.split='train';const result=filterCandidates([null,r,r],[],{families:{development_only:'train'},reserved_hidden_families:[]});
  assert.equal(result.filtered.length,1);assert.equal(result.quarantine.length,2);assert.equal(trainingEligible(result.filtered[0]),false);
});
test('dedicated NPC naming refuses absent context and grants',()=>{
  const r=base();r.task='NPC_NAME_GENERATION';const o=structuredClone(r.expected_output);o.decision='none';o.opportunity=null;
  has(validateOutput(r,o),'missing_npc_naming_context_or_parts');
  o.naming.accepted=true;assert.equal(validateOutput(r,o).schema_valid,false);
});
test('harness rejects duplicate, wrong-input and train predictions',()=>{
  const r=base(),p={record_id:r.id,input_sha256:hash(project(r)),output:r.expected_output};
  assert.throws(()=>evaluate([r],[p,p]),/duplicate_prediction/);
  assert.throws(()=>evaluate([r],[{...p,input_sha256:'wrong'}]),/input_hash_mismatch/);
  r.split='train';assert.throws(()=>evaluate([r],[p]),/evaluation_split_not_authorized/);
});
test('empty reports contain no invented model metrics; pairing requires complete matching runs',()=>{
  const empty=evaluate(developmentFixtures(),[]);assert.equal(empty.status,'NOT_EVALUATED');assert.equal(empty.metrics.schema_valid_rate,null);assert.equal(empty.metrics.repair_rate,null);
  assert.throws(()=>compareReports(empty,empty),/paired_condition_mismatch/);
  const r=base(),report=evaluate([r],[{record_id:r.id,input_sha256:hash(project(r)),output:r.expected_output}],{input_manifest_sha256:'same',generation_config_sha256:'same',hardware_id:'same',backend_sha256:'same',base_model_sha256:'same'});
  assert.equal(compareReports(report,report).adoption_verdict,'NOT_ELIGIBLE');assert.equal(report.human_review_metrics.memorability,null);
});

// note: These counterfactuals exercise input/output authority failures independently of any public evaluation answer.
test('evidence sessions cannot exceed canonical player history',()=>{
  const r=base();r.player_evidence[0].sessions=r.player_state.session_count+1;
  assert.ok(validateRecord(r).some(x=>x.startsWith('evidence_sessions_exceed_history:')));
});
test('destinations cannot invent or change facts, objectives, entities or effect tags',()=>{
  has(mutate((r,o)=>o.opportunity.destination_id='fixture:destination:invented'),'unknown_destination');
  has(mutate((r,o)=>o.opportunity.canonical_fact_ids=[]),'missing_destination_fact:');
  has(mutate((r,o)=>o.opportunity.objective_ids=['fixture:objective:invented']),'unknown_objective:');
  has(mutate((r,o)=>o.opportunity.mechanic_tags=['teleport']),'unknown_mechanic_tag:');
  has(mutate((r,o)=>o.opportunity.canonical_entity_ids=[]),'destination_binding_mismatch:canonical_entity_ids');
});
test('quest proposals require supplied objective bindings',()=>{
  const r=base(),o=structuredClone(r.expected_output),cap=r.available_capabilities[0],dest=r.world_context.destinations[0];
  cap.opportunity_kinds=['quest'];cap.effect_kind='quest';dest.kind='quest';o.opportunity.kind='quest';o.naming.presentation_only=false;
  has(validateOutput(r,o),'quest_without_supplied_objective');
  const objective={id:'fixture:objective:dev_bound',template_id:cap.template_id,target_entity_id:o.opportunity.canonical_entity_ids[0],canonical_fact_ids:dest.required_fact_ids,supplied_objective:'Review the supplied record.',authority:'offline_mock_not_runtime_certified'};
  r.world_context.objective_records=[objective];dest.objective_ids=[objective.id];o.opportunity.objective_ids=[objective.id];r.expected_output=o;
  assert.deepEqual(validateRecord(r),[]);
  objective.template_id='fixture:template:invented';assert.ok(validateRecord(r).includes('objective_destination_contract_mismatch'));
});
test('contact opportunity cannot use facts outside the contact knowledge',()=>{
  const r=base(),o=structuredClone(r.expected_output),dest=r.world_context.destinations[0];
  const privateId=dest.required_fact_ids.at(-1);dest.npc_knowledge_requirements=[{npc_entity_id:'fixture:entity:mara',fact_ids:[privateId]}];
  has(validateOutput(r,o),'unknown_destination_npc_knowledge:');
  r.known_entities.find(x=>x.id==='fixture:entity:mara').knowledge_fact_ids.push(privateId);
  assert.deepEqual(validateOutput(r,o).errors,[]);
});
test('future hooks require supplied canonical support',()=>{
  const r=base(),o=structuredClone(r.expected_output),hook=r.world_context.hook_records[0];o.opportunity.future_hooks=[hook.id];
  hook.required_fact_ids=['fixture:fact:missing_hook'];has(validateOutput(r,o),'missing_hook_fact:');
  r.world_context.hook_records=[];has(validateOutput(r,o),'hook_without_supplied_context');
  r.world_context.hook_records=[hook];hook.required_fact_ids=[];r.known_entities[0].status='unavailable';has(validateOutput(r,o),'unavailable_hook_destination');
});
test('accepted ancestry cannot be downgraded by a generated child',()=>{
  const r=base(),o=structuredClone(r.expected_output),parent='fixture:identity:development_parent';
  r.existing_content=[{id:parent,name:'Earlier Local Identity',category:'title',rarity:'unique',naming_lineage_id:null,canonical_entity_ids:[],semantic_identity:'prior care',motifs:[]}];
  r.player_state.accepted_identity_ids=[parent];Object.assign(o.naming,{parent_identity_id:parent,evolution_reason:'Later care.',behavioral_delta:'Broader context.',mechanical_delta:'Presentation only.'});
  has(validateOutput(r,o),'ancestry_rarity_downgrade');
});
test('unsupported effect implications are rejected in every proposed name candidate',()=>{
  const r=base(),o=structuredClone(r.expected_output);r.eval_labels.unsupported_name_terms=['interrupt'];o.naming.candidates.push('The Interrupted Threat');
  has(validateOutput(r,o),'unsupported_name_implication:interrupt');
});
test('typed canonical input references fail closed before synthetic admission',()=>{
  const r=base();r.world_context.destinations[0].required_fact_ids.push('fixture:fact:invented');
  assert.ok(validateRecord(r).some(x=>x.startsWith('invalid_destination_fact_input:')));
  const clean=base();clean.world_context.facts[0].entity_ids.push('fixture:entity:invented');
  assert.ok(validateRecord(clean).some(x=>x.startsWith('invalid_fact_entity_input:')));
});
