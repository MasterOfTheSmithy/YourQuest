# G08-R2 P18 — full-turn preflight

Status: **INVALID_ROUTE; focused witness not executed; full R2 incomplete**.

## Change and rationale

Only `Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs` changed. D42 demonstrated that the existing short eastward capsule corridor does not cover the turn sequence. P18 computes a conservative rectangle containing every prefix of the existing first eastward boost and eight turns for independent step lengths0–13 m (unchanged260 m/s times the50 ms gate). Existing same-row candidates are sampled at2 m spacing for steep terrain, with a bounded nonrecursive obstacle overlap query. No additional frame, preparation request, movement bypass, teleport, world edit, threshold relaxation or production motor/streamer change.

Physical input obstruction now has a separate gate using actual forward progress, requested integrated motion, capsule-skin tolerance, new side contact and no readiness rejection. It invalidates the route, just as a coast obstruction does. Route version3; earlier route-v2 performance cannot certify it.

540 detached route regressions PASS (`Logs/G08_R2_P18_Route_20261002.route.tsv`), including512 extremal combinations of first-boost/eight-turn lengths at yaw0/37/90 degrees. This proves bounding arithmetic, not a useful terrain corridor. Both source and compilation were checked in idle Edit. Native Computer Use was used only to discover/focus/capture the existing PlaySafe Editor and select the existing Game tab; no driving, resizing, quality change or runtime input through desktop tools. Fresh screenshot showed Edit, zero Console errors, one existing CS0162 warning. Runtime remained marker-driven.

## Runtime result

Normal existing fixture Continue, no binary/history/stacks/deep/GPU/motion instrumentation. Run16:38:42.4132302–16:38:50.4463093 UTC, immutable receipt `Docs/G08_R2_Speed260_2026-10-02_163850_P18_Background_Diagnostic_Receipt.md`. MVID`228deaf2-cb4c-47e5-aabf-c4e3a0232a67`; mainDLL`BA59459156127ABDB078B7F1FC770ABCC135A85B6B36DCB85D312C91742CF44D`. All eight source/DLL hashes matched after return to Edit; no compilation in observed Play (`outputs/G08_R2_P18_ReturnedAudit_20261002.json`).

No candidate passed the conservative full-area preflight. Selected best candidate was zOffset2, sampled peak2 m height step2.569 m, physicalRouteBlockers1. Staging took zero frames and terminated `invalid-route-preflight`. The260 m/s input phase took zero frames. The existing flow still performed its low-speed warmup and a three-frame6 m/s coast (0.395 m, maximum44 ms); those do **not** establish any260 m/s gate. Zero unfocused frames is retained but does not make this a usable timing run. Some legacy rows report PASS for zero input observations; the overall verdict is FAIL/INVALID_ROUTE. Future early rejection must label unrun phases explicitly and skip that remaining warmup/coast.

## Finding and remaining action

P18 stops the known unsafe route from being certified, but has not supplied a usable route. The bounding rectangle includes points outside actual turn paths, so rejecting it does not prove that no safe eight-turn route exists. Its detached geometry PASS must not be promoted to runtime route success.

The next bounded test repair should evaluate the actual eight-leg turn corridors (including frame-duration variation and capsule clearance), preserving the existing input and the runtime physical-obstruction gate. It must avoid treating unrelated terrain anywhere in a large rectangle as a route obstacle. No automatic retry of unchanged P18 is warranted. P16 fixed the coast exit but did not close the complete physical-route requirement; P18 is a second unsuccessful complete-route closure. Preserve the three-attempt limit for this cause.

The last useful clean performance witness remains P17: input43 ms/coast67 ms, direction displacement62/63, no clean focused PASS. D42 identified collection and LLM callback costs but was profiled/unfocused. After a valid final route, remaining work is the50 ms and all readiness/movement gates, independent clean repeat, and affected/missing full-R2 rows. R3 remains gated.
