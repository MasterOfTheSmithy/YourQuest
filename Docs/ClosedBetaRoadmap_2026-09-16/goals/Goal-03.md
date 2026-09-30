# /goal — Harden the existing LLM scheduler and proposal boundary

## 1. GOAL
Make model work bounded, cancellable, observable, and incapable of committing stale or invalid proposals.

## 2. BINDING EXECUTION CONTRACT
This full specification governs execution; the short goal is only its summary. Certify generic orchestration and representative clients, not future domain implementations.

## 3. PURPOSE
Let gameplay continue through slow, failed or unavailable inference while preserving generated content as accepted save records.

## 4. PREREQUISITES
G02 profile/world identity, state revision, lifecycle and commit interfaces.

## 5. SCOPE
LLMClient/YQLlmRequest lifecycle, categories, fairness, startup exclusivity, bounded prompt/response envelopes, generic proposal validation and terminal outcomes.

## 6. OUT OF SCOPE / DEFERRED
Terrain/geography G05–07; character normalization semantics G10; quests/dialogue knowledge G12; social law G14; behavioral eligibility G15; adaptive content G16. Expose validators for these domains without claiming their correctness.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna Max/Extra High for bounded queue debugging; Sol High only for lifecycle/state race decisions. Luna High subagent may build scheduler fault fixtures.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Strengthen the existing queue. Every admitted request ends exactly once: accepted response, invalid response, failed, cancelled, superseded or evicted. Model output is data; it cannot select arbitrary assets, execute instructions or mutate state directly.

## 9. REQUIRED WORK
1. Trace actual request callers and category/priority ownership. Fix background eviction without terminal callback; bound queue size, retries, repair attempts, response size, context and deadlines. Prevent starvation; record active-request latency separately from queued latency.
2. Bind requests to profile, world, generation epoch, owner and relevant state revision; invalidate on profile switch/unload/shutdown. Revalidate at application, including requests that completed after cancellation.
3. Keep startup exclusivity bounded and release it on all terminal paths. Optional generation must never gate ground, collision, traversal or reconstruction; no blocking wait on the render/input thread.
4. Standardize parse→schema→domain validator→normalization→curation→commit; failed validation cannot partially mutate records. Persist source, prompt/generation hash, accepted normalized payload and version. Reuse accepted content after reload.
5. Delimit untrusted origin/dialogue text, bound history, exclude private NPC facts by provider interface, and prohibit scaffold/debug internals from becoming accepted lore. Distinguish minimal offline fallback from successful normal model generation.
6. Add queue depth, age, category, latency, cancellation/eviction/failure/malformed counts and application cost diagnostics. Avoid full private transcripts in default logs.

7. Bounded audit follow-up (A03): bind proposals to the relevant entity/candidate/evidence dependencies, not every unrelated player/world change. Preserve mandatory profile/world/epoch/owner cancellation and revalidate mutable preconditions at commit. Routine movement must not invalidate an otherwise valid proposal; a changed referenced target must. Do not simply disable stale-result checks globally.
8. Bounded audit follow-ups (A06, A11): construct compact task-specific projections with explicit required facts, schema, output reservation and tokenizer-aware budgets. Optional history/retrieval may be dropped before required facts. Never silently middle-truncate an objective, ID or schema; fail/defer explicitly if the required contract cannot fit. Keep private NPC knowledge scoped by provider. Give origin/world canonical fields priority over optional presentation without adding another model call by default.
9. Consolidate one opportunity-admission/deadline policy in the existing scheduler/director/progression owners (A12). Preserve domain ownership; avoid independent timers scheduling duplicate semantic work. Transport retries, parser repair and domain repair share observable end-to-end budgets and terminal outcomes. Domain validators still own actual acceptance.
10. Extend existing diagnostics (A15) with actual input/output tokens where available, role/context size, queue versus active latency, load/cache events where exposed, stale/structural/domain rejection, transport retry/model repair/fallback, and validation/application cost. Mark unavailable metrics honestly. Establish the inexpensive baseline harness now; integrated shared-GPU comparisons belong to G19.
11. Runtime architecture policy (A19): retain one local generative model with isolated role prompts and supported schemas, deterministic tools and accepted persistent records. Approximately 4B is a benchmark candidate, not a proven minimum or permanent mandate. Start retrieval with IDs/tags/lexical queries. Extra models, embeddings, adapters, speculative decoding or backend replacement require task-specific comparative evidence before adoption; no agent swarm, parallel IR or model-dependent movement/terrain.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Run deterministic queue tests for priority, fairness, eviction callback, cancellation, timeout, malformed JSON and stale ownership. In production startup and gameplay, disconnect the configured model, switch profiles mid-request and restore service; movement and safe existing world play continue, stale responses commit nothing, queues recover. Separately record a real successful configured-model response; mocks alone do not prove integration.

Add movement-during-inference versus changed-target cases, late cancellation/profile-switch results, oversized and multilingual required-fact prompts, output/schema reservation, dialogue behind background work, fairness and total retry/repair deadline exhaustion. Reproduce the audit sentinel-loss experiment against current source before repair, then verify required content survives or the request fails explicitly. These are bounded G03 follow-ups before their dependent generated-content acceptance; they do not restart G01–G07.

## 11. DELIVERABLES
Scheduler contract, request-source map, generic content commit adapter, telemetry, fault and live-model receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G10/G12/G15/G16 receive safe request/response lifecycle and must supply domain validators. G19 receives repeatable failure injection. No promise of every future content type working yet.

## 13. COMPLETION GATE
Representative origin/dialogue/background clients obey exactly-once terminal outcomes and stale rejection; optional model failure cannot corrupt state or block movement. Domain failures beyond the generic boundary are assigned, not hidden by fallback.
