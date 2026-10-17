# Requirements traceability

Main M01–M29 map the supplied goal; N01–N31 map the preserved original appendix. A mapped row is not a completion claim. Offline checks cannot certify production behavior.

Naming source: `Sentinel_3d3e5191419c8191884be587f20e9b5f`; SHA-256 `1fc2e0ad474f03e2a52bdb677091058f795cd6c4ac36fccd9fb17afb3c75f7e6`.

| Section | Requirement | Evidence | State |
|---|---|---|---|
| M01 | Production behavior-to-meaning-to-opportunity improvement | `preregistration.json` | PENDING_PAIRED_RUNTIME_PROOF |
| M02 | Actual model/runtime/contracts/hardware audit | `audit.json`, `../../Docs/AdaptiveContentLoRA/AUDIT.md` | SOURCE_AUDITED_UPSTREAM_PROVENANCE_UNRESOLVED |
| M03 | Interpreter/proposal only; deterministic authority | `core.mjs`, `schemas/output.schema.json` | OFFLINE_GATES_IMPLEMENTED_RUNTIME_BLOCKED |
| M04 | Distinct ordinary/emerging/personal/rare/unique/legendary decisions | `catalog.mjs`, `preregistration.json` | FIXTURES_PENDING_REVIEW |
| M05 | Discovered causal identity, not action-count rewards | `catalog.mjs`, `review/review-pack.jsonl` | PENDING_HUMAN_AND_MODEL_COMPARISON |
| M06 | Original/licensed sources only | `../../Docs/AdaptiveContentLoRA/RIGHTS.md`, `schemas/record.schema.json` | POLICY_AND_EXPORT_GATE_IMPLEMENTED |
| M07 | Versioned structured dataset and compact projections | `schemas/record.schema.json`, `schemas/task-projections.json` | OFFLINE_IMPLEMENTED |
| M08 | All eight task families | `catalog.mjs`, `reports/dataset-check.json` | OFFLINE_COVERED_PENDING_REVIEW |
| M09 | Heavy abstention/incubation; restrained rarity distribution | `reports/dataset-manifest.json` | SMALL_SEED_AND_200_RECORD_EXPANSION_PLAN_ONLY |
| M10 | Twelve negative categories | `catalog.mjs`, `tests.mjs`, `../../Docs/AdaptiveContentLoRA/FAILURES.md` | OFFLINE_FIXTURES_AND_DEVELOPMENT_GATES |
| M11 | Diverse causal structures including peaceful paths | `catalog.mjs` | FIXTURES_PENDING_REPRESENTATIVE_REVIEW |
| M12 | Schema IDs bounded explanations explicit deferral | `schema.mjs`, `core.mjs` | OFFLINE_IMPLEMENTED_NOT_RUNTIME_SCHEMA |
| M13 | Seeds, checked synthetic expansion, adversarial and sanitized trace stages | `catalog.mjs`, `cli.mjs`, `../../Docs/AdaptiveContentLoRA/RIGHTS.md` | SEEDS_AND_LOCAL_PIPELINE_NO_TEACHER_OR_TRACES |
| M14 | Family splits and untuned hidden test | `family-seal.json`, `core.mjs` | PUBLIC_FAMILIES_SEALED_HIDDEN_RESERVED_UNAUTHORED |
| M15 | Measured PEFT pilot and reproducible training records | `training-pilot.NOT_RUN.json` | NOT_RUN_BLOCKED |
| M16 | No raw chain-of-thought | `schema.mjs`, `catalog.mjs` | POLICY_AND_SOURCE_FLAG_IMPLEMENTED |
| M17 | Identical held-out base versus adapter evaluation | `cli.mjs`, `reports/baseline.json`, `reports/adapter.json` | HARNESS_IMPLEMENTED_MODELS_NOT_RUN |
| M18 | All 25 specified evaluation cases | `catalog.mjs`, `reports/dataset-check.json` | C01_C25_PUBLIC_FIXTURES_COVERED_NOT_HIDDEN_PROOF |
| M19 | Causal interpretation quality gate | `preregistration.json`, `review/review-form.template.json` | PENDING_BLIND_HUMAN_REVIEW_AND_PAIRED_RESULTS |
| M20 | Explicit rarity inflation metric | `core.mjs`, `preregistration.json` | OFFLINE_METRIC_DEFINED_NOT_MEASURED |
| M21 | Existing G03 scheduler and persisted model-off content | `audit.json`, `demo-specifications.json` | INTEGRATION_BLOCKED_NO_RUNTIME_EDITS |
| M22 | Measured lowest-risk stable adapter loading | `training-pilot.NOT_RUN.json`, `audit.json` | CLI_SUPPORT_FOUND_COMPATIBILITY_NOT_TESTED |
| M23 | Keep base unless comparable evidence justifies change | `audit.json` | PRESERVED_NO_CHALLENGER_INVOCATION |
| M24 | Twenty deliverables with truthful completion state | `../../Docs/AdaptiveContentLoRA/README.md` | CPU_ARTIFACTS_ONLY_ADAPTER_AND_RUNTIME_PENDING |
| M25 | All adoption criteria | `preregistration.json` | NOT_ELIGIBLE |
| M26 | Reject/revise failure conditions | `../../Docs/AdaptiveContentLoRA/FAILURES.md`, `core.mjs` | OFFLINE_TAXONOMY_AND_GATES |
| M27 | No G08 changes/domain redesign/new mechanics/swarm/vectorDB | `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md` | PRESERVED_ISOLATED_TOOLING_ONLY |
| M28 | Six final demonstrations including model-off unique chain | `demo-specifications.json` | SPECIFIED_NOT_RUN |
| M29 | Measured improvement completion gate or retain base | `preregistration.json`, `audit.json` | INCOMPLETE_KEEP_BASE |
| N01 | PURPOSE | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | PARTIAL_CATEGORY_SCHEMA_SEEDS_PENDING_REVIEW |
| N02 | CORE NAMING EQUATION | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | SEMANTIC_KERNEL_AND_REASONS_PENDING_CAUSAL_REVIEW |
| N03 | NAMING AXES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | OFFLINE_AXES_RARITY_SCHEMA |
| N04 | RARITY CONTROLS LANGUAGE | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | CEILING_GATE_STYLE_REVIEW_REQUIRED |
| N05 | CONTENT-TYPE GRAMMARS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | GRAMMARS_PARTIAL_SEEDS_PENDING |
| N06 | TITLES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | PRESENTATION_TITLE_FIXTURES_RUNTIME_EFFECTS_BLOCKED |
| N07 | CLASSES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | CLASS_EVIDENCE_AND_DESTINATION_FIXTURES_PENDING |
| N08 | CLASS EVOLUTIONS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | TITLE_ANCESTRY_FIXTURE_CLASS_EVOLUTION_PENDING |
| N09 | UNIQUE CLASSES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | UNIQUE_CLASS_FIXTURES_AND_RUNTIME_BLOCKED |
| N10 | ITEMS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | ITEM_FIXTURE_EQUIPMENT_ARTIFACT_EXPANSION_PENDING |
| N11 | QUESTS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | QUEST_FIXTURES_PENDING_SITUATION_REVIEW |
| N12 | UNIQUE OPPORTUNITY NAMES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | UNIQUE_QUEST_FIXTURES_PENDING_CONTEXT_REVIEW |
| N13 | LEGENDARY CHAINS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | CHAIN_LINEAGE_FIXTURES_RUNTIME_NOT_RUN |
| N14 | MOTIF MEMORY | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | MOTIF_METADATA_LEXICAL_COUNTS_REVIEW_REQUIRED |
| N15 | CULTURAL NAMING | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | SUPPLIED_CULTURE_CONTEXT_FIXTURE |
| N16 | CHARACTER / NPC NAMES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | DEDICATED_NPC_TASK_PARTS_FIXTURE |
| N17 | PLACE NAMES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | PLACE_FIXTURE_GEOGRAPHIC_SEMANTICS_REVIEW |
| N18 | FACTION NAMES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | FACTION_FIXTURE_ORGANIZATIONAL_REVIEW |
| N19 | ANTI-GENERIC FILTER | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | LEXICAL_FREQUENCY_ADVISORY_NOT_BAN |
| N20 | LENGTH BUDGETS | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | SOFT_BUDGET_WARNINGS |
| N21 | DUPLICATE DETECTION | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | EXACT_NORMALIZED_TOKEN_PHONETIC_CHECKS_REVIEW_REQUIRED |
| N22 | SEMANTIC DUPLICATE DETECTION | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | EXISTING_SEMANTIC_PROJECTION_AND_HUMAN_DEDUP_REQUIRED |
| N23 | PERSISTENCE | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | PERSISTENCE_NOT_RUN |
| N24 | NAME + MECHANIC CONSISTENCY | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | EXPLICIT_TERM_GATE_SEMANTIC_REVIEW_REQUIRED |
| N25 | THREE-STAGE NAME GENERATION | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | MULTI_CANDIDATE_IMPORTANT_NAME_FIXTURES_NO_FABRICATED_SCORES |
| N26 | GENERATED NAME RECORD | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | OFFLINE_NAME_RECORD_PROPOSALS_RUNTIME_PERSISTENCE_BLOCKED |
| N27 | TRAINING EXAMPLES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | SAME_MECHANIC_DIFFERENT_HISTORY_PAIR_PENDING_REVIEW |
| N28 | NAMING EVALUATION | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | NAMING_METRICS_AND_REVIEW_FORM_NOT_EVALUATED |
| N29 | FAILURE EXAMPLES | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | ADVERSARIAL_IMPLICATION_AND_GRANDEUR_REVIEW |
| N30 | TARGET FEEL | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | PENDING_AUTHORED_FOR_HISTORY_HUMAN_REVIEW |
| N31 | FINAL ACCEPTANCE | `schema.mjs`, `core.mjs`, `catalog.mjs`, `../../Docs/AdaptiveContentLoRA/ARCHITECTURE.md`, `preregistration.json` | NOT_ACCEPTED_HELDOUT_RUNTIME_PROOF_PENDING |
