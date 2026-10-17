# G08 R2 targeted gate — 2026-09-29 19:04 UTC

**Verdict: FAIL.** R3 remains gated. R1 and R3 were not run.

## Source and authority

- Unity `6000.3.2f1`, PlaySafe, normal selected-profile Continue and background focused-speed marker. The Editor recompiled after the patch; the 19:03 witness executed `Assembly-CSharp.dll` SHA-256 `1DC916532BC6486CC3B56142BABE48F87A3CA1698D21CA06CCA6095B98BF0ECB` (MVID `1f8634a7-06e1-4dfe-a5f6-b2f559c5ded9`). Streamer source SHA-256 `233CA99DE7A8A905E7929F4D06DC57D263992038662ECC5760FCEE9FF3CEC0AD`; minimap source SHA-256 `B6D3352641A5A2FA750C3C9C1B4E183C0A2A05C30024B6A1B569C69DBFA453AD`.
- Disposable profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 content and validated hash `c1d6ce9d3e06f760`. Identity matched before and after the patch. No seed, accepted artifact, motor setting, view distance, capacity, or 50 ms threshold was changed.
- Full terminal witness: [G08_R2_Speed260_2026-09-29_190350_FAIL.md](G08_R2_Speed260_2026-09-29_190350_FAIL.md). The opt-in diagnostic native trace from the pre-patch source is `Logs/G08_R2_FrameTrace_20260929_185222_820.tsv` with raw capture alongside it; it was an **unfocused, profiler-armed diagnostic**, not performance certification.

## Targeted diagnosis and change

- The pre-patch focused receipt had 38 camera-ground synchronous fallbacks during speed input; 35 had finished background height samples awaiting publication. The live-view provisional publication reserve was raised from one to three **within the existing aggregate budget check**. The post-patch 19:00 attempt observed zero fallbacks; the 19:03 attempt still observed seven, all with completed samplers. Publication throughput is improved in one run but not repeatably sufficient.
- In the diagnostic native trace, frames 6772, 6829, and 6888 included 83.826, 119.065, and 101.196 ms respectively inside `YQGeneratedWorldMinimap.Update`. Its three-second `SaveDiscovery` call serialized the whole `WorldState` mirror; the valid active profile uses paired player/world commits. The minimap now continues writing discovered flags into the live `WorldState` but skips that recurring standalone mirror save while a profile owner is active. The existing paired Save/Quit path remains the persistence owner. Paired reload after this change was **not** run in this R2-only scope.
- The diagnostic trace also recorded a 1,643.620 ms `FrameDebuggerWindow.Paint → Semaphore.WaitForSignal` at frame 6965. It does not establish the cause of the later 19:03 coast peak; that peak has no explicit matching-frame native trace.

## Post-patch R2 evidence

- Startup accepted-save terrain, initial required/visual coverage, ordinary walk, run, dash, reversal, diagonal movement, rapid turns, abrupt 6→260 transition, boundary crossing, input visible view, turn buffer, and fall-safety recovery: **PASS** in the 19:03 witness. World/profile/accepted V2 identity remained stable.
- Input: 54 frames, median 43 ms, P95 61 ms, maximum 65 ms; seven synchronous camera-ground publications. Coast: 215 frames, median 34 ms, P95 58 ms, maximum 859 ms against the unchanged 50 ms limit. Ground coverage and input release held; visible coverage failed at cell `(10,8)`, whose accepted-site structure iterator had ten budget-denied frames over 1.717 seconds and required content/ecology remained incomplete. No traversal blocks or fall-safety recoveries were reported.
- Terrain sampling during the stress window: 332 executed, 316 successful, 16 canceled, zero faulted over 12.307 seconds (27.0 executed tasks/s); 39 canonical handoffs. Final physical census: 122 live/122 cached physical owners, below capacity 256. These observations do not certify the full owner/job/painter lifetime contract.
- `Application.isFocused=False` at input acknowledgment; all 54 speed and 215 coast frames were marked unfocused. A second post-patch attempt at 19:00 also recorded unfocused input; it had zero synchronous fallbacks and complete visible coverage but an 80 ms coast maximum. Neither is a qualifying focused repeat. Game-view activation and marker dispatch from the same computer-use session did not resolve the focus report.

## Stop boundary

R2 is not at the repeatable-evidence standard. The remaining demonstrated blockers are incomplete coast content, nonzero synchronous publication in one post-patch attempt, and frame maxima above 50 ms. The 19:03 spike cannot be assigned to a project owner from its recorder's frame association. Focus reporting also invalidates these attempts as final R2 certification. Do not start R3 or promote historical R1/R2 rows to a current overall PASS.
