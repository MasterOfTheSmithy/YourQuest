# CPU artifact verification — October 1, 2026

The v0.2.0 repair/consolidation workflow passed at 17:14 UTC using Node v24.19.0. This receipt covers isolated tooling and documentation. Detailed step statuses and hashes are in [artifact-verification.json](../../Tools/AdaptiveContentLoRA/reports/artifact-verification.json).

| Check | Result |
|---|---|
| `node Tools/AdaptiveContentLoRA/run-offline.mjs` | PASS; builds/checks owned artifacts, runs tests and guidance, prepares local prompts and verifies refusal guards |
| Development regressions | PASS; 25 tests, including sessions/history, bound facts/objectives, contact knowledge, hook availability, accepted rarity ancestry and all-candidate effect implications |
| Complete candidate validation | PASS; 64 original pending candidates, all eight main tasks plus NPC naming, C01–C25, exact 29+31 source mapping |
| Train coverage/distribution | PASS; all nine task tags, 42 candidates, within requested initial bands |
| Existing family membership | PASS; all 52 previous record IDs retain their family and split; 12 new train-only families appended |
| Deduplication | No exact projected-input duplicates or lexical near duplicates; semantic duplication still requires human review |
| Repository guidance | PASS; no compilation/runtime certification |
| Local teacher prompts | Prepared for train families only; teacher invocations zero |
| Training export and paired comparison | Expected refusal; mock/runtime readiness and missing actual paired metadata remain blockers |
| JSON/JSONL parsing and whitespace | PASS; 51 public artifact files inspected; private local review/prediction files excluded |
| Mutation boundary | Only this task's `Tools/AdaptiveContentLoRA` and `Docs/AdaptiveContentLoRA` artifacts written; existing user/runtime/G08 work preserved |

Current dataset canonical-object SHA-256: `d3a8c60b5252ab7c73eb487cf38956e15479f363a054a08106f34c23d5bcc65d`.

Current family-seal canonical-object SHA-256: `ccb7309cdce715e93e12eb70ab7a8e56e370b036693e4105b18b0b4404ab208a`.

| Train outcome | Count | Share |
|---|---:|---:|
| None | 16 | 38.1% |
| Incubate | 10 | 23.8% |
| Personalized | 10 | 23.8% |
| Rare | 5 | 11.9% |
| Unique | 1 | 2.4% |
| Legendary chain | 0 | 0% |

Public split counts are train 42, validation four, adversarial eight, regression five and review-only five. The exceptional legendary candidate stays review-only. All 64 candidates remain pending rights/human review; training-eligible count is zero. The [19-item readable pack](../../Tools/AdaptiveContentLoRA/review/REVIEW_PACK.md) and hash-bound form are review materials, not an approval receipt.

c02 now records exactly two sessions and two tree types. c18 evolves a personalized parent to a rare child. Every positive proposal has supplied destination facts and permitted effects; every quest has an input-owned mock objective. Continuations use supplied hook facts, and named contacts have fixture IDs, living status and explicit knowledge. s44/s45 use identical ordinary projectile mechanics: careful sight lines versus protective early timing, with no route-marking or interruption effects in their names or candidates. Pure NPC naming no longer masquerades as adaptive player evidence.

The v0.1.0 dataset remains unchanged for historical fingerprint and membership checks: `5f54b5ba9f521ca9526e8f7546a0820667ea19302d5fe5e3e241485dbef1d39c`. Its old 52-record/17-test receipt and old seal fingerprint `be13ff32eedff22072055f99ebf66f8b3653e7fdef0f0953254269ca26ac3a4d` are historical, superseded evidence. Do not train on the archived targets. The current checker verifies the archive fingerprint and compares membership before rebuilding.

Actual independent hidden inputs remain unauthored: only 12 reserved family IDs exist. Baseline and adapter reports remain NOT_RUN with null quality metrics. ID/fact binding cannot prove prose factuality, behavioral merit, memorability or semantic distinctiveness. Those judgments, full telemetry and larger family samples remain necessary.

No inference, training, adapter creation, model/weight download, installation, paid teacher call, Unity build/test, runtime integration, save migration or GPU workload occurred. Source safetensors/tokenizer provenance, runnable isolated training capacity, G03/G09/G15/G16 production contracts and final A–F persistence/resource demonstrations remain blocked or unverified. The earlier 7,152 MiB free-VRAM observation is not a training benchmark or a renewed capacity guarantee.

Recommendation remains **REVISE / NOT YET TESTABLE; keep the deployed base model**. [execution-plan.json](../../Tools/AdaptiveContentLoRA/execution-plan.json) orders the remaining gates and distinguishes the CPU workflow executable now from model stages that are blocked.
