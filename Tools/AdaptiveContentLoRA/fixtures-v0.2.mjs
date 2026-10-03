// note: These original fixture repairs bind supplied narrative facts to mock destinations. They do not extend Unity canon or executors.
const id = value => `fixture:${value}`;
const FORD=id('entity:reed_ford'), NPC=id('entity:mara'), CULTURE=id('culture:fordfolk'), FACT=id('fact:ford'), HOOK=id('hook:ford_return');
const entity=(key,kind='location',status='available',culture=CULTURE)=>({id:id(`entity:${key}`),kind,status,culture_id:culture,knowledge_fact_ids:[]});
function fact(record,key,text,entities) {
  const value={id:id(`fact:${key}`),text,entity_ids:entities,consistency:'consistent'};
  record.world_context.facts.push(value);return value.id;
}
function initialize(record) {
  record.schema_version='0.2.0';
  Object.assign(record.world_context,{destinations:[],objective_records:[],hook_records:[{id:HOOK,target_entity_id:FORD,required_fact_ids:[FACT],description:'Return to the supplied crossing. This hook does not authorize new content or mechanics.'}]});
  return record;
}

// note: Positive examples must name an input-owned job, event or presentation slot; generated prose cannot supply its existence.
export function ground(record,{text,entities=[],contacts=[],objective=null,knowledge=false,hook=null}) {
  const key=record.id.split(':').at(-1), opportunity=record.expected_output.opportunity;
  record.known_entities.push(...entities);
  const suppliedContacts=contacts.map(key=>entity(key,'npc','alive'));
  record.known_entities.push(...suppliedContacts);opportunity.canonical_entity_ids.push(...suppliedContacts.map(x=>x.id));
  const factId=fact(record,`${key}_destination`,text,opportunity.canonical_entity_ids);
  const required=[FACT,factId],cap=record.available_capabilities.find(x=>x.template_id===opportunity.mechanical_template_id);
  if(knowledge)record.known_entities.find(x=>x.id===NPC).knowledge_fact_ids.push(factId);
  const objectiveIds=[];
  if(objective){const target=opportunity.canonical_entity_ids.find(x=>x!==FORD)??FORD,objectiveId=id(`objective:${key}`);
    record.world_context.objective_records.push({id:objectiveId,template_id:cap.template_id,target_entity_id:target,canonical_fact_ids:[factId],
      supplied_objective:objective,authority:'offline_mock_not_runtime_certified'});objectiveIds.push(objectiveId);}
  if(hook){const hookId=id(`hook:${key}_continuation`),hookFact=fact(record,`${key}_hook`,hook,opportunity.canonical_entity_ids);
    required.push(hookFact);record.world_context.hook_records.push({id:hookId,target_entity_id:suppliedContacts[0]?.id??opportunity.canonical_entity_ids.at(-1),required_fact_ids:[hookFact],description:hook});
    record.world_context.hook_ids.push(hookId);opportunity.future_hooks=[hookId];}
  Object.assign(opportunity,{destination_id:id(`destination:${key}`),canonical_fact_ids:required,objective_ids:objectiveIds,mechanic_tags:[...cap.mechanic_tags]});
  record.world_context.destinations.push({id:opportunity.destination_id,template_id:cap.template_id,kind:opportunity.kind,
    canonical_entity_ids:[...opportunity.canonical_entity_ids],required_fact_ids:required,objective_ids:objectiveIds,permitted_mechanic_tags:[...cap.mechanic_tags],
    npc_knowledge_requirements:[...(knowledge?[{npc_entity_id:NPC,fact_ids:[factId]}]:[]),...suppliedContacts.map(contact=>({npc_entity_id:contact.id,fact_ids:required.filter(x=>x!==FACT)}))]});
  for(const contact of suppliedContacts)contact.knowledge_fact_ids=required.filter(x=>x!==FACT);
  record.expected_output.naming.semantic_kernel.context=text;
  return record;
}
function recount(record,counts) {
  const [attempts,successes,failures,sessions,contexts,targets]=counts;
  Object.assign(record.player_evidence[0],{attempts,successes,failures,sessions,distinct_contexts:contexts,distinct_targets:targets});record.player_state.session_count=sessions;
}
function lineage(record,{parentName,parentRarity='personalized',category='title',lineageName,delta}) {
  const key=record.id.split(':').at(-1),parent=id(`identity:${key}_parent`),line=id(`lineage:${key}`),entities=record.expected_output.opportunity.canonical_entity_ids;
  record.existing_content.push({id:parent,name:parentName,category,rarity:parentRarity,naming_lineage_id:line,canonical_entity_ids:entities,semantic_identity:delta,motifs:['continuity']},
    {id:line,name:lineageName,category:'opportunity',rarity:parentRarity,naming_lineage_id:line,canonical_entity_ids:entities,semantic_identity:`Accepted ${lineageName} history`,motifs:['continuity']});
  record.player_state.accepted_identity_ids=[parent];record.player_state.prior_choices=[{offer_id:id(`offer:${key}_parent`),state:'accepted',identity_id:parent}];
  Object.assign(record.expected_output.naming,{parent_identity_id:parent,naming_lineage_id:line,evolution_reason:delta,behavioral_delta:delta,
    mechanical_delta:record.expected_output.opportunity.kind==='quest'?'Only the supplied follow-up objective becomes available.':'Presentation-only title evolution; the existing mechanics stay fixed.'});
}
const alternatives=(record,names)=>{record.expected_output.naming.candidates=[record.expected_output.naming.selected_name,...names];record.expected_output.naming.candidate_families=['remembered method','local consequence'];};

export function repairAndExpand(rows,make) {
  rows.forEach(initialize);const by=key=>rows.find(x=>x.id===id(`case:${key}`));
  by('c02').player_evidence[0].note='Compared an axe, a stone and empty hands on two tree types; only two short outings are recorded.';
  by('c02').player_evidence[0].tool_ids=[id('tool:axe'),id('tool:stone'),id('tool:hands')];by('c03').player_evidence[0].tool_ids=[id('tool:hands')];
  // note: A personalized parent evolves into a rare title; an accepted unique identity is never silently downgraded.
  by('c18').existing_content.find(x=>x.id===id('identity:c18_parent')).rarity='personalized';
  for(const [key,counts]of [['s35',[1,1,0,1,1,1]],['s36',[2,1,1,2,2,2]],['s37',[1,1,0,1,1,1]],['s38',[1,1,0,1,1,1]],['s41',[1,1,0,1,1,1]],['s42',[1,1,0,1,1,1]]])recount(by(key),counts);
  by('s43').player_evidence=[];by('s43').behavior_summary.signals=[];by('s43').player_state.session_count=0;by('s43').rarity_constraints.eligible_evidence_ids=[];
  by('s41').expected_output.response.text='You brought the ledger back. Thank you for returning it.';
  const projectile=by('s44');
  projectile.player_evidence[0].note='Across several visits, selected clear lines of sight between branches and aimed ordinary projectiles at already supplied practice posts before attempting difficult shots. Companions could observe the shots; no marker or route guidance effect exists.';
  projectile.expected_output.interpretation.summary='Careful sight-line selection distinguishes an ordinary projectile practice history.';
  projectile.behavior_summary.signals=[projectile.expected_output.interpretation.summary];projectile.behavior_summary.identity_hypothesis=projectile.expected_output.interpretation.summary;
  Object.assign(projectile.expected_output.opportunity,{identity:'The Measured Shot',diegetic_reason:'Careful sight-line selection shapes the name. The destination executes only the supplied ordinary projectile, with no marking or navigation effect.'});
  Object.assign(projectile.expected_output.naming,{selected_name:'The Measured Shot',naming_reason:projectile.expected_output.opportunity.diegetic_reason,motifs:['careful sight line']});
  Object.assign(projectile.expected_output.naming.semantic_kernel,{identity:projectile.expected_output.interpretation.summary,method:'selecting clear lines of sight',motif:'careful sight line',consequence:projectile.expected_output.opportunity.diegetic_reason});
  alternatives(projectile,['Sight Between Branches','A Careful Angle']);
  const early=by('s45');
  early.player_evidence[0].note='Across unrelated encounters, chose ordinary ranged attacks early while companions withdrew, instead of pursuing finishing shots. These attacks have no interrupt, stun, stagger or ally buff effect.';
  early.expected_output.interpretation.summary='Protective attack timing distinguishes this ordinary projectile history without granting interruption.';
  early.behavior_summary.signals=[early.expected_output.interpretation.summary];early.behavior_summary.identity_hypothesis=early.expected_output.interpretation.summary;
  early.expected_output.opportunity.diegetic_reason='Early protective timing shapes a different name for the identical ordinary projectile. It does not stop attacks or grant control effects.';
  Object.assign(early.expected_output.naming,{naming_reason:early.expected_output.opportunity.diegetic_reason,motifs:['early protective timing']});
  Object.assign(early.expected_output.naming.semantic_kernel,{identity:early.expected_output.interpretation.summary,method:'early ordinary ranged attack',motif:'early protective timing',consequence:early.expected_output.opportunity.diegetic_reason});
  alternatives(early,['An Early Shot','A Moment for Another']);
  for(const key of ['s44','s45'])by(key).eval_labels.unsupported_name_terms=['interrupt','stun','teleport','immortal','route marker'];
  // note: The NPC naming brief is presentation work, not player behavioral evidence or an adaptive reward.
  const epithet=by('s46');epithet.expected_output.decision='none';epithet.expected_output.opportunity=null;epithet.eval_labels.decision='none';epithet.eval_labels.rarity=null;
  epithet.player_evidence=[];epithet.player_state.session_count=0;epithet.behavior_summary.signals=[];epithet.rarity_constraints.eligible_evidence_ids=[];
  epithet.rarity_constraints.eligibility_validated=false;epithet.rarity_constraints.ceiling='none';epithet.expected_output.interpretation.supporting_evidence_ids=[];
  fact(epithet,'mara_naming_role','Mara is the named ferry clerk. Fordfolk use plain occupational epithets; this unaccepted epithet is a presentation candidate only.',[NPC,CULTURE]);
  epithet.expected_output.naming.semantic_kernel.context='Supplied Mara ferry-clerk role and Fordfolk occupational epithet convention.';

  const contexts={
    c03:{text:'The supplied ferry shelter has damaged roof frames. Its named caretaker knows the approved repair job, which accepts gathered fallen wood. The player has not completed this job.',entity:'ferry_shelter',contacts:['shelter_caretaker'],objective:'Deliver the already specified fallen-wood repair material to the ferry shelter; completion and quantity stay with the mock quest authority.',hook:'After a validated repair commit, the supplied shelter caretaker may offer the already recorded maintenance conversation known to that caretaker.'},
    c05:{text:'Local companions witnessed the supplied protective choices. A new local epithet slot is approved; it grants no interception, shield effect or NPC omniscience.'},
    c06:{text:'Mara witnessed the recorded cargo returns and knows them. The supplied conversation topic is entrusted cargo; it creates no inventory transfer or quest completion.',knowledge:true},
    c09:{text:'The supplied bank approach exists and is accessible. The canonical discovery ledger verifies this player is its first discoverer. An unaccepted survey objective is approved for that approach.',entity:'unmarked_bank_approach',objective:'Visit the supplied bank approach and submit its survey record; first-discovery truth is already provided by the mock ledger.'},
    c14:{text:'The supplied ferry tool rack contains worn ordinary handles. Its approved maintenance job requests handle repairs without weapon upgrades or new item powers.',entity:'ferry_tool_rack',objective:'Return the already specified repaired tool handles to the supplied rack; repair verification remains deterministic.'},
    c15:{text:'Mara directly witnessed the listed voluntary restitution and knows it. A bounded local relationship conversation about restitution is approved; it grants no universal admiration.',knowledge:true},
    c16:{text:'The supplied hazardous approach is an existing fixed route. Its approved survey job records known hazards; no terrain, route location or navigation effect can change.',entity:'hazardous_approach',objective:'Visit the supplied approach and return its specified hazard record; traversal and completion remain deterministic.'},
    c17:{text:'This approved skill presentation slot binds exactly the existing ordinary melee swing. The observed protective timing grants no stagger, forced opening, ally buff, damage change or control effect.'},
    c18:{text:'The accepted Bank Listener title is personalized and belongs to River Watch. Local residents witnessed and used the recorded warnings. A rare presentation-title evolution is approved without changing powers.'},
    c20:{text:'The supplied Far Bank settlement exists and remains reachable after the recorded flood. Its living records contact knows the supplied flood entries and approved follow-up review. The accepted unique survey quest and its player choices are immutable.',entity:'far_bank_settlement',contacts:['far_bank_records_contact'],objective:'Bring the existing survey record to the supplied Far Bank records contact and review the recorded flood route; no new flood, settlement or reward is generated.',hook:'After deterministic acceptance, the supplied Far Bank records contact may discuss the already supplied annual records review known to that contact. This is an opportunity hook, not a completed event.'},
    s44:{text:'Two ordinary practice posts already exist beside Reed Ford. The only skill executor is an ordinary projectile; the posts and shots do not create route markers or navigation effects.',entity:'practice_posts'},
    s45:{text:'Only the ordinary projectile executor is approved. Companions witnessed earlier attack timing during withdrawal. No interruption, stun, stagger, damage change or ally buff is available.'},
    s47:{text:'The supplied unaccepted settlement name slot is fixed beside Reed Ford. The ferry ledger is an established local fact. Naming changes no geography or accepted name.'},
    s48:{text:'The supplied unnamed civic group maintains ferry records and repairs. This presentation name creates no faction, conquest, reputation power or history.'},
    s49:{text:'The supplied ordinary repaired lantern was used in the recorded night surveys and returned to the existing ferry shelter. Its unaccepted presentation-name slot grants no light spell or item power.'},
    s50:{text:'The canonical ledger supplies twelve seasons of civic service across forty-eight sessions, a verified first bank discovery, and an accepted unique survey chain. The recorded flood event is historical. The approved continuation reviews already supplied flood records; it does not create an event or deity.',entity:'flood_records_archive',objective:'Visit the existing flood-records archive and compare the accepted survey pages with the supplied historical flood entries; any consequences require deterministic commit.',hook:'The archive already records a later civic review meeting. After accepted continuation, a local invitation may be considered; no new world event is authorized.'}
  };
  for(const [key,context]of Object.entries(contexts)){
    const record=by(key),entities=context.entity?[entity(context.entity)]:[];
    if(context.entity)record.expected_output.opportunity.canonical_entity_ids=[FORD,entities[0].id];
    ground(record,{...context,entities});
  }
  // note: Existing item/faction/site slots stay explicit; their identities were never accepted immutable content.
  for(const key of ['s47','s48','s49']){const slot=by(key).known_entities.find(x=>['unnamed_bank','ferry_workers','returned_lantern'].some(s=>x.id===id(`entity:${s}`)));
    const dest=by(key).world_context.destinations[0];dest.canonical_entity_ids=[slot.id,FORD];by(key).expected_output.opportunity.canonical_entity_ids=[slot.id,FORD];}
  const additions=trainingAdditions(make);rows.push(...additions);return rows;
}

function trainingAdditions(make) {
  const specs=[
    ['t53','witnessed_instrument_return','NPC_WORLD_REACTION','Returned the observatory lens case once. The keeper directly witnessed the return.','A local acknowledgment needs only the supplied witnessed fact.','none',{domain:'social',counts:[1,1,0,1,1,1]}],
    ['t54','ordinary_apothecary_names','NPC_NAME_GENERATION','A supplied ordinary adult apothecary naming brief carries no adaptive reward.','Name the supplied ordinary character without status inflation.','none',{domain:'social',counts:[1,1,0,1,1,1]}],
    ['t55','soil_rotation_experiments','BEHAVIOR_INTERPRETATION','Compared two supplied soil treatments in two visits and preserved one useful failed result.','Careful soil experimentation is emerging, with insufficient horizon for an opportunity.','incubate',{domain:'crafting',counts:[2,1,1,2,2,2]}],
    ['t56','shared_ration_uncertainty','ABSTAIN_INCUBATE','Alternated generous food sharing with keeping supplies during unrelated shortages; the constraints on each choice remain unclear.','Food allocation may be meaningful but the current evidence does not resolve a coherent identity.','incubate',{domain:'social',contradictions:['allocation context unresolved'],counts:[6,4,2,3,3,4]}],
    ['t57','reclaimed_kiln_fuel','OPPORTUNITY_PROPOSAL','Across six visits used supplied reclaimed fuel for ordinary kiln jobs, tracked failed batches and preferred conserving new timber over faster firing.','Resource-conscious kiln work supports a modest maintenance opportunity.','propose',{domain:'crafting',template:'quest',kind:'quest',category:'quest',name:'Ash Left for Tomorrow',reason:'The supplied kiln-maintenance objective follows the recorded fuel-conservation method.',counts:[18,14,4,6,4,5],motif:'reclaimed kiln fuel'}],
    ['t58','borrowed_seed_measure','IDENTITY_GENERATION','Across five planting visits returned a borrowed ordinary seed measure cleaned and repaired instead of retaining a more efficient tool.','A borrowed measuring item can carry a modest memory of careful returns.','propose',{domain:'crafting',kind:'identity',category:'item',name:'The Spare Measure',reason:'The supplied ordinary seed measure receives only an unaccepted presentation-name candidate, with no quantity or yield power.',counts:[10,10,0,5,3,4],motif:'borrowed measure'}],
    ['t59','patient_meal_service','OPPORTUNITY_PROPOSAL','Across fifteen diverse service visits delayed personal travel to prepare ordinary meals for people with different needs, including repeated unprofitable substitutions.','Unusual patient hospitality is sustained across unrelated recipients.','propose',{domain:'social',rarity:'rare',name:'The Unhurried Table',reason:'The approved local epithet remembers patient meal service; no healing, resource or universal reputation power follows.',counts:[42,38,4,15,9,16],motif:'patient hospitality'}],
    ['t60','reserve_seed_custody','OPPORTUNITY_CHAIN','After accepting a modest seed-custody role, maintained the promised records for eleven planting visits and voluntarily offered the reserve for an already recorded lean year.','Accepted custody deepens into accountable community provision.','propose',{domain:'crafting',rarity:'rare',template:'quest',kind:'quest',category:'quest',name:'Seeds Kept for the Lean Year',reason:'The supplied reserve-ledger review continues the accepted promise without creating supplies or harvest outcomes.',counts:[26,24,2,11,6,8],motif:'seed custody'}],
    ['t61','kiln_cooling_responsibility','IDENTITY_GENERATION','After accepting Kiln Hand, repeatedly stayed for ordinary cooling checks across thirteen varied jobs, helping new workers document failures instead of claiming faster results.','An accepted workshop identity deepens into responsibility for careful handover.','propose',{domain:'crafting',rarity:'rare',name:'Keeper of the Cooling Hour',reason:'A presentation-title evolution follows the accepted cooling-care history; kiln timing and mechanics stay unchanged.',counts:[31,27,4,13,7,9],motif:'careful cooling'}],
    ['t62','livestock_trade_restraint','OPPORTUNITY_PROPOSAL','Across fourteen unrelated trade visits disclosed ordinary livestock limitations at a personal cost and refused profitable misrepresentation even when no buyer could verify the claim.','Costly honest disclosure supports a rare local trust opening.','propose',{domain:'social',rarity:'rare',template:'conversation',kind:'relationship',category:'opportunity',name:'The Price Said Plainly',reason:'The informed local buyer may offer the supplied trust conversation; prices and faction attitudes remain deterministic.',counts:[33,30,3,14,8,12],motif:'honest disclosure'}],
    ['t63','observatory_margin_records','UNIQUE_OPPORTUNITY','Across sixteen observations kept faint-star omissions and instrument failures in ordinary margins, refusing impressive claims that the supplied instrument could not support.','Unusually careful archival observation supports a rare records opportunity.','propose',{domain:'exploration',rarity:'rare',template:'quest',kind:'quest',category:'quest',name:'What the Margin Keeps',reason:'The supplied observatory archive objective uses honest missing observations, with no new celestial entity or magical sight.',counts:[39,29,10,16,8,11],motif:'honest margins'}],
    ['t64','clockwork_history_intersection','UNIQUE_OPPORTUNITY','Across twenty-four maintenance visits preserved the public clock records, gave up profitable shortcuts, and reconciled an accepted repair promise with an independently verified historical ledger discrepancy.','An exceptional intersection of careful clock care, accepted promise and canonical history supports a unique opening.','propose',{domain:'crafting',rarity:'unique',template:'quest',kind:'quest',category:'quest',name:'The Hour That Stayed',reason:'The supplied municipal archive examination follows the validated historical discrepancy and existing promise. It changes no time or clock mechanics.',counts:[64,58,6,24,12,18],motif:'preserved clock history',hooks:true}]
  ];
  const rows=specs.map(([key,family,task,description,summary,decision,options])=>initialize(make(key,family,task,description,summary,decision,options))),by=key=>rows.find(r=>r.id===id(`case:${key}`));
  const reaction=by('t53'),keeper=entity('observatory_keeper','npc','alive'),witness=fact(reaction,'observatory_return','The observatory keeper directly witnessed the returned lens case. The keeper knows this event only.',[keeper.id]);
  keeper.knowledge_fact_ids=[witness];reaction.known_entities.push(keeper);reaction.expected_output.interpretation.supporting_evidence_ids=[];
  reaction.expected_output.response={npc_entity_id:keeper.id,knowledge_fact_ids:[witness],text:'The lens case is back. Thank you for returning it.'};
  const npc=by('t54');npc.player_evidence=[];npc.behavior_summary.signals=[];npc.player_state.session_count=0;npc.rarity_constraints.eligible_evidence_ids=[];npc.expected_output.interpretation.supporting_evidence_ids=[];
  npc.naming_context={species_id:null,people_id:null,culture_id:CULTURE,region_id:FORD,social_role:'ordinary apothecary',age_band:'adult',pronoun_context:'they/them',
    local_naming_examples:['Mara Venn','Ilen Pell'],phonetic_tendencies:['short given and family names'],common_roots:[],honorific_rules:['no professional honorific'],allowed_compounds:[],taboo_vocabulary:[],
    applicable_name_parts:['given_name','family_name'],forbidden_existing_names:['Mara Venn','Ilen Pell'],existing_identity_immutable:false};
  npc.expected_output.naming={category:'npc',selected_name:'Dena Moss',semantic_kernel:{identity:'ordinary apothecary',method:'supplied naming brief',motif:'plain local name',consequence:'presentation only',context:'supplied Fordfolk examples',tone:'ordinary'},
    candidate_families:['short local given and family'],candidates:['Dena Moss'],parent_identity_id:null,naming_lineage_id:null,evolution_reason:'',behavioral_delta:'',mechanical_delta:'',culture_id:CULTURE,
    naming_reason:'A plain name follows the supplied local brief without an honorific or status claim.',presentation_only:true,motifs:['plain name'],accepted:false,
    npc_parts:{given_name:'Dena',family_name:'Moss',clan_name:null,honorific:null,pronunciation_hint:null}};
  const contexts={
    t57:{entity:'kiln_maintenance_slot',text:'The existing kiln has an approved maintenance-log delivery job for reclaimed-fuel batches. Fuel quantities, firing and results stay fixed.',objective:'Return the specified existing batch log to the kiln-maintenance slot.'},
    t58:{entity:'ordinary_seed_measure',kind:'item',text:'The ordinary seed measure exists, was borrowed for the recorded visits and has an unaccepted personal-name slot. Its quantity and yield mechanics cannot change.'},
    t59:{text:'Local recipients witnessed the supplied meal-service history. A rare local epithet slot is approved without meal, healing or reputation effects.'},
    t60:{entity:'seed_reserve_ledger',text:'The seed reserve and lean-year entries already exist. The accepted personalized seed-custody role is immutable. A follow-up review of its existing allocation ledger is approved.',objective:'Review the existing seed-reserve allocation ledger with its supplied custodian; neither quantities nor harvest outcomes are generated.'},
    t61:{text:'Kiln Hand is an accepted personalized title. Its supplied cooling-care lineage may evolve to a rare presentation title; no timing, heat or crafting effect changes.'},
    t62:{text:'Mara directly witnessed the listed trade disclosures and knows them. The approved local trust conversation concerns those disclosed limitations only; it changes no price.',knowledge:true},
    t63:{entity:'observatory_archive',text:'The observatory and its instrument-limit records already exist. An approved archive-review objective accepts recorded omissions and failures without discovering new entities.',objective:'Visit the supplied observatory archive and compare the existing observation margins with the supplied instrument-limit record.'},
    t64:{entity:'municipal_clock_archive',contacts:['municipal_archivist'],text:'The public clock and its municipal archive exist. The living municipal archivist knows the canonical ledger discrepancy and the approved archive examination. The ledger verifies the accepted personalized maintenance promise; unique reservation is approved. No time manipulation or new event exists.',objective:'Examine the specified historical ledger page in the existing municipal clock archive with the supplied archivist; any identity or consequence requires a deterministic commit.',hook:'The supplied municipal archivist knows the authorized later review topic for this ledger discrepancy. An invitation may follow accepted examination; no new event or clock power is created.'}
  };
  for(const[key,context]of Object.entries(contexts)){
    const record=by(key),entities=context.entity?[entity(context.entity,context.kind??'location')]:[];
    if(context.entity)record.expected_output.opportunity.canonical_entity_ids=[FORD,entities[0].id];
    ground(record,{...context,entities});
    if(['rare','unique'].includes(record.eval_labels.rarity))alternatives(record,[`${record.expected_output.naming.selected_name} Remembered`]);
  }
  alternatives(by('t59'),['A Seat Before the Road','Meals Before Miles']);alternatives(by('t60'),['The Reserve Remembered','What Was Kept for Others']);
  alternatives(by('t61'),['After the Fire','The Patient Handover']);alternatives(by('t62'),['No Hidden Price','What the Buyer Was Told']);
  alternatives(by('t63'),['A Record of What Was Missing','The Honest Margin']);alternatives(by('t64'),['The Unshortened Hour','A Promise in the Clock Ledger']);
  lineage(by('t60'),{parentName:'Seed Custodian',lineageName:'Seed Reserve Care',category:'quest',delta:'Keeping the accepted reserve promise now supports review of a recorded lean-year allocation.'});
  lineage(by('t61'),{parentName:'Kiln Hand',lineageName:'Cooling Care',delta:'Ordinary kiln work has deepened into careful handover and shared failure records.'});
  lineage(by('t64'),{parentName:'Clock Caretaker',lineageName:'Municipal Clock Care',category:'quest',delta:'The accepted maintenance promise now intersects the independently supplied historical ledger discrepancy.'});
  return rows;
}

export function groundDevelopment(rows) {
  rows.forEach(initialize);
  for(const record of rows)if(record.expected_output.opportunity)ground(record,{text:'A local unaccepted presentation-title slot is approved for the supplied record-care history. It grants no mechanical effect.'});
  return rows;
}
