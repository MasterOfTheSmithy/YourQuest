# Generated-content context

The current LLM path is `LLMClient`/`YQLlmRequest` with request categories, priorities, terminal outcomes, JSON-schema options, retries, and exclusive owners. Domain services such as `YQOriginGenerationService`, `YQGeneratedNpcPlanningService`, dialogue, progression, and world planning submit typed proposals. `YQContentProposalBoundary` validates required properties, normalizes accepted data, and records `YQMutationReceipt` commits against the relevant state revision.

Model text is untrusted. Canonical gameplay uses structured records, stable IDs, validated enums/fields, and persisted state. Optional Goddess voice and other prose are presentation and must not mutate canonical state. Fallbacks must be explicit and observable; they must not silently replace accepted content or bypass ownership.
