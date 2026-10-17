# G08-R2 D29 — bounded coast allocation ownership

**Diagnostic only; full R2 FAIL. Latest clean authority remains P10 (44 ms input / 53 ms coast). No independent repeat or full matrix PASS; R3 gated.**

## Capture and identity

One ordinary PlaySafe/title/selected-save Continue run, 13:51:07.5304199–13:51:29.1536040 UTC. [Immutable runtime receipt](G08_R2_Speed260_2026-10-01_135129_D29_BoundedStacks_FAIL.md). Unity 6000.3.2f1/DX12/PID28040, Ryzen5800X/RTX5060, viewport1278×637/scale0.79. Same disposable profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted/validated V2 `c1d6ce9d3e06f760`, schema7/topology9/gate2, view512m/owner256. Original input/full normal coast; no history/deep/GPU/motion override, no manual measured movement, zero unfocused frames in both phases.

Only the verifier changed: optional allocation stacks at first coast boundary, bounded to24 intervals or1second, with prior setting restored at window end and Dispose. Production P10 files unchanged. Executing CSharp MVID `77d102cb-e0b7-4daf-9947-c367978cf1d7`, DLL SHA `DC98F42E2BC68C18C371C4F7363D2A4AE4C63B329ED441249F8770445F161635`; verifier SHA `ECD6646584C4A8FCB5CECD1F3BADBDEC06A583C85440DE0633140BEEC428B9B4`; streamer SHA `7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C`. Compile13:48:43.3149537UTC, successful reload and Console0errors/0warnings before arming. Complete source/Editor DLL/status records: `outputs/G08_R2_D29_Armed_20261001.json` and diagnostic patch receipt.

Raw `Logs/G08_R2_FrameTrace_20261001_135116_937.raw` (233165312B), GUID `419eb0c2-30ca-44d9-990f-3388b7783589`,315 metadata frames/314 paired intervals. Stack activation itself consumed sufficient elapsed time that the bounded window stopped after **one interval**, boundary4925→4926. This overhead is part of the diagnostic, not clean performance. No unchanged replay is justified.

## Results and matched ownership

Input70frames median/P95/max34/43/49.591ms. Coast245frames34/46/74.511ms,9.559s/1200.7m, settled0.0 with normal28/34 restored. Input view/turn/ground passed; profiled transition/directions/constants failed. Coast first visibility miss(11,9): content/overlays/appearance/ecology/activation false, collision/traversability true. Altered profiled movement prevents clean capacity/readiness certification; no new exhausted scheduling attempt.

Saved stack and complete owner exports both matched native2114:2115 / Unity4925:4926. `outputs/G08_R2_D29_RemainingFrameCost_20261001.json` and `...StackWindowOwners...json` retain exact boundary clipping. Coast maximum4926 wall74.511ms/native73.241ms, no collection in the interval. Inclusive nested samples must not be summed.

- `YQProductionBaselineDiagnostics.Update`:18.308ms,6044 allocation events/207900B.
- `YQGeneratedWorldRuntimeBuilder.Update`:15.098ms,5978 events/194424B.
- StreamerUpdate6.265ms,261 events/12094B; painter2.344ms includes appearance2.318ms; streamerLate2.024ms. Motor1.979ms; camera-ground scope is below the export's1ms cutoff, so the earlier D28 ground13.161ms/1070708B question remains unresolved by this one interval.
- The sampled stack selection contains44 accepted-blueprint hash stacks:25 under baseline diagnostics,19 under builder. Actual path is `ExpectedStartupSettlementCount → CountInitialTerrainSettlements → GetSettlementAnchor → TryGetPrepared → TryComputeCurrentHash → ComputeContentHashReadOnly → Append/StableHashWriter.Append`, including float/int formatting. The two callback byte totals are disjoint, but not every byte is proved to be formatting. Their profiled duration cannot predict clean improvement.
- One44B allocation resolves to streamer `PhysicalOwnerCapacity` line1454 via `AddPhysicalDemand/RebuildPhysicalDemandSet/UpdateStreamingFrame`. This proves a params-array allocation, not dominance of the whole ground burst. Defer this smaller owner while investigating the larger demonstrated hash boundary.

Main native2115 recorded14443 allocation events/901272B; evenly spaced selection64,60 with call stacks,53 unique selected stacks. This is representative selection, not a complete source allocation census. Thread IDs/capture labels stay literal; unresolved workers are not assigned to an assumed task. No driver fault follows from wait labels.

## Next bounded repair hypothesis

Repeated hashing must retain current acceptance/mutation validation on every lookup. Do not cache a claimed hash, bypass checking, change sorting/content/encoding, or change call frequency to hide work. Test replacing scalar temporary strings with span formatting that emits the **identical canonical character sequence**, preserving round-trip invariant float formatting, invariant integer tokens, current-culture length prefixes and FNV output.

Shortest regression: detached accepted artifact, frozen old hasher reference, edge float/int/culture/string/null/reorder cases, content-change detection, read-only JSON identity, native before/after allocation and actual complete-hash timing in Edit. Compile outside Play; focused clean witness only if parity and measured improvement pass. Clean coast deficit remains3ms on P10; no prediction that this repair alone closes it. Scheduling/reservation repair loop remains exhausted.

Postrun native screenshot confirms correct PlaySafe Editor outside Play, no modal; Console retains four known runtime collider errors rather than being described as compiler errors. Saved analysis requests consumed. No production edit preceded analysis.
