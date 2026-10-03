import { readFileSync, writeFileSync, mkdirSync, statSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname, relative, isAbsolute } from 'node:path';
import { catalog, developmentFixtures } from './catalog.mjs';
import { TASKS, SPLITS, recordSchema, outputSchema } from './schema.mjs';
import { VERSION, canonical, hash, validateRecord, verifySplits, deduplicate, filterCandidates, namingStatistics, project, trainingEligible, PROJECTIONS, evaluate, compareReports } from './core.mjs';
const root = dirname(fileURLToPath(import.meta.url));
const seal = JSON.parse(readFileSync(resolve(root, 'family-seal.json'), 'utf8'));
const readJSON = name => JSON.parse(readFileSync(resolve(root, name), 'utf8'));
const write = (name, value) => {
  const destination = resolve(root, name), rel = relative(root, destination);
  if (rel.startsWith('..') || isAbsolute(rel)) throw Error('output_outside_tooling_root');
  mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, value, 'utf8');
};
const json = (name, value) => write(name, JSON.stringify(value, null, 2) + '\n');
const jsonl = (name, rows) => write(name, rows.map(canonical).join('\n') + (rows.length ? '\n' : ''));
function readJSONL(path) {
  const file = resolve(path);
  if (statSync(file).size > 16 * 1024 * 1024) throw Error('input_file_exceeds_16MiB_budget');
  return readFileSync(file, 'utf8').split(/\r?\n/).filter(x => x.trim()).map((x, i) => { try { return JSON.parse(x); } catch { throw Error(`invalid_jsonl_line:${i+1}`); } });
}
const cases = catalog();
const counts = rows => Object.fromEntries(['none','incubate','personalized','rare','unique','legendary_chain'].map(k => [k, rows.filter(r => (r.eval_labels.rarity ?? r.eval_labels.decision) === k).length]));
function check() {
  const errors = cases.flatMap(r => validateRecord(r).map(e => `${r.id}:${e}`));
  errors.push(...verifySplits(cases, seal));
  if (new Set(cases.map(x => x.id)).size !== cases.length) errors.push('duplicate_case_id');
  const covered = new Set(cases.flatMap(r => r.eval_labels.coverage_ids));
  for (let i=1; i<=25; i++) if (!covered.has(`C${String(i).padStart(2,'0')}`)) errors.push(`missing_required_case:C${i}`);
  for (const task of TASKS) if (!cases.some(r => r.task === task)) errors.push(`missing_task:${task}`);
  const train=cases.filter(r=>r.split==='train'),trainDistribution=counts(train);
  for (const task of TASKS) if (!train.some(r=>r.task===task)) errors.push(`missing_train_task:${task}`);
  const previous=readJSONL(resolve(root,'data/seed.v0.1.0.jsonl'));
  if(hash(previous)!==seal.previous_dataset_canonical_sha256)errors.push('archived_dataset_fingerprint_changed');
  for(const old of previous){const current=cases.find(r=>r.id===old.id);
    if(!current||current.scenario_family!==old.scenario_family||current.split!==old.split)errors.push(`existing_family_assignment_changed:${old.id}`);}
  const trainBands={none:[0.30,0.40],incubate:[0.20,0.30],personalized:[0.20,0.25],rare:[0.08,0.15],unique:[0.02,0.05],legendary_chain:[0,0.02]};
  for(const [category,[minimum,maximum]]of Object.entries(trainBands))if(trainDistribution[category]/train.length<minimum||trainDistribution[category]/train.length>maximum)errors.push(`train_distribution_outside_initial_band:${category}`);
  const d = deduplicate(cases);
  if (d.exact_input_duplicates.length) errors.push('exact_input_duplicates');
  if (d.lexical_near_duplicates.some(x => x.cross_split)) errors.push('cross_split_near_duplicate_needs_review');
  const matrix = readJSON('requirements.json');
  if (matrix.main.length !== 29 || matrix.naming.length !== 31) errors.push('traceability_row_count');
  if(matrix.naming_source.status==='ORIGINAL_NUMBERED_TEXT_AVAILABLE'){
    const source=readFileSync(resolve(root,'../../',matrix.naming_source.path),'utf8');
    if(hash(source)!==matrix.naming_source.utf8_sha256)errors.push('naming_source_hash_drift');
    const headings=[...source.matchAll(/^#{1,2} (\d+)\. (.+)$/gm)];
    if(headings.length!==31||headings.some((h,i)=>matrix.naming[i].original_section!==Number(h[1])||matrix.naming[i].requirement!==h[2]))errors.push('naming_original_section_map_mismatch');
  }
  const report = { schema_version: VERSION, status: errors.length ? 'FAIL' : 'PASS_OFFLINE_ARTIFACT_CHECKS_ONLY', records: cases.length,
    decision_distribution_all_public_records: counts(cases), train_candidate_distribution: counts(cases.filter(r => r.split === 'train')),
    task_counts: Object.fromEntries(TASKS.map(t => [t,cases.filter(r => r.task === t).length])),
    train_task_counts: Object.fromEntries(TASKS.map(t=>[t,train.filter(r=>r.task===t).length])),
    previous_family_memberships_preserved: !errors.some(x=>x.startsWith('existing_family_assignment_changed:')),
    archived_v0_1_0_dataset_sha256:hash(previous),
    split_counts: Object.fromEntries(SPLITS.map(s => [s,cases.filter(r => r.split === s).length])),
    human_review_pending: cases.filter(r => r.source.human_review === 'pending').length, training_eligible_count: cases.filter(trainingEligible).length,
    hidden_test_status: seal.hidden_test_status, exact_naming_traceability: matrix.naming_source.status,
    model_invocations: 0, dataset_sha256: hash(cases), family_seal_sha256: hash(seal), errors, deduplication: d };
  return report;
}

const command = process.argv[2] ?? 'check';
try {
  if (command === 'build') {
    // note: Regeneration must never erase subsequently recorded model results.
    for(const variant of ['baseline','adapter'])if(existsSync(resolve(root,`reports/${variant}.json`))){const previous=readJSON(`reports/${variant}.json`);
      if(previous.predictions_count>0||previous.run_status==='RECORDED_ACTUAL_MODEL_RUN')throw Error('preserve recorded evaluation reports before regeneration');}
    const report = check();
    if (report.errors.length) throw Error(report.errors.join('\n'));
    json('schemas/record.schema.json', recordSchema); json('schemas/output.schema.json', outputSchema);
    json('schemas/task-projections.json', { version: VERSION, fields: PROJECTIONS, policy: 'Labels, expected outputs, family/split/source metadata never enter model input.' });
    jsonl(`data/seed.v${VERSION}.jsonl`, cases);
    for (const split of SPLITS) jsonl(`data/splits/${split}.pending-review.jsonl`, cases.filter(r => r.split === split));
    jsonl('review/projected-inputs.jsonl', cases.filter(r => r.split !== 'hidden_test').map(r => ({ record_id: r.id, task: r.task, input_sha256: hash(project(r)), input: project(r), output_schema: 'schemas/output.schema.json', training_eligible: trainingEligible(r) })));
    const selected = ['c01','c02','c03','c05','c14','c18','c20','s41','s42','s44','s45','s46','s50','s52','t53','t54','t60','t61','t64'];
    const pack = cases.filter(r => selected.includes(r.id.split(':').at(-1)));
    jsonl('review/review-pack.jsonl', pack);
    // note: The readable pack preserves the same inputs/targets and shows supplied grounding alongside pending judgments.
    write('review/REVIEW_PACK.md',[
      `# Pending review pack v${VERSION}`,'',
      'Original agent-authored offline candidates. All judgments are pending. Mock destinations are not runtime-certified. This pack contains public review/evaluation cases and must never be sent to a teacher or training job.','',
      `Dataset canonical SHA-256: ${report.dataset_sha256}`,'',
      ...pack.flatMap(r=>{const o=r.expected_output;return[
        `## ${r.id} — ${r.task}`,'',`Family: ${r.scenario_family}; split: ${r.split}. Expected: **${o.decision}${o.opportunity?` / ${o.opportunity.rarity}`:''}**.`,
        '', 'Evidence: '+(r.player_evidence.map(e=>`${e.note} [${e.attempts} attempts, ${e.successes} successes, ${e.failures} failures; ${e.sessions} sessions; ${e.distinct_contexts} contexts, ${e.distinct_targets} targets]`).join(' ')||'No player-evidence input for this role.'),
        '',`Interpretation: ${o.interpretation.summary}`,
        ...(o.naming?['',`Name: **${o.naming.selected_name}**. Candidates: ${o.naming.candidates.join('; ')}.`,...(o.naming.parent_identity_id?[`Parent: ${o.naming.parent_identity_id}; lineage: ${o.naming.naming_lineage_id}. ${o.naming.evolution_reason}`]:[])]:[]),
        ...(o.opportunity?['',`Proposal: ${o.opportunity.diegetic_reason}`,`Template: ${o.opportunity.mechanical_template_id}; destination: ${o.opportunity.destination_id}; effects: ${o.opportunity.mechanic_tags.join(', ')}.`,
          '',...o.opportunity.canonical_fact_ids.map(f=>`- Supplied ${f}: ${r.world_context.facts.find(x=>x.id===f)?.text}`),
          ...r.world_context.objective_records.map(x=>`- Supplied ${x.id}: ${x.supplied_objective}`),
          ...o.opportunity.future_hooks.map(h=>`- Hook ${h}: ${r.world_context.hook_records.find(x=>x.id===h)?.description}`)]:[]),
        ...(o.response?['',`Authorized reaction: “${o.response.text}” Knowledge: ${o.response.knowledge_fact_ids.join(', ')}.`]:[]),
        ...(!o.opportunity&&(o.response||o.naming)?['',...r.world_context.facts.filter(f=>o.response?o.response.knowledge_fact_ids.includes(f.id):true).map(f=>`- Supplied ${f.id}: ${f.text}`)]:[]),
        ...(o.naming?.parent_identity_id?['',`Accepted parent record: ${r.existing_content.find(c=>c.id===o.naming.parent_identity_id)?.name}; rarity: ${r.existing_content.find(c=>c.id===o.naming.parent_identity_id)?.rarity}.`]:[]),
        ...(o.naming?.npc_parts?['',`NPC parts: ${JSON.stringify(o.naming.npc_parts)}. Supplied brief: ${JSON.stringify(r.naming_context)}.`]:[]),
        '', 'Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.',''
      ];}),
      '## New train-only families','', '| Record | Task | Decision / rarity | Family |','|---|---|---|---|',
      ...cases.filter(r=>r.id.split(':').at(-1).startsWith('t')).map(r=>`| ${r.id} | ${r.task} | ${r.eval_labels.rarity??r.eval_labels.decision} | ${r.scenario_family} |`),'',
      'All 64 records need human review. The 19-item pack is representative and cannot approve the remaining records. Actual hidden inputs remain unauthored.',''
    ].join('\n'));
    json('review/review-form.template.json', { version: VERSION, status: 'PENDING_USER_REVIEW', scale: '1 poor, 5 strong; null unreviewed',
      items: pack.map(r => ({ record_id: r.id, record_sha256: hash(r), rights_approved: null, decision_approved: null, causal_grounding: null, mechanic_consistency: null,
        rarity_fit: null, naming_memorability: null, pronounceability: null, distinct_history_fit: null, comments: '', reviewer: '', reviewed_at: null })) });
    json('reports/dataset-check.json', report);
    json('reports/naming-fixture-statistics.json',namingStatistics(cases));
    jsonl('review/generated-name-records.pending.jsonl',cases.filter(r=>r.expected_output.naming).map(r=>({record_id:r.id,name:r.expected_output.naming.selected_name,
      content_type:r.expected_output.naming.category,rarity:r.eval_labels.rarity,semantic_kernel:r.expected_output.naming.semantic_kernel,motifs:r.expected_output.naming.motifs,
      behavior_evidence_ids:r.expected_output.interpretation.supporting_evidence_ids,world_context_ids:r.expected_output.opportunity?.canonical_entity_ids??r.known_entities.map(e=>e.id),
      mechanical_template_id:r.expected_output.opportunity?.mechanical_template_id??null,lineage_id:r.expected_output.naming.naming_lineage_id,
      generation_version:`offline-seed-${VERSION}`,model_version:'NOT_RUN_ORIGINAL_AGENT_AUTHORED_FIXTURE',adapter_version:'NOT_RUN',accepted:false})));
    const evaluationCases = cases.filter(r => ['validation','adversarial','regression'].includes(r.split));
    const metadata = { input_manifest_sha256: hash(evaluationCases.map(r => ({ id:r.id, input_sha256:hash(project(r)) }))), base_model_sha256: readJSON('audit.json').model.sha256,
      generation_config_sha256: null, hardware_id: null, backend_sha256: readJSON('audit.json').backend.impl_sha256 };
    for (const variant of ['baseline','adapter']) json(`reports/${variant}.json`, evaluate(evaluationCases, [], { ...metadata, variant, run_status: 'NOT_RUN' }));
    const dev = developmentFixtures();
    json('reports/harness-self-check.json', evaluate(dev, dev.map(r=>({ record_id:r.id, input_sha256:hash(project(r)), output:r.expected_output })), { variant:'DEVELOPMENT_FIXTURE_ORACLE_NOT_A_MODEL', run_status:'SELF_TEST_ONLY' }));
    json('reports/dataset-manifest.json', { version: VERSION, source: 'catalog.mjs hand-authored fixtures', schema_sha256: hash(recordSchema), output_schema_sha256: hash(outputSchema),
      dataset_sha256: report.dataset_sha256, family_seal_sha256: report.family_seal_sha256, training_ready:false, runtime_capabilities_certified:false,
      current_dataset_path:`data/seed.v${VERSION}.jsonl`,archived_dataset_policy:'v0.1.0 retained for provenance/membership verification only; superseded targets are invalid for training/review.',
      target_expansion_distribution: { total:200, none:70, incubate:50, personalized:48, rare:24, unique:7, legendary_chain:1 }, actual_distribution:report.decision_distribution_all_public_records });
    console.log(JSON.stringify({status:report.status,records:cases.length,training_eligible:report.training_eligible_count,model_invocations:0}));
  } else if (command === 'check') {
    const report=check(); json('reports/dataset-check.json',report); console.log(JSON.stringify(report,null,2)); if(report.errors.length) process.exitCode=1;
  } else if (command === 'filter') {
    const input=process.argv[3]; if(!input)throw Error('usage: node cli.mjs filter explicit-candidates.jsonl');
    const candidates=readJSONL(input);
    if(candidates.length>500)throw Error('candidate_batch_exceeds_500_review_budget');
    const {quarantine,filtered}=filterCandidates(candidates,cases.filter(r=>r.split==='train'),seal);
    const d=deduplicate([...cases.filter(r=>r.split==='train'),...filtered]);
    jsonl('reports/candidates.filtered.pending-review.jsonl',filtered);jsonl('reports/candidates.quarantined.jsonl',quarantine);
    json('reports/candidate-filter.json',{status:'REVIEW_REQUIRED_NOT_TRAINING_APPROVAL',candidate_count:candidates.length,deterministically_valid:filtered.length,quarantined:quarantine.length,training_eligible:filtered.filter(trainingEligible).length,deduplication:d});
    console.log(JSON.stringify({filtered:filtered.length,quarantined:quarantine.length,review_required:true}));
  } else if (command === 'prepare-teacher') {
    // note: This emits local candidate-generation prompts only; there is no API/client/network implementation.
    jsonl('review/teacher-prompts.train-only.jsonl',cases.filter(r=>r.split==='train').map(r=>({ task:'ORIGINAL_SYNTHETIC_CANDIDATE',source_family:r.scenario_family,
      instruction:'Create an original counterfactual in this same family. Use only supplied fixture facts and schema. No copyrighted story text, private text, hidden reasoning, new mechanics, or authoritative grants. Output requires separate review.',
      input:project(r),record_schema:recordSchema })));
    console.log('Local prompts prepared; teacher invocations=0.');
  } else if (command === 'evaluate') {
    const file=process.argv[3], variant=process.argv[4]; if(!file||!['baseline','adapter'].includes(variant))throw Error('usage: node cli.mjs evaluate predictions.jsonl baseline|adapter');
    const metadata=readJSON('evaluation-run.template.json');
    if(metadata.run_status!=='RECORDED_ACTUAL_MODEL_RUN')throw Error('record actual run metadata explicitly; template is NOT_RUN');
    const report=evaluate(cases.filter(r=>['validation','adversarial','regression'].includes(r.split)),readJSONL(file),{...metadata,variant});
    json(`reports/${variant}.json`,report);console.log(JSON.stringify({status:report.status,coverage:report.complete_coverage,adoption:report.adoption_verdict}));
  } else if (command === 'pair') {
    const comparison=compareReports(readJSON('reports/baseline.json'),readJSON('reports/adapter.json')); json('reports/paired-comparison.json',comparison);console.log(JSON.stringify(comparison));
  } else if(command==='export-training') {
    const readiness=readJSON('reports/dataset-manifest.json');
    if(!readiness.training_ready||!readiness.runtime_capabilities_certified)throw Error('dataset not approved for training; mock destinations and integration blockers remain');
    const ready=cases.filter(trainingEligible); if(!ready.length)throw Error('no human-approved, rights-approved train records; export refused');
    jsonl('data/train.approved.messages.jsonl',ready.map(r=>({record_id:r.id,messages:[{role:'system',content:'YourQuest proposal-only interpreter. Return bounded schema JSON, concise external rationale and supplied evidence IDs. Never mutate state or invent mechanics/canon. No hidden reasoning.'},{role:'user',content:canonical(project(r))},{role:'assistant',content:canonical(r.expected_output)}]})));
  } else throw Error(`unknown_command:${command}`);
} catch(error){ console.error(String(error.message??error)); process.exitCode=1; }
