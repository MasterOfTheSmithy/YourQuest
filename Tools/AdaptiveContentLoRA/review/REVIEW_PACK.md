# Pending review pack v0.2.0

Original agent-authored offline candidates. All judgments are pending. Mock destinations are not runtime-certified. This pack contains public review/evaluation cases and must never be sent to a teacher or training job.

Dataset canonical SHA-256: d3a8c60b5252ab7c73eb487cf38956e15479f363a054a08106f34c23d5bcc65d

## fixture:case:c01 — ABSTAIN_INCUBATE

Family: tree_identity; split: train. Expected: **none**.

Evidence: Collected ordinary firewood with an axe while completing common chores. No unusual tradeoff or sustained method. [18 attempts, 18 successes, 0 failures; 1 sessions; 1 contexts, 3 targets]

Interpretation: Ordinary harvesting does not establish a distinctive identity.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c02 — BEHAVIOR_INTERPRETATION

Family: tree_identity; split: train. Expected: **incubate**.

Evidence: Compared an axe, a stone and empty hands on two tree types; only two short outings are recorded. [8 attempts, 6 successes, 2 failures; 2 sessions; 2 contexts, 2 targets]

Interpretation: Meaningful tool experimentation is emerging but remains immature.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c03 — OPPORTUNITY_PROPOSAL

Family: tree_identity; split: train. Expected: **propose / personalized**.

Evidence: Across eight outings deliberately used fallen limbs and hand gathering instead of faster tools, left living trunks intact and repaired shelter frames from the gathered wood. [36 attempts, 30 successes, 6 failures; 8 sessions; 5 contexts, 7 targets]

Interpretation: A sustained low-impact hand-gathering method connects gathering to shelter care.

Name: **Branches for the Waiting Roof**. Candidates: Branches for the Waiting Roof.

Proposal: The ferry shelter repair uses the same gathered materials and restraint already demonstrated.
Template: fixture:template:quest; destination: fixture:destination:c03; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:c03_destination: The supplied ferry shelter has damaged roof frames. Its named caretaker knows the approved repair job, which accepts gathered fallen wood. The player has not completed this job.
- Supplied fixture:fact:c03_hook: After a validated repair commit, the supplied shelter caretaker may offer the already recorded maintenance conversation known to that caretaker.
- Supplied fixture:objective:c03: Deliver the already specified fallen-wood repair material to the ferry shelter; completion and quantity stay with the mock quest authority.
- Hook fixture:hook:c03_continuation: After a validated repair commit, the supplied shelter caretaker may offer the already recorded maintenance conversation known to that caretaker.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c05 — OPPORTUNITY_PROPOSAL

Family: guarded_execution; split: validation. Expected: **propose / rare**.

Evidence: Across unrelated fights abandoned personal finishing attacks to cover injured companions; precise protective timing succeeded despite sacrificing individual victories. [40 attempts, 35 successes, 5 failures; 9 sessions; 7 contexts, 8 targets]

Interpretation: Protective timing, rather than mere shield use, is unusually sustained.

Name: **The Held Opening**. Candidates: The Held Opening; A Place Behind the Shield; Last Opening Held.

Proposal: Witnessed restraint earns a local epithet; this presentation-only offer grants no interception mechanic.
Template: fixture:template:epithet; destination: fixture:destination:c05; effects: presentation_only.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:c05_destination: Local companions witnessed the supplied protective choices. A new local epithet slot is approved; it grants no interception, shield effect or NPC omniscience.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c14 — OPPORTUNITY_PROPOSAL

Family: quiet_craft; split: train. Expected: **propose / personalized**.

Evidence: Over many repairs salvaged mismatched scrap, fitted durable handles and returned repaired household tools without seeking combat work. [8 attempts, 6 successes, 2 failures; 3 sessions; 2 contexts, 2 targets]

Interpretation: Patient repair and material stewardship form a peaceful crafting identity.

Name: **Handles for Other Hands**. Candidates: Handles for Other Hands.

Proposal: A bounded ferry-tool repair request follows the established care for ordinary tools.
Template: fixture:template:quest; destination: fixture:destination:c14; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:c14_destination: The supplied ferry tool rack contains worn ordinary handles. Its approved maintenance job requests handle repairs without weapon upgrades or new item powers.
- Supplied fixture:objective:c14: Return the already specified repaired tool handles to the supplied rack; repair verification remains deterministic.
- Hook fixture:hook:ford_return: Return to the supplied crossing. This hook does not authorize new content or mechanics.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c18 — OPPORTUNITY_PROPOSAL

Family: title_evolution; split: regression. Expected: **propose / rare**.

Evidence: After accepting the title Bank Listener, repeatedly translated careful river observations into evacuation warnings that residents actually used. [8 attempts, 6 successes, 2 failures; 3 sessions; 2 contexts, 2 targets]

Interpretation: The accepted river-watch identity has deepened into responsibility for others.

Name: **The Bank That Warns**. Candidates: The Bank That Warns; Voice of the Bank; The Listening Bank.
Parent: fixture:identity:c18_parent; lineage: fixture:lineage:river_watch. Accepted river work now supports a later consequence.

Proposal: Evolve the existing presentation title around witnessed warnings, preserving its river lineage.
Template: fixture:template:epithet; destination: fixture:destination:c18; effects: presentation_only.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:c18_destination: The accepted Bank Listener title is personalized and belongs to River Watch. Local residents witnessed and used the recorded warnings. A rare presentation-title evolution is approved without changing powers.

Accepted parent record: Bank Listener; rarity: personalized.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:c20 — OPPORTUNITY_CHAIN

Family: accepted_chain; split: validation. Expected: **propose / unique**.

Evidence: Accepted a unique river-survey quest, kept the promised records and later used them to help an isolated settlement during floods. [8 attempts, 6 successes, 2 failures; 3 sessions; 2 contexts, 2 targets]

Interpretation: The accepted survey chain can now create a civic consequence beyond personal recognition.

Name: **When the Far Bank Calls**. Candidates: When the Far Bank Calls; A Ledger Across the Water; Readers on the Far Bank.
Parent: fixture:identity:c20_parent; lineage: fixture:lineage:river_watch. Accepted river work now supports a later consequence.

Proposal: A supplied follow-up objective extends the accepted bank-survey lineage without changing its origin.
Template: fixture:template:quest; destination: fixture:destination:c20; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:c20_destination: The supplied Far Bank settlement exists and remains reachable after the recorded flood. Its living records contact knows the supplied flood entries and approved follow-up review. The accepted unique survey quest and its player choices are immutable.
- Supplied fixture:fact:c20_hook: After deterministic acceptance, the supplied Far Bank records contact may discuss the already supplied annual records review known to that contact. This is an opportunity hook, not a completed event.
- Supplied fixture:objective:c20: Bring the existing survey record to the supplied Far Bank records contact and review the recorded flood route; no new flood, settlement or reward is generated.
- Hook fixture:hook:c20_continuation: After deterministic acceptance, the supplied Far Bank records contact may discuss the already supplied annual records review known to that contact. This is an opportunity hook, not a completed event.

Accepted parent record: The Bank Nobody Marked; rarity: unique.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s41 — NPC_WORLD_REACTION

Family: authorized_recognition; split: regression. Expected: **none**.

Evidence: Mara witnessed the player return a lost cargo ledger and knows only that local event. [1 attempts, 1 successes, 0 failures; 1 sessions; 1 contexts, 1 targets]

Interpretation: A restrained witnessed acknowledgment is appropriate.

Authorized reaction: “You brought the ledger back. Thank you for returning it.” Knowledge: fixture:fact:witnessed_return.

- Supplied fixture:fact:witnessed_return: Mara directly witnessed the returned ledger.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s42 — NPC_WORLD_REACTION

Family: authorized_recognition; split: regression. Expected: **none**.

Evidence: A distant private rescue is accepted in world history, but Mara has no knowledge of it. [1 attempts, 1 successes, 0 failures; 1 sessions; 1 contexts, 1 targets]

Interpretation: No supplied knowledge supports recognition of an adaptive identity.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s44 — IDENTITY_GENERATION

Family: same_mechanic_distinct_history; split: train. Expected: **propose / personalized**.

Evidence: Across several visits, selected clear lines of sight between branches and aimed ordinary projectiles at already supplied practice posts before attempting difficult shots. Companions could observe the shots; no marker or route guidance effect exists. [8 attempts, 6 successes, 2 failures; 3 sessions; 2 contexts, 2 targets]

Interpretation: Careful sight-line selection distinguishes an ordinary projectile practice history.

Name: **The Measured Shot**. Candidates: The Measured Shot; Sight Between Branches; A Careful Angle.

Proposal: Careful sight-line selection shapes the name. The destination executes only the supplied ordinary projectile, with no marking or navigation effect.
Template: fixture:template:projectile; destination: fixture:destination:s44; effects: projectile.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:s44_destination: Two ordinary practice posts already exist beside Reed Ford. The only skill executor is an ordinary projectile; the posts and shots do not create route markers or navigation effects.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s45 — IDENTITY_GENERATION

Family: same_mechanic_distinct_history; split: train. Expected: **propose / personalized**.

Evidence: Across unrelated encounters, chose ordinary ranged attacks early while companions withdrew, instead of pursuing finishing shots. These attacks have no interrupt, stun, stagger or ally buff effect. [8 attempts, 6 successes, 2 failures; 3 sessions; 2 contexts, 2 targets]

Interpretation: Protective attack timing distinguishes this ordinary projectile history without granting interruption.

Name: **Before the Blow**. Candidates: Before the Blow; An Early Shot; A Moment for Another.

Proposal: Early protective timing shapes a different name for the identical ordinary projectile. It does not stop attacks or grant control effects.
Template: fixture:template:projectile; destination: fixture:destination:s45; effects: projectile.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:s45_destination: Only the ordinary projectile executor is approved. Companions witnessed earlier attack timing during withdrawal. No interruption, stun, stagger, damage change or ally buff is available.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s46 — IDENTITY_GENERATION

Family: culture_names; split: review_only. Expected: **none**.

Evidence: No player-evidence input for this role.

Interpretation: Culture and ordinary civic role support a modest NPC epithet.

Name: **Mara of the Ledger**. Candidates: Mara of the Ledger.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:mara_naming_role: Mara is the named ferry clerk. Fordfolk use plain occupational epithets; this unaccepted epithet is a presentation candidate only.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s50 — OPPORTUNITY_CHAIN

Family: exceptional_legendary_chain; split: review_only. Expected: **propose / legendary_chain**.

Evidence: An accepted unique survey chain, independently verified first discovery, twelve seasons of civic service and a supplied world-event ledger jointly authorize a legendary-chain candidate. [144 attempts, 120 successes, 24 failures; 48 sessions; 18 contexts, 30 targets]

Interpretation: Exceptional accumulated civic history can open an approved world-event continuation.

Name: **The Ledger Beyond the Flood**. Candidates: The Ledger Beyond the Flood; What the Flood Could Not Erase; A River With a Record.
Parent: fixture:identity:s50_parent; lineage: fixture:lineage:river_watch. Accepted river work now supports a later consequence.

Proposal: The approved chain continues through supplied records and future objectives rather than divine power.
Template: fixture:template:quest; destination: fixture:destination:s50; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:s50_destination: The canonical ledger supplies twelve seasons of civic service across forty-eight sessions, a verified first bank discovery, and an accepted unique survey chain. The recorded flood event is historical. The approved continuation reviews already supplied flood records; it does not create an event or deity.
- Supplied fixture:fact:s50_hook: The archive already records a later civic review meeting. After accepted continuation, a local invitation may be considered; no new world event is authorized.
- Supplied fixture:objective:s50: Visit the existing flood-records archive and compare the accepted survey pages with the supplied historical flood entries; any consequences require deterministic commit.
- Hook fixture:hook:s50_continuation: The archive already records a later civic review meeting. After accepted continuation, a local invitation may be considered; no new world event is authorized.

Accepted parent record: The Bank Nobody Marked; rarity: unique.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:s52 — NPC_NAME_GENERATION

Family: culture_names; split: review_only. Expected: **none**.

Evidence: No player-evidence input for this role.

Interpretation: An ordinary local character name follows the supplied naming brief.

Name: **Nera Vale**. Candidates: Nera Vale; Tessa Ren; Lina Berr.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.

NPC parts: {"given_name":"Nera","family_name":"Vale","clan_name":null,"honorific":null,"pronunciation_hint":"NEH-ra VAYL"}. Supplied brief: {"species_id":null,"people_id":null,"culture_id":"fixture:culture:fordfolk","region_id":"fixture:entity:reed_ford","social_role":"ferry repairer","age_band":"adult","pronoun_context":"she/her","local_naming_examples":["Mara Venn","Ilen Pell"],"phonetic_tendencies":["short given name","plain consonant clusters"],"common_roots":[],"honorific_rules":["no occupational honorific for ordinary repairers"],"allowed_compounds":[],"taboo_vocabulary":[],"applicable_name_parts":["given_name","family_name"],"forbidden_existing_names":["Mara Venn","Ilen Pell"],"existing_identity_immutable":false}.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:t53 — NPC_WORLD_REACTION

Family: witnessed_instrument_return; split: train. Expected: **none**.

Evidence: Returned the observatory lens case once. The keeper directly witnessed the return. [1 attempts, 1 successes, 0 failures; 1 sessions; 1 contexts, 1 targets]

Interpretation: A local acknowledgment needs only the supplied witnessed fact.

Authorized reaction: “The lens case is back. Thank you for returning it.” Knowledge: fixture:fact:observatory_return.

- Supplied fixture:fact:observatory_return: The observatory keeper directly witnessed the returned lens case. The keeper knows this event only.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:t54 — NPC_NAME_GENERATION

Family: ordinary_apothecary_names; split: train. Expected: **none**.

Evidence: No player-evidence input for this role.

Interpretation: Name the supplied ordinary character without status inflation.

Name: **Dena Moss**. Candidates: Dena Moss.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.

NPC parts: {"given_name":"Dena","family_name":"Moss","clan_name":null,"honorific":null,"pronunciation_hint":null}. Supplied brief: {"species_id":null,"people_id":null,"culture_id":"fixture:culture:fordfolk","region_id":"fixture:entity:reed_ford","social_role":"ordinary apothecary","age_band":"adult","pronoun_context":"they/them","local_naming_examples":["Mara Venn","Ilen Pell"],"phonetic_tendencies":["short given and family names"],"common_roots":[],"honorific_rules":["no professional honorific"],"allowed_compounds":[],"taboo_vocabulary":[],"applicable_name_parts":["given_name","family_name"],"forbidden_existing_names":["Mara Venn","Ilen Pell"],"existing_identity_immutable":false}.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:t60 — OPPORTUNITY_CHAIN

Family: reserve_seed_custody; split: train. Expected: **propose / rare**.

Evidence: After accepting a modest seed-custody role, maintained the promised records for eleven planting visits and voluntarily offered the reserve for an already recorded lean year. [26 attempts, 24 successes, 2 failures; 11 sessions; 6 contexts, 8 targets]

Interpretation: Accepted custody deepens into accountable community provision.

Name: **Seeds Kept for the Lean Year**. Candidates: Seeds Kept for the Lean Year; The Reserve Remembered; What Was Kept for Others.
Parent: fixture:identity:t60_parent; lineage: fixture:lineage:t60. Keeping the accepted reserve promise now supports review of a recorded lean-year allocation.

Proposal: The supplied reserve-ledger review continues the accepted promise without creating supplies or harvest outcomes.
Template: fixture:template:quest; destination: fixture:destination:t60; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:t60_destination: The seed reserve and lean-year entries already exist. The accepted personalized seed-custody role is immutable. A follow-up review of its existing allocation ledger is approved.
- Supplied fixture:objective:t60: Review the existing seed-reserve allocation ledger with its supplied custodian; neither quantities nor harvest outcomes are generated.

Accepted parent record: Seed Custodian; rarity: personalized.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:t61 — IDENTITY_GENERATION

Family: kiln_cooling_responsibility; split: train. Expected: **propose / rare**.

Evidence: After accepting Kiln Hand, repeatedly stayed for ordinary cooling checks across thirteen varied jobs, helping new workers document failures instead of claiming faster results. [31 attempts, 27 successes, 4 failures; 13 sessions; 7 contexts, 9 targets]

Interpretation: An accepted workshop identity deepens into responsibility for careful handover.

Name: **Keeper of the Cooling Hour**. Candidates: Keeper of the Cooling Hour; After the Fire; The Patient Handover.
Parent: fixture:identity:t61_parent; lineage: fixture:lineage:t61. Ordinary kiln work has deepened into careful handover and shared failure records.

Proposal: A presentation-title evolution follows the accepted cooling-care history; kiln timing and mechanics stay unchanged.
Template: fixture:template:epithet; destination: fixture:destination:t61; effects: presentation_only.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:t61_destination: Kiln Hand is an accepted personalized title. Its supplied cooling-care lineage may evolve to a rare presentation title; no timing, heat or crafting effect changes.

Accepted parent record: Kiln Hand; rarity: personalized.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## fixture:case:t64 — UNIQUE_OPPORTUNITY

Family: clockwork_history_intersection; split: train. Expected: **propose / unique**.

Evidence: Across twenty-four maintenance visits preserved the public clock records, gave up profitable shortcuts, and reconciled an accepted repair promise with an independently verified historical ledger discrepancy. [64 attempts, 58 successes, 6 failures; 24 sessions; 12 contexts, 18 targets]

Interpretation: An exceptional intersection of careful clock care, accepted promise and canonical history supports a unique opening.

Name: **The Hour That Stayed**. Candidates: The Hour That Stayed; The Unshortened Hour; A Promise in the Clock Ledger.
Parent: fixture:identity:t64_parent; lineage: fixture:lineage:t64. The accepted maintenance promise now intersects the independently supplied historical ledger discrepancy.

Proposal: The supplied municipal archive examination follows the validated historical discrepancy and existing promise. It changes no time or clock mechanics.
Template: fixture:template:quest; destination: fixture:destination:t64; effects: existing_objective_records.

- Supplied fixture:fact:ford: Reed Ford is a river crossing. Mara keeps its ferry ledger. These are original offline facts, not production canon.
- Supplied fixture:fact:t64_destination: The public clock and its municipal archive exist. The living municipal archivist knows the canonical ledger discrepancy and the approved archive examination. The ledger verifies the accepted personalized maintenance promise; unique reservation is approved. No time manipulation or new event exists.
- Supplied fixture:fact:t64_hook: The supplied municipal archivist knows the authorized later review topic for this ledger discrepancy. An invitation may follow accepted examination; no new event or clock power is created.
- Supplied fixture:objective:t64: Examine the specified historical ledger page in the existing municipal clock archive with the supplied archivist; any identity or consequence requires a deterministic commit.
- Hook fixture:hook:t64_continuation: The supplied municipal archivist knows the authorized later review topic for this ledger discrepancy. An invitation may follow accepted examination; no new event or clock power is created.

Accepted parent record: Clock Caretaker; rarity: personalized.

Review required: rights, decision, causal grounding, mechanic consistency, rarity, memorable naming, pronunciation and distinct-history fit. Approve, revise or reject with comments; this text is not an approval receipt.

## New train-only families

| Record | Task | Decision / rarity | Family |
|---|---|---|---|
| fixture:case:t53 | NPC_WORLD_REACTION | none | witnessed_instrument_return |
| fixture:case:t54 | NPC_NAME_GENERATION | none | ordinary_apothecary_names |
| fixture:case:t55 | BEHAVIOR_INTERPRETATION | incubate | soil_rotation_experiments |
| fixture:case:t56 | ABSTAIN_INCUBATE | incubate | shared_ration_uncertainty |
| fixture:case:t57 | OPPORTUNITY_PROPOSAL | personalized | reclaimed_kiln_fuel |
| fixture:case:t58 | IDENTITY_GENERATION | personalized | borrowed_seed_measure |
| fixture:case:t59 | OPPORTUNITY_PROPOSAL | rare | patient_meal_service |
| fixture:case:t60 | OPPORTUNITY_CHAIN | rare | reserve_seed_custody |
| fixture:case:t61 | IDENTITY_GENERATION | rare | kiln_cooling_responsibility |
| fixture:case:t62 | OPPORTUNITY_PROPOSAL | rare | livestock_trade_restraint |
| fixture:case:t63 | UNIQUE_OPPORTUNITY | rare | observatory_margin_records |
| fixture:case:t64 | UNIQUE_OPPORTUNITY | unique | clockwork_history_intersection |

All 64 records need human review. The 19-item pack is representative and cannot approve the remaining records. Actual hidden inputs remain unauthored.
