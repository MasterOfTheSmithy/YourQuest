# G02 — LLM scheduler and proposal boundary receipt

Date: 2026-09-16
Source/build identity: YourQuest working tree, Unity 6000.3.2f1, `YourQuest.slnx` / `Assembly-CSharp`
Evidence level: compile-verified, editor-tool-verified, and PlaySafe runtime-verified for the named disposable `G02 Disposable Scheduler` profile, including disconnect, mid-request profile switch, restore, and real configured-model success.

## Changed or established

- `LLMClient` now stamps requests with profile, world, lifecycle epoch, owner, and player/world revisions; it rejects stale results before application.
- Every admitted request has one typed terminal outcome: accepted response, failed, invalid response, cancelled, superseded, or evicted.
- Queue eviction and shutdown preserve callbacks; active, queued, and retry-delay requests can be cancelled.
- High-priority work is bounded by a configurable burst so background work cannot starve indefinitely.
- Prompt context, response characters, retries, queue depth, exclusive leases, and diagnostic payloads are bounded.
- Completion telemetry exposes category counts, queue/active latency, cancellation, eviction, malformed, timeout, and superseded counts without private transcripts.
- `YQContentProposalBoundary` standardizes parse → schema gate → normalization → curation → provenance → atomic persisted accepted record.
- Accepted records now retain schema version, prompt hash, generation hash, and normalized payload JSON.
- Origin, world-plan, background-lore, dialogue-adjacent tutorial, investor, and world-delta callbacks refuse lifecycle-terminal results instead of applying fallback mutation to a replacement profile.
- Model-facing origin/dialogue evidence is explicitly delimited as untrusted data, while `NpcDialogueAgent.BuildPersonaBlock` exposes only public identity/role facts and excludes session/private knowledge.
- Exclusive-owner release now terminalizes queued owned requests, preventing stranded exclusive work after startup handoff.
- The editor marker and PlaySafe verifier provide a disposable, non-mutating runtime receipt for queue and live-model behavior.

## Request-source map

| Source | Category/priority | Commit/apply boundary |
| --- | --- | --- |
| `YQOriginGenerationService` | OriginGeneration / StartupExclusive | `YQContentProposalBoundary` → `PlayerState.acceptedContent` → origin callback |
| `YQWorldGenerationService` world plan | WorldGeneration / StartupExclusive | `YQContentProposalBoundary` → `WorldState.acceptedContent` → plan application |
| `DialogueThinkService` | Dialogue / PlayerFacing | typed terminal result → bounded dialogue validator/repair → NPC session |
| `LLMThinkCycle` world delta | StructuredState / Background | stale terminal guard → existing deterministic applier/fallback policy |
| `YQWorldGenerationService` lore refresh | WorldGeneration / Background | stale terminal guard → background lore validator |
| `ProgressionThinkCycle` | StructuredState / Background | existing decision applier; no stale fallback mutation |

## Checks

| Check | Status | Expected / actual |
| --- | --- | --- |
| Solution compile | PASS | `dotnet build YourQuest.slnx`; 0 errors, 1 pre-existing CS0162 warning |
| Targeted diff whitespace | PASS | `git diff --check` reported no whitespace errors for the goal-owned files; unrelated pre-existing assets still emit line-ending/trailing-whitespace notices |
| Generic proposal fixture + malformed JSON parser | PASS | Editor pure regression receipt at `Editor.log:156560`; code-fence parsing, schema/curation, atomic provenance, duplicate rejection, malformed proposal, and malformed LLM JSON rejection passed |
| Deterministic queue priority/fairness/eviction/cancellation/timeout | PASS | PlaySafe receipt: `evictions=1`, `cancellations=1`, timeout request `26` classified terminal `Failed`, fairness order `H0,H1,H2,N,H3`, queue drained to zero |
| Exclusive release / stale ownership | PASS | PlaySafe receipt: `stale ownership pass=True`; lifecycle terminal callback was `Superseded` and no proposal commit was attempted |
| PlaySafe startup/title flow | PASS | Existing enabled PlaySafe flow reached `titleFlowComplete=True`, `gameplayRuntimeReady=True`, and the runtime verifier ran at the accepted boundary |
| Real configured-model success | PASS | PlaySafe receipt: request `24`, `outcome=AcceptedResponse`, `success=True`, `textChars=15` |
| Model disconnect + restore with active profile reload | PASS | PlaySafe receipt: disposable profile created/deleted through `YQProfileSaveSystem`; active request `24` superseded during profile switch; disconnected endpoint request `25` ended `Failed`; timeout request `26` classified `Failed`; original profile/service restored; configured request `27` ended `AcceptedResponse` |

## Deferred defects / next gate

- No G02-owned deferred defects remain. An earlier enhanced runtime attempt was interrupted by an unrelated native Unity crash during world reconstruction; the restarted editor completed the same PlaySafe flow and produced the final PASS receipt above.

No future domain validator correctness is claimed for G10/G12/G14/G15/G16; those consumers receive the bounded request/result contract and must own their domain gates.
