# G08-R2 D31 — remaining frame owners on P11

**Diagnostic only. Latest clean P11 FAIL56/61ms; no full focused PASS/repeat/full R2. R3 gated.**

## Capture and conditions

Question: resolve actual native intervals for input/coast maxima and a representative overbudget coast on final P11, separating production work, collections and native/Editor work. One binary-only ordinary PlaySafe/title/Continue capture14:09:45.0414025–14:10:08.1060553UTC. [Immutable receipt](G08_R2_Speed260_2026-10-01_141008_D31_BinaryOnly_Receipt.md). No C# change after P11, deep/stack/history/GPU/motion overrides off, no extra preparation/cache clearing/Editor restart/manual measured movement. Both measured phases0unfocused;53externalPlay observations watcher inactive/no compilation. ReturnedEdit/source/DLL hashes match arming. Actual native screenshot confirms correct idle PlaySafe Editor; requests consumed.

Executing CSharp MVID9e96b945-04ef-47ff-bccf-8284f593d1a7/DLL3B350B3B4EC19B9EFFF672DDB55E1728DBABE2436758FF5BE9CD848141EFFE09; hasherD7F12141E364D99E88A42D0A65305C4CB5B028D7ADAA24FD09F8F2A038C60BD1/verifierF5817C1E90BB5624D6A31AA1E5678CCF20173AC5CB3EBBCF24F75C1CFF01B14F/streamer7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C. Complete hashes/status:outputs/G08_R2_D31_Armed_20261001.json and ...ReturnedEdit.... Same Unity6000.3.2f1/DX12/PID28040/Ryzen5800X/RTX5060/1278×637/scale0.79. Same disposable profile3ccfff4df9a1479884061e51940e4457/worldyq-world-0284d4409a720743c13eb56e/seed4bb221dc/acceptedvalidatedV2c1d6ce9d3e06f760/schema7/topology9/gate2/view512m/owner256.

Raw Logs/G08_R2_FrameTrace_20261001_140954_761.raw,238303131B; GUID0bdded45-aaa8-4ecf-a596-5ca51ff26256. Metadata383frames/382paired intervals. Saved exports matched2each:InputMax2520:2521,CoastMax2745:2746,CoastRepresentativeGC2688:2689. No new capture for extraction. outputs/G08_R2_D31_RemainingFrameCost_20261001.json and ...AllOwners...json retain exact timestamps, full nested paths/thread IDs and disjoint allocation groups.

## Results and interpretation

Input72frames median/P95/max33/42/45.919ms. Coast311frames34/46/56.671ms,10.690s/1435.7m/settled0.9/normal28/34 restored. Ground/inputview/turn checks and zero fallback passed. Profiling altered abrupt transition/directions/constants (boostUnity227.2/wall813.2m/s/directionfalse); this is not clean certification or sustainable capacity evidence.

Coast first required visibility miss(-2,11): collision/content/overlays/ecology true, appearance/activation false, hard age2.693s/collision6.214s/contentStart0.957s/content0.259s, observed snapshot frame4970. Target active painter absent/activationTarget-1; no cell history armed for this frame-cost capture. This snapshot does not prove starvation, exact discarded work or capacity shortage, nor authorize another exhausted scheduling repair. Latest clean P11 had complete coast visibility throughout.

| Matched interval | Actual scope / allocation evidence |
|---|---|
| Input4875/native2521 wall45.919/native47.1705ms |NoGC. StreamUpdate10.687ms includes contentStart4.270ms; baseline diagnostics5.160ms/64608B/808events; Late2.505ms. StreamUpdate12438B/271events. Previous native2520 LightProbeProxyVolume3.007ms/minimap2.792ms overlap the explicit wall boundary.|
| Coast maximum5100/native2746 wall56.671/native56.9093ms |NoGC. Late10.457ms includes **ApplyLifecycleOwnerPass8.905ms**; motor9.311ms; StreamUpdate8.107ms; decorative3.180ms; underwater3.076ms. Motor272210B/3353events, decorative203030B/2773events, appearance37044B/3events. Previous native2745 LightProbeProxyVolume3.964ms/minimap2.461ms overlap the explicit boundary.|
| Representative coast5043/native2689 wall53.636/native53.7471ms |StreamUpdate31.516ms **includes GC17.404ms**; no inclusive adding. Late3.238ms includes ownerPass1.973ms. StreamUpdate81066B/990events, appearance37044B/3events, motor22270B/204events. Collection context is allocator pressure, not proof that one caller's bytes caused it.|

A separate small collection1.074ms at5032/native2678 occurs in a35.532ms interval (flat evidence retained); it is not the maximum. Exact current D31 owners do not establish native ownership of clean P11 frames4854/5137 or earlier D28 frames. Managed allocation zeros remain invalid. Filtered CPU samples below1ms cannot prove zero cost. Nested callbacks, render scopes and waits are not additive; no driver/package fault or lighting-quality change follows from sample names.

## Boundary understood / next bounded question

Current evidence separates a real collection interval from a collection-free maximum. The ownerPass8.905ms is larger than the6.671ms diagnostic coast deficit, but contains shared activation/readiness/teardown work and is not yet an exact redundant-work cause. Source ApplyLifecycle10714–10925 retains demand ordering, full readiness, fair activation, cancellation and bounded object budget. A source read found no justified shortcut. Do not change scheduling, limits, visibility, render settings or content to reduce the observed duration.

The next question is which operation inside that owner pass consumes the cost: repeated readiness/renderer reconciliation, per-owner activation, unpublished-task cancellation, or generated hierarchy teardown. Existing native scopes do not separate those paths sufficiently. Add only bounded attribution around these actual subcalls while outside Play, compile, then one ordinary original-window diagnostic if needed; exit after actual owner/subcall ownership is resolved. No further broad unchanged capture or clean retry. Any production repair needs measured redundant cost, unchanged-result regression and another final-source clean witness. Full R2 remainsFAIL; exhausted scheduler/reservation loop is not reopened.
