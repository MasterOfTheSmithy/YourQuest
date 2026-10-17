# Evaluation and review

`preregistration.json` contains proposed quantitative gates recorded before any model run. Parent review and a sufficiently large family-separated evaluation set are required before adoption testing. The small public fixture set cannot establish production false-positive rates or confidence intervals.

The harness does not invoke an LLM. It reads explicitly supplied prediction JSONL, checks the exact projected-input hash, parses the output, applies offline validators and produces machine-readable reports. Baseline and adapter reports start as NOT_RUN with null model metrics. The development fixture-oracle self-check proves harness plumbing only; it is not baseline or adapter quality evidence.

Predictions use this envelope:

```json
{
  "record_id": "fixture:case:example",
  "input_sha256": "hash of canonical projected input",
  "output": {},
  "accepted_by_runtime": false,
  "latency_ms": null,
  "prompt_tokens": null,
  "completion_tokens": null,
  "peak_ram_mib": null,
  "peak_vram_mib": null,
  "repair_count": null
}
```

Replace `output` with actual raw model JSON or its raw string. Do not populate resource fields with estimates. Preserve parse failures and rejected outputs. `accepted_by_runtime` is true only when a real owning runtime acceptance receipt establishes it; fixture validation is not runtime acceptance.

After an authorized run, populate a preserved copy of `evaluation-run.template.json` with actual hashes, hardware, sampling, seed and RECORDED_ACTUAL_MODEL_RUN. Current CLI uses that local template file as run metadata; preserve distinct baseline/adapter metadata and reports before replacing its contents. Invoke `evaluate predictions.jsonl baseline|adapter` and then `pair`. Pairing refuses incomplete coverage or mismatched input, generation, hardware, backend and base-model fingerprints. No ADOPT verdict can come from this scaffolding.

Machine-checkable measures include schema validity, explicit canonical/evidence-reference membership, destination compliance, decisions, rarity labels, raw false-special rates and combat/noncombat slices. Prose factuality is not proven by valid IDs. Human review measures causal grounding, coherence, identity fit, memorability, pronunciation, semantic collisions and motif saturation; these remain null until genuine reviews. Name repetition and retry telemetry remain unimplemented/null in this initial report scaffold and must be added before full adoption evaluation.

Use a larger preregistered family sample; report numerator/denominator counts and family-cluster confidence intervals. Keep counterfactual pairs together. Blind paired human review must demonstrate causal interpretation improvement, rather than preference for ornate prose. Review the proposed near-ceiling rule before hidden evaluation if the baseline already meets absolute targets.

`review/REVIEW_PACK.md`, the equivalent JSONL and the hash-bound review-form template provide 19 representative originals. All 64 candidates remain pending; a representative pack cannot approve the rest. The reviewer can judge:

- c01/c02/c03: ordinary gathering, immature experiment and sustained low-impact gathering.
- c05: protective interception evidence presented as a restrained epithet without invented powers.
- c14: peaceful repair leading to a bounded crafting quest.
- c18/c20: accepted identity evolution and a later consequence preserving lineage.
- s41/s42: witnessed acknowledgment versus private-event ignorance.
- s44/s45: **The Measured Shot** versus **Before the Blow**, identical ordinary projectile mechanics, careful sight-line selection versus protective early timing. Neither grants marking or interruption effects, including its alternative candidates.
- s46: culture/role-grounded NPC epithet.
- s52: dedicated ordinary NPC name and culturally applicable name parts.
- s50: exceptional authorized legendary-chain candidate, review-only.
- t53/t54: train-only witnessed acknowledgment and ordinary NPC naming.
- t60/t61/t64: new train-only rare continuation, rare title ancestry and a unique history intersection.

The repaired c02 has exactly two evidence/history sessions; c18 evolves a personalized parent to a rare child. Repair/survey/follow-up/archive jobs are now explicit supplied facts, entities and mock objectives. NPC acknowledgments use supplied authorized facts. Dataset checks reject missing destination facts, mismatched objectives, unsupported effect tags and changed existing family membership; development tests exercise those failures without tuning model outputs on evaluation cases. Schema acceptance still cannot establish semantic prose truth or human-quality merit.

The review form records record hashes, reviewer/time, rights approval, decision approval and separate naming/grounding scores. Completing the form does not silently edit source flags or approve production mechanics. Apply accepted feedback in a versioned dataset update; never change hidden cases in response to their scores.

`demo-specifications.json` defines A–F. E requires real save/quit/reload with inference disabled, unchanged accepted identity/effects and authorized subsequent NPC response. No offline fixture can substitute for those receipts. Movement/streaming/frame, startup/residency, unrelated LLM-role regression and long-session resource budgets remain required runtime measurements.
