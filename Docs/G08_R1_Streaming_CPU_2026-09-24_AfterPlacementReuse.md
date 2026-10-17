# G08-R1 streaming CPU capture

UTC: 2026-09-24T08:57:22.0579398Z
Evidence: direct Unity Profiler recorders in the Editor. Inclusive metrics overlap and must not be added together. Deep profiling and quality settings were not changed.
Focus at start/end: True/True; ending timeScale: 1
Real elapsed seconds: 15.035; gameplay frames: 275

| Metric | Unit | Samples | Mean | Maximum |
| --- | --- | ---: | ---: | ---: |
| EditorLoop | ms | 276 | 0.000 | 0.000 |
| GC.Alloc | ms | 276 | 4.997 | 21.988 |
| GC.Collect | ms | 276 | 0.673 | 16.238 |
| Main Thread | ms | 276 | 53.901 | 451.236 |
| PlayerLoop | ms | 276 | 49.636 | 393.582 |
| WaitForTargetFPS | ms | 276 | 0.000 | 0.002 |
| RenderLoop.Draw | ms | 276 | 1.506 | 2.552 |
| Gfx.WaitForPresentOnGfxThread | ms | 276 | 0.000 | 0.000 |
| CoroutinesDelayedCalls | ms | 276 | 34.277 | 89.644 |
| BehaviourUpdate | ms | 276 | 8.071 | 55.457 |
| Camera.Render | ms | 276 | 0.001 | 0.004 |
| Update.ScriptRunBehaviourUpdate | ms | 276 | 8.072 | 55.458 |
| Update.ScriptRunDelayedTasks | ms | 276 | 0.003 | 0.007 |
| PreLateUpdate.ScriptRunBehaviourLateUpdate | ms | 276 | 0.201 | 3.146 |
| YQAuthoredSiteAssetImportWatcher.OnPostprocessAllAssets | ms | 276 | 0.000 | 0.000 |

Result: CAPTURED (not a performance acceptance PASS).
