# YourQuest adaptive-content LoRA - offline preparation v0.2.0

This is an isolated CPU-only preparation package. It does not load a model, train an adapter, modify Unity, change saves or certify production integration. Keep the configured Qwen3.5-4B base artifact.

Start with [the audit](AUDIT.md), [architecture](ARCHITECTURE.md), [rights policy](RIGHTS.md), [failure taxonomy](FAILURES.md) and [evaluation/review guide](EVALUATION.md). Machine-readable artifacts live in `Tools/AdaptiveContentLoRA`.

Current budget and timing: [the bounded local pilot](BOUNDED_LOCAL_PILOT.md) uses $0, the existing PC, a 50-minute optimization cap and a 110-minute total GPU-experiment cap. It is an unrun feasibility plan with explicit stop rules, not a promise that the full production goal fits in two hours. Preparation, source matching and human review remain prerequisites.

Run from the repository root using the existing Node runtime; no dependency installation is required:

```powershell
node Tools/AdaptiveContentLoRA/run-offline.mjs
```

The entrypoint rebuilds this tool's own schemas, JSONL fixtures, split candidates, readable review pack and explicitly unrun evaluation reports, runs 25 development regressions and guidance checks, prepares local teacher prompts, verifies the export/pair refusal guards, and checks JSON/JSONL parsing and whitespace. It invokes no teacher, model or training process. It refuses to overwrite recorded baseline/adapter results. Dataset and seal hashes identify the snapshot. Tests use the independent `development_only` family.

Read [the 19-item review pack](../../Tools/AdaptiveContentLoRA/review/REVIEW_PACK.md), fill a separate copy of its hash-bound review form, and follow [execution-plan.json](../../Tools/AdaptiveContentLoRA/execution-plan.json) for the blocked model phases. `data/seed.v0.2.0.jsonl` is current. The superseded v0.1.0 file is retained only to verify provenance and unchanged family membership; its targets must not be trained on.

All seed records are original agent-authored examples, **not human-reviewed**. Their provenance marks `pending`, and training export refuses them. All IDs start with `fixture:`. Mock template validity means only that the offline input supplied that template; it does not establish a supported production capability. No real behavioral traces or private text were read.

The 64 pending candidates cover all eight main task families plus NPC naming and C01-C25. All nine tasks are represented in the 42 train candidates: 16 none, 10 incubate, 10 personalized, five rare, one unique and zero legendary-chain. These fit the requested initial proportions; they are not calibrated production rates. Twelve original train families were appended without moving any existing family. A 200-record corpus remains an expansion plan. Hidden families are reserved but **actual hidden inputs are not authored or sealed yet**. No hidden test result has been used for tuning.

`requirements.json` and [TRACEABILITY.md](TRACEABILITY.md) map all 29 main sections and the original 31 naming sections. The parent supplied the original owner appendix, Dot chat, October 1 approximately 15:01 UTC, message `Sentinel_3d3e5191419c8191884be587f20e9b5f`; it is preserved in [NAMING_APPENDIX_SOURCE.md](NAMING_APPENDIX_SOURCE.md) with a source fingerprint. No Jarvis or hidden-store read was performed here. Exact requirement mapping is available; production acceptance remains incomplete as the individual rows state.

| Required deliverable | Current state |
|---|---|
| 1 Runtime audit | `audit.json` and AUDIT.md; upstream weight/tokenizer revision unresolved |
| 2 Training architecture | ARCHITECTURE.md; production blockers explicit |
| 3 Dataset schema | Generated record/output JSON schemas and task projections |
| 4 Rights policy | RIGHTS.md and source/export gates |
| 5 Hand-authored seeds | Original JSONL candidates; representative human review pending |
| 6 Synthetic pipeline | Local teacher-prompt preparation and candidate filter; no teacher called |
| 7 Dedup/filtering | Strict structure, IDs, authority, rarity, exact-input and lexical checks; semantic review required |
| 8 Negative/adversarial dataset | Adversarial family split plus development rejection tests |
| 9 Splits | Public family-separated candidates; hidden families reserved, not completed |
| 10 Training script | Not implemented; blocked by provenance/environment/capacity/export canary |
| 11 Reproducible environment/config | Dependency-free Node artifacts; training config is a NOT_RUN proposal |
| 12 Pilot adapter | Not trained |
| 13 Baseline results | NOT_RUN report with null model metrics |
| 14 Adapter results | NOT_RUN report with null model metrics |
| 15 Failure taxonomy | FAILURES.md |
| 16 Runtime integration config | Audit and proposal only; no runtime mutation |
| 17 Inference harness | Offline projection/scoring/paired-condition scaffolding; no transport/model invocation |
| 18 Regression suite | Development unit tests and separate regression fixtures; Unity/runtime tests not run |
| 19 Provenance | Exact local GGUF/backend/template fingerprints; dataset/seal/schema fingerprints; source weights unresolved |
| 20 Recommendation | REVISE / NOT YET TESTABLE; retain base |

The next useful decisions concern matching source-weight/tokenizer provenance, representative seed review and isolated training resources. Owner-approved G03/G09/G15/G16 work is required before production integration. This package does not start those goals.
