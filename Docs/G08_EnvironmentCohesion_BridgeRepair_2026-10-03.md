# G08 generated bridge collision repair

Status: **bounded repair; EC06 remains NOT VERIFIED**. Prior user-accepted legacy PASS decisions remain accepted. Final affected publication/streaming and world-wide crossing acceptance are separate requirements.

## Reproduction and owning causes

The [before session](../outputs/G08_R3_Physical_20261003/20261004_012208_463_session/Discovery.json) used ordinary PlaySafe/title/Continue, accepted profile `b138c228684b48739bad60f17f6ba5eb`, world `yq-world-300bc1cdba75926d8224ffb9`, seed `610adf18`, artifact `e37706d3ad7f55b6`. The connected walk reached five endpoints forward and returned over the same route using ordinary 1.8 m/s controls. [Raw-ray analysis](../outputs/G08_EnvironmentCohesion_20261003/20261004_bridge_support_ray_before.json) retains trace hashes, issue counts and exact collider observations.

Forward: 32 warning frames, including 13 MissingSupport; reverse: 28 warnings, including 11 MissingSupport. Supplemental downward rays found center support 0.747–0.999 m below forward feet and 0.771–1.946 m below reverse feet, beyond the unchanged 0.65 m query reach. Selected steep footprint hits were BoxColliders named `StoneBridgeWalkSurface`, with slopes reaching 77.318 degrees.

Three owning defects were identified:

1. `YQGeneratedWorldIntegrityValidator` classified every bridge-named renderer as walkable. It re-enabled deliberately disabled visual rail colliders and created colliders on render-only wood/disheveled/repaired deck blocks; its existing exception covered only the stone deck.
2. `YQGeneratedRiverBridge` placed individually tilted hidden support boxes with overlapping end extensions. Their sloped end faces could become the highest ray hit at joints and lift the controller above the intended road/deck plane.
3. The origin crossing builder replaced a higher finished road with a lower water datum at its wet-mask threshold.

The continuous-support replay identified a third cause at `YQGeneratedWorldEnvironment.BuildRiverCrossingsRoutine`: the wet-mask branch replaced the pre-carve finished road height with a lower water datum. Selected top-mesh slopes still reached 61.369 degrees after the boxes disappeared. This was a real generated grade, not another hidden box face.

The motor's `LastBlockingControllerCollider` retains its last collision hit. A recorded rail name is supporting context, not proof of a current-frame rail contact. Supplemental rays never clear the original probe verdict. No probe threshold, motor, capsule size, speed, route input or accepted geography was changed.

## Repairs and separate evidence

The integrity validator now recognizes all four owned render-deck variants, requiring an enabled fitted mesh on the same bridge before borrowing collision. Missing/disabled fitted owners remain errors. Owned visual rails are excluded from floor classification; similarly named objects without bridge ownership retain the prior classification.

[Eleven initial ownership checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_012900_933_bridge_ownership/Receipt.json). The intermediate [production replay](../outputs/G08_R3_Physical_20261003/20261004_013152_989_session/Discovery.json) reached both directions, changing forward warnings to 17 with zero MissingSupport, while reverse retained 26 warnings including nine MissingSupport. Raw results remain unchanged; this partial improvement was not treated as crossing acceptance.

The bridge now builds one continuous hidden support mesh from the same shared road vertices, retaining the one-metre shoulder, half-metre depth and 0.01 m inset beneath the unchanged walk plane. It closes only the two bank ends. Internal segment end caps and their independent overlapping boxes are removed. Visual deck geometry/materials, accepted route/water identity and existing bridge ownership are preserved.

[Sixteen checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_013822_932_bridge_ownership/Receipt.json), including shared heights, dimensions, bank bounds, closed-edge topology, no internal caps, all four render variants, disabled rails, non-owned-name behavior and rejection of missing/disabled fitted owners. Evidence is **Edit Mode synthetic integrity repair**, not production clearance. Two expected negative-case WORLDGEN errors are retained. Fresh runtime/Editor compilation completed outside Play Mode; the executing receipt records source hashes and assembly identities.

The [continuous-support scene census](../outputs/G08_R3_Physical_20261003/20261004_014210_591_session/BridgeColliders.json) recorded twenty active fitted meshes and twenty continuous support meshes, with zero active support boxes, rail colliders or render-deck colliders. Forward reached all five endpoints but retained thirteen warnings; reverse retained twenty-seven, including eight MissingSupport. Those results demonstrate that removing the boxes alone did not close clearance. The original flags remain retained.

The origin crossing builder now treats water clearance as a lower bound: it preserves `finished road + 0.18 m`, raised to `accepted water surface + 0.32 m` only when that surface is higher. It does not alter the accepted route/water record, terrain carve predicate, seed, scale, palette or save schema. [All twenty-one focused checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_014947_979_bridge_ownership/Receipt.json), adding dry/wet continuity, higher-water clearance and negative-elevation cases. These five height cases are synthetic owner-level checks, not all-world bridge-grade proof.

## Persistence and limits

The before session restored its snapshot with the existing persistence barrier retained; [all twelve post-exit fingerprints matched](../outputs/G08_R3_Physical_20261003/20261004_012208_463_session/PersistedAfterPlayExit.json). Intermediate live restoration was deferred by in-flight LLM work; the barrier remained retained through Play exit and [all twelve disk fingerprints matched](../outputs/G08_R3_Physical_20261003/20261004_013152_989_session/PersistedAfterPlayExit.json). Disk preservation does not certify successful intermediate live rollback.

Profile display name `New Adventurer` does not establish disposable-fixture provenance. These protected diagnostics are not qualifying R2 witnesses. Remaining crossing bands, both cameras, frontier/reload, visual deck correspondence and final affected R1/R2 still require evidence. Overall G08 remains INCOMPLETE.

The continuous-support session's initially deferred snapshot restoration completed within the existing retry window, with its barrier retained. [Its twelve post-exit fingerprints matched](../outputs/G08_R3_Physical_20261003/20261004_014210_591_session/PersistedAfterPlayExit.json).

## Road-water-grade diagnostic (retained earlier source)

[Final discovery](../outputs/G08_R3_Physical_20261003/20261004_015212_952_session/Discovery.json) records runtime MVID `96b0c903-b0a0-4478-8093-c2cc2f7ac5f5`, runtime SHA-256 `9535eb759f6acff125a879cd9318bc69d85e46f9145d3d73228f829c3228c783`, Editor MVID `f3e85fdb-8d5d-4ff1-869f-9715910c5071`, Editor SHA-256 `03cb73cfa2d1de80284873710d67ffb1ff73567a1db62fa89967bf763e04f28c`. Accepted profile/world/seed/artifact, capsule, camera and quality settings match the preceding diagnostic boundary. The adjacent collider census still reports twenty fitted/continuous support pairs and zero active support boxes, rail or render-deck colliders.

| Leg | Endpoints | Travel | Observed frames | Probe warning frames | Streamer failures |
| --- | ---: | ---: | ---: | ---: | ---: |
| [Forward](../outputs/G08_R3_Physical_20261003/20261004_015212_952_session/20261004_015310_780_first_bridge_after_road_water_grade/Result.json) | 5 | 68.7386 m | 1,067 | 0 | 0 |
| [Reverse](../outputs/G08_R3_Physical_20261003/20261004_015212_952_session/20261004_015434_697_first_bridge_reverse_after_road_water_grade/Result.json) | 5 | 67.2524 m | 950 | 20 | 0 |

The reverse warnings are six AmbiguousSupportOverlap, four AmbiguousSupportOverlap+UnsupportedFootprint, eight MissingSupport, one Occupied+AmbiguousSupportOverlap and one UnsupportedFootprint. Fourteen were grounded. One warning is at `(13.247,63.849,approximately −37)` on the stone bridge edge; nineteen occur around X 1.930–2.054 on the origin's disheveled-bridge approach. They remain unresolved, not normalized away. The wider diagnostic X band 12.5–26 contains 32→0 forward and 13→1 reverse warnings across before/final runs; this band is not a semantic crossing ID and does not hide the nineteen approach warnings.

Controls use the existing synthetic keyboard/mouse diagnostic path, actual authoritative capsule and ordinary walk speed; no staging contributes to travel. Recorded focus was true on final frames. The maximum observed intervals were 108.805 ms forward and 125.0792 ms reverse, including diagnostic work. Neither run certifies R2 performance, visual quality or unassisted human traversal. Forward success alone cannot close EC06.

Next contact investigation: correlate the origin approach's exact last bank/road samples with the full capsule footprint and retained ray evidence; resolve the remaining stone-bank overlap without lowering thresholds or broadly exempting its floor collider. Three further retained crossing bands also remain open. No further speculative geometry patch was made.

[The full comparison](../outputs/G08_EnvironmentCohesion_20261003/20261004_bridge_repair_review.json) verifies all eight result counters against their raw frames and warning records. The final live snapshot restored successfully with its barrier retained; [all twelve final post-exit disk fingerprints matched](../outputs/G08_R3_Physical_20261003/20261004_015212_952_session/PersistedAfterPlayExit.json). Every one of the four sessions preserved all twelve fingerprints. This does not imply the intermediate live rollback that remained deferred succeeded.

## Fourth cause: inward collision faces

The retained reverse rays identified the underside of the fitted deck and continuous support: triangle indices 2/3 belong to the bottom faces in the existing mesh builder. Their normals pointed upward. The top and longitudinal sides pointed inward; the two terminal support caps already pointed outward. Unity 6.3 documents that [collision mesh faces are one-sided](https://docs.unity3d.com/6000.3/Documentation/Manual/mesh-colliders-introduction.html#directional-mesh-facing).

The [25-check reproduction](../outputs/G08_EnvironmentCohesion_20261003/20261004_021616_065_bridge_ownership/Receipt.json) retained four failures on runtime MVID `96b0c903-b0a0-4478-8093-c2cc2f7ac5f5`, bridge source SHA-256 `85a8d9ebf52073cf8fb5f67cd569fc7f08efbbf0158994be5cd584e3cc59f9bd`: both outward-face checks and both direct downward-ray checks. The fitted ray hit Y 0.573 instead of the intended 0.620; the support ray hit 0.110 instead of 0.610. This is an executed synthetic physics reproduction, not inference from naming or closed-edge counts.

An earlier writer attempt at `outputs/G08_EnvironmentCohesion_20261003/20261004_021500_177_bridge_ownership` failed before saving a receipt because ordinary JSON serialization followed Unity's recursive Vector3 properties. That attempt remains invalid. The writer now uses the existing project vector converter; it does not suppress physics failures.

The bounded repair reverses only each segment's top, bottom and side triangle order. It retains all vertices, dimensions, offsets, bank caps, materials, accepted route/water IDs, seeds and save contracts. The fixture adds actual downward Collider.Raycast checks, per-face geometric normals, four cardinal directions, two bent/signed coordinate profiles and explicit mesh cleanup. The observer adds an idle census of the small project-generated collision meshes and bank rays; it does not export vendor geometry or change probe admission.

[All 26 extended checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_022812_616_bridge_ownership/Receipt.json) after compilation outside Play Mode. The actual fitted and support rays now hit Y 0.620 and 0.610 respectively, at top-face triangle indices 1 and 0. The receipt records runtime MVID `030a996a-3bae-47cb-a729-0d8eb5a06fcd`, Editor MVID `98dd842b-4901-4497-a33a-fe6b6720320a`, bridge SHA-256 `83057eaac29f25b68354b9a0dbb88141254cd2f66d2df3e9ce33fdab7bf46699` and fixture SHA-256 `07429c736ab8bcf6d6cccfebec18a3746a068898f6b259f46d996a355ba24d04`.

The following production replay is retained on the outward-winding build. It exposes further physical defects; it does not close EC06.

## Outward-winding production replay and preservation scope

The [retained raw review](../outputs/G08_EnvironmentCohesion_20261003/20261004_bridge_winding_review.json) reconciles both legs with Frames.tsv and SupportWarnings.jsonl in [session discovery](../outputs/G08_R3_Physical_20261003/20261004_024355_404_session/Discovery.json). Runtime MVID was 030a996a-3bae-47cb-a729-0d8eb5a06fcd; Editor MVID was bccee6e0-4d04-4be2-95a0-2407986c5b5e. World, accepted artifact, capsule, ordinary speed and connected waypoints match the preceding primary-world diagnostic.

| Leg | Travel | Frames | Warning frames | MissingSupport | Support overlap | Streamer failures |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Forward | 68.79343 m | 989 | 2 | 0 | 0 | 0 |
| Reverse | 67.26761 m | 925 | 13 | 9 | 0 | 0 |

Forward warnings remain near X 13.06–13.17, Z −37.37. Reverse warnings remain near X 1.907–1.983, Z −29.362 to −28.441; five warning frames are ungrounded. The deepest retained terrain support is 1.91794 m below the expected feet. The primary accepted north-running route approaches the lateral shoulder of an east-running bridge; exact semantic bridge/route ownership still needs correlation. A 0.4 m first-bank centre height difference is also recorded. No blind widening, ramp, threshold exemption or safety-recovery success is claimed.

Maximum observed intervals were 109.4223 ms forward and 126.2014 ms reverse, with diagnostics active. Recorded focus was true; controls were synthetic keyboard/mouse through the actual motor. These are neither unprofiled R2 witnesses nor unassisted/both-camera visual acceptance. The first-person Waypoint_4.png shows repetitive bare slopes and dark repeated deck strips; this slice does not demonstrate the requested composition/ecological/material floor.

Live restoration completed and [all twelve post-begin fingerprints matched through exit](../outputs/G08_R3_Physical_20261003/20261004_024355_404_session/PersistedAfterPlayExit.json). However, the [same-original-path startup audit](../outputs/G08_R3_Physical_20261003/20261004_024355_404_session/PreProtectionPublicationAudit.json) found revision 20→21 before the barrier: seven common projection/manifest/backup paths changed and five old-revision paths stayed unchanged. The prePlayMismatches=12 field in the earlier exit artifact compared different revision key sets and is not a valid same-path changed count. No rollback, reset or save-file edit was performed. The exact normal-startup save trigger is not certified. Future gameplay diagnostics must use a proven disposable profile, rather than claim the entire normal-profile startup was protected.

## Fifth cause: repeated terminal samples fold collision strips

The census covers twenty fitted/support mesh pairs. Four nondegenerate top/bottom faces are inward, all on bridge instance −11566478 near X 181. The final two centreline samples coincide, but the final section uses the builder's default forward tangent; its side rotates from approximately (−1.0615, 4.6812) to (−4.8, 0). This folds the last strip. Ordinary triangle winding cannot repair a zero-length sampled segment.

The [54-check reproduction](../outputs/G08_EnvironmentCohesion_20261003/20261004_031116_347_bridge_ownership/Receipt.json) retained seven failures: repeated first/interior/final samples, repeated bank runs, a sub tenth-millimetre terminal sample, a fully repeated profile and the affected captured bridge. The remaining nineteen reconstructed bridge profiles passed. This explicitly reproduces the new cause separately from the previously repaired inward-winding cause.

BuildExtrudedRoadMesh now copies the derived collision centreline and removes consecutive samples within 0.1 mm in three dimensions. Distinct short segments remain; fewer than two distinct samples produce no manufactured surface. Accepted records, input arrays, route IDs, visual assemblies, widths, offsets, terrain samples and save/schema contracts are unchanged. This changes only derived fitted/support collision geometry; no generation or accepted layout migration is introduced.

After compilation outside Play Mode, [all 54 checks pass](../outputs/G08_EnvironmentCohesion_20261003/20261004_031216_526_bridge_ownership/Receipt.json). Checks compare complete vertices/indices, outward faces and unchanged inputs against clean profiles, plus all twenty centreline profiles reconstructed from the recorded project-generated fitted mesh. Reconstruction is numeric fixture evidence, not exact original-road capture or a fresh gameplay traversal. Census SHA-256: 431a901e738f5a46a5cc662676966e3dd6c4336397a5b55e5ff153c675d004bc.

| Current affected identity | Value |
| --- | --- |
| Runtime MVID | 920b79ea-8e0e-443c-8c08-a6c243a1984a |
| Runtime SHA-256 | 7c69eed743ad328312bfbcd3d677f435349853a5be49978680799b8ffd4e4c4f |
| Editor MVID | 4a9a257f-4931-42b5-a786-0031753c4118 |
| Editor SHA-256 | ca87dd8c222a6623052f8b7e145df86af7a5f2bcd01f8c175435634fa7273ab0 |
| Bridge source SHA-256 | f95575944b852b8cf47ef2e131b589b3f68c4ab11226b482d5b9f79ff9ef699a |
| Fixture source SHA-256 | 600916c4dd63e07b53aef4ab29b94ae8e5155ea732248a35205644f428532ef5 |

Next: verify the documented disposable profile's identity, replay affected crossings on this build, resolve the origin lateral shoulder/route connection at the existing owner, then cover the other crossing bands, both cameras and reconstruction. All EC rows remain NOT VERIFIED; full world, library, performance and visual acceptance remain incomplete.

## Disposable-world replay and expanded sharp-bend regression

The [current-source disposable session](../outputs/G08_R3_Physical_20261003/20261004_032515_874_session/Discovery.json) selected documented profile 3ccfff4df9a1479884061e51940e4457 through ordinary PlaySafe/title/Continue. World yq-world-0284d4409a720743c13eb56e, seed 4bb221dc and accepted/validated artifact c1d6ce9d3e06f760 match earlier fixture evidence. Startup published revision 84→85. Runtime MVID remains 920b79ea-8e0e-443c-8c08-a6c243a1984a; replay Editor MVID was 4a9a257f-4931-42b5-a786-0031753c4118. This is a different world from the primary diagnostic, not a matched improvement comparison.

| Leg | Result | Distance | Frames | Support warnings | Streamer failures |
| --- | --- | ---: | ---: | ---: | ---: |
| Direct lateral approach | FAIL: eight seconds without progress | 5.356862 m | 265 | 0 | 0 |
| Bank approach forward | Ten endpoints reached | 81.20845 m | 1,072 | 1 Occupied | 0 |
| Same bank approach reverse | Ten endpoints reached | 82.25942 m | 1,075 | 0 | 0 |

The direct attempt stops at (−1.0157,61.6136,2.9520) against Generated_RiverBridge_Wood/StoneBridgeWalkSurface while terrain support remains valid. The second leg physically retreats and approaches the bank via (−8,1), (−8,13), (−2,13); no teleport or staging occurs between legs. Its retained Occupied warning at (1.3647,62.4644,6.1016) identifies YQ_GENERATED_TERRAIN_CHUNK_11_-1. This remains a collision/ground correspondence issue; the successful reverse leg cannot average it away. Every measured frame is focused. Maximum intervals are 203.476 / 188.9049 / 150.6562 ms with diagnostics active, not R2 performance certification.

The [raw review](../outputs/G08_EnvironmentCohesion_20261003/20261004_disposable_bridge_review.json) reconciles all three result counts with frames/warnings. The census has forty small project-generated fitted/support meshes, 1,628 top and 1,628 bottom triangles. Eight inward faces occur on bridge −12227348 near (3.43,−41.36). Distinct points turn sharply: (3.6640,−39.9388) → (4.2272,−40.7595) → (3.4324,−41.3574). A wide ribbon's inner edges cross; these are distinct points, not the previous duplicate-endpoint cause. No width reduction or blind triangle flip was applied.

After stable Edit-mode compilation, the [expanded two-world fixture](../outputs/G08_EnvironmentCohesion_20261003/20261004_035711_463_bridge_ownership/Receipt.json) executes 75 checks: **74 PASS, 1 FAIL**, on that exact sharp-bend profile. It retains all earlier 54 checks and twenty additional captured profiles plus their count check. Current Editor MVID is 7701ae29-633c-4087-8e7d-c30de6b3ae13, SHA-256 473240573f3a399b4a27fef55c95b7d75c0751d01518f31ebc9bc42a9992babe; fixture source SHA-256 787e87bd4b015e23180ebe8185abb5fc897ec0bb1d7b886a76a4914e93e7b120. Runtime bridge source remains f95575944b852b8cf47ef2e131b589b3f68c4ab11226b482d5b9f79ff9ef699a. Original repeated-sample evidence stays valid within its scope; it is not an all-profile collision certificate.

### Restoration and startup recovery

Live restoration completed without deferral, with the barrier retained. [All twelve protected paths match after Play exit](../outputs/G08_R3_Physical_20261003/20261004_032515_874_session/PersistedAfterPlayExit.json). The [normal-profile audit](../outputs/G08_R3_Physical_20261003/20261004_032515_874_session/OriginalProfilePreservationAudit.json) finds four changed working/backup paths and five unchanged committed-revision paths, including absent backup sentinels. Normal revision 21, world, seed and artifact remain the same in [identity metadata](../outputs/G08_R3_Physical_20261003/20261004_032515_874_session/PostExitProfileIdentity.json).

The [startup attribution](../outputs/G08_R3_Physical_20261003/20261004_032515_874_session/StartupRecoveryAttribution.json) and exact log show the existing YQProfileSaveSystem.Awake recovery copying newer same-profile shared snapshots before title selection. The normal primary player/world projection hashes now equal the pre-entry shared hashes. This is distinct from the later protected itinerary; no full-startup byte-preservation PASS is claimed. No save rollback, direct save edit or regeneration was performed. The first pre-entry fingerprint attempt used incorrectly combined path strings; BeforePlay.invalid_path_construction.json is retained, and the corrected twenty-one-path baseline was validated before Play.

Current source still needs a causal sharp-bend collision construction repair, direct-bank/terrain-contact proof, primary origin shoulder repair, both cameras and reconstruction. Captures show sparse/repetitive slopes and conspicuous deck strips; the required visual floor remains unevidenced. G08 and every EC row remain incomplete.

## Sixth cause: full-width sharp-bend collision construction

The folded-strip owner now detects inward or collapsed top triangles and rebuilds only those derived collision surfaces as the union of full-width segment rectangles and outer bevels. It keeps the original bank planes, width, thickness and every original centreline height. A constrained chain along each original road segment preserves the interpolated tread grade. Ordinary nonfolded strip output stays on its existing path. This introduces collision geometry version `buffered_road_union_v2`; accepted inputs, route/water IDs, visual meshes, materials and save/schema contracts are preserved. Disconnected, overlapping or unsupported multiple-loop boundaries fail explicitly.

The [first union attempt](../outputs/G08_EnvironmentCohesion_20261003/20261004_042130_118_bridge_ownership/Receipt.json) failed at a numerically boundary-aligned bank sample. A ten-micrometre distance tolerance replaces the fixed area tolerance. The [107-check pass](../outputs/G08_EnvironmentCohesion_20261003/20261004_042253_043_bridge_ownership/Receipt.json) was then expanded: [115 checks exposed eight grade failures](../outputs/G08_EnvironmentCohesion_20261003/20261004_042413_095_bridge_ownership/Receipt.json), with up to 0.331077 m between-sample height error. Retaining isolated anchors alone was insufficient. Inserting original road-segment edge chains repaired the demonstrated grade defect; [115 checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_042545_470_bridge_ownership/Receipt.json).

After compilation outside Play Mode, [all 119 focused checks pass](../outputs/G08_EnvironmentCohesion_20261003/20261004_042758_256_bridge_ownership/Receipt.json). They retain the previous 75 checks and forty reconstructed profiles, then exercise short ninety-degree turns, reversal, mirroring and signed far coordinates. Independent checks cover outward top/bottom/wall faces, indexed manifold edges, terminal bank openings, 95% of full segment widths, original height anchors, actual cooked downward rays, interpolated centreline grades and paired support clearance. Maximum sampled grade error is 0.000069574 m; sampled wider support remains approximately 0.01 m below the fitted tread. These are bounded Edit-mode geometry/physics checks, not production traversal, visual or throughput certification.

| Buffered-mesh fixture identity before terrain replay repair | Value |
| --- | --- |
| Runtime MVID | c13d0c75-d977-430f-8549-042002fb98c5 |
| Runtime SHA-256 | 2436b1ccc3eccfc767502acc03e27cac0069b5a6cc99f5fce22248daca5ad980 |
| Editor MVID | 58cd43bd-193d-431e-9734-4bb2d3c8fb6e |
| Editor SHA-256 | b3fa8df2a2ee5e9e2c7b8018ce4fd36c914a53e45ca37a7726f0c720add4b303 |
| Bridge source SHA-256 | f32ce5c0dea6c11fcdc279689932567de647044453b60566a8fd033641302aad |
| Fixture source SHA-256 | fc0b7b5edbd60ba36ca5b665076303d86040ca38347c4299351faa97910d442c |

The idle runtime census now records buffered top/bottom/wall submeshes without pretending that their vertex order encodes strip bank sections. Actual gameplay replay, bank/terrain contact correspondence, both cameras, reconstruction and added construction/cooking cost remain open. The accepted legacy forced PASS remains preserved; final affected expanded-goal regression evidence is still required. Every EC row and G08 remain incomplete.

### Buffered production replay: geometry repaired, bank ingress fails

[Discovery](../outputs/G08_R3_Physical_20261003/20261004_043254_948_session/Discovery.json) confirms the same disposable profile/world/seed/artifact, ordinary Continue revision 86, commit 558f9fd072744d2a82d896cd26b3ead9, and the exact current fixture runtime/Editor identity above. [The raw review](../outputs/G08_EnvironmentCohesion_20261003/20261004_buffered_bridge_review.json) checks frame/warning counters and forty captured generated collision meshes. Two meshes use the buffered union. All 1,674 top and 1,674 bottom faces point outward, versus eight inward faces in the preceding disposable census. This is actual runtime collision geometry evidence; runtime wall clearance and full crossing acceptance are separate.

| Continuous leg | Result | Distance | Frames | Support warnings | Streamer failures |
| --- | --- | ---: | ---: | ---: | ---: |
| West trail sharp-bend bridge approach | FAIL: eight seconds without progress after four XZ endpoints | 30.93522 m | 663 | 0 | 0 |
| Physical retreat to start | Four endpoints reached | 30.198717 m | 505 | 0 | 0 |

The forward leg physically approaches the first bank, then blocks at (−6.852597,58.469425,−24.605598), contacting Generated_RiverBridge_Disheveled/StoneBridgeWalkSurface. Numeric interpolation of the captured fitted top at that exact XZ is Y 60.829638, approximately **2.360211 m above the recorded feet**; the support top is 2.350209 m above. This interpolation is not a new physics ray or admission override. [The blocked player view](../outputs/G08_R3_Physical_20261003/20261004_043701_603_discovery/PlayerView.png) shows the underside above the camera. The retreat does not count as a reverse crossing. All observed frames were focused, with synthetic ordinary keyboard/mouse through the authoritative capsule. Maximum intervals 122.8161/143.7865 ms include diagnostics and do not certify performance.

All 38 remaining strip meshes have unique exact-footprint matches in the preceding census and identical indices. **Twenty-four change height by more than 0.1 mm, with maximum 1.338634 m**, despite the same accepted world/artifact. The compared environment, runtime-builder, cell/feature authority, streamer, motor, integrity and profile-owner source hashes are unchanged; only bridge construction and the census observer differ. The original nonfolded construction path is retained. This establishes a reconstruction-height difference, not its cause. Saved terrain restoration, pre-carve road sampling and subsequent channel restoration require a same-source reload comparison before a causal bank repair. Do not call these sessions identical terrain baselines or attribute all differences to the buffered mesh.

Live restoration succeeded with its barrier retained and Play exited. [All twelve exact protected fingerprints match](../outputs/G08_R3_Physical_20261003/20261004_043254_948_session/PersistedAfterPlayExit.json). [All nine normal-profile paths match the pre-entry baseline](../outputs/G08_R3_Physical_20261003/20261004_043254_948_session/OriginalProfilePreservationAudit.json), including the existing committed revision and absent-backup sentinels. Shared projection/manifest changes and four disposable working/backup changes occurred during ordinary startup/recovery; revision 85 committed files remain unchanged and startup published revision 86. No save was directly edited or rolled back. This preservation result does not certify semantic reload stability.

The subsequent [terrain replay repair](G08_EnvironmentCohesion_TerrainReplayRepair_2026-10-04.md) reproduces same-build cumulative terrain drift and removes the restored-snapshot road regrading invocation. The current runtime MVID is a63612f1-1e30-4f0b-a920-3fa65a114fbd. Refreshed bridge contracts pass119 checks; two ordinary Continue sessions preserve all263,169 saved terrain samples and forty collision meshes on identical repaired assemblies. Both sessions restore and exit with protected12 and normal9 fingerprints unchanged. The earlier blocked bank approach remains historical FAIL evidence, not a repaired crossing claim. Next is causal bank/terrain correspondence, followed by both-camera crossing, broader reconstruction, cost, complete library/world coverage and visual acceptance. G08 stays INCOMPLETE.


## Subsequent whole-span correspondence and footprint failure

[The current diagnosis](G08_EnvironmentCohesion_FootprintDiagnosis_2026-10-04.md) supersedes the earlier first-bank interpretation: the blocked point is an internal section seam. Connected true-bank forward/dry-egress/reverse walking covers159.644m with86 footprint warnings. Recorded faces reach67.94032degrees against the58-degree capsule. Three unsuccessful grade candidates are retained and retired; the loop is stopped. Fresh compilation and126 native checks restore the preceding geometry: original119PASS, four new slope/footprintFAIL. Full G08 remains incomplete; no failed candidate was played, legacy accepted PASS remains, and Astra stays stopped. Independent library/reference work is the next executable slice while a shared-width grade review is prepared.
