# LLM scope

`LLMClient`/`YQLlmRequest` provide transport and scheduling; `YQContentProposalBoundary` validates and commits structured proposals; domain services own canonical state. Model output is never executable and presentation voice is transient. Preserve request epochs, exclusive startup ownership, JSON schema/normalization, retry/failure outcomes, and profile/world identity checks.
