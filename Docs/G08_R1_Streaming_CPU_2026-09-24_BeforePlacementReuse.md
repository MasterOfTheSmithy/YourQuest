# G08-R1 streaming CPU capture

UTC: 2026-09-24T08:49:49.5166765Z
Evidence: direct Unity Profiler recorders in the Editor. Inclusive metrics overlap and must not be added together. Deep profiling and quality settings were not changed.
Focus at start/end: True/True; ending timeScale: 1
Real elapsed seconds: 15.007; gameplay frames: 341

| Metric | Unit | Samples | Mean | Maximum |
| --- | --- | ---: | ---: | ---: |
| EditorLoop | ms | 342 | 0.000 | 0.000 |
| GC.Alloc | ms | 342 | 3.145 | 12.711 |
| GC.Collect | ms | 342 | 0.543 | 13.853 |
| Main Thread | ms | 342 | 43.667 | 259.899 |
| PlayerLoop | ms | 342 | 40.208 | 254.356 |
| WaitForTargetFPS | ms | 342 | 0.000 | 0.001 |
| RenderLoop.Draw | ms | 342 | 1.412 | 1.975 |
| Gfx.WaitForPresentOnGfxThread | ms | 342 | 0.000 | 0.000 |
| CoroutinesDelayedCalls | ms | 342 | 28.418 | 243.055 |
| BehaviourUpdate | ms | 342 | 5.640 | 31.944 |
| Camera.Render | ms | 342 | 0.001 | 0.004 |
| Update.ScriptRunBehaviourUpdate | ms | 342 | 5.641 | 31.945 |
| Update.ScriptRunDelayedTasks | ms | 342 | 0.003 | 0.012 |
| PreLateUpdate.ScriptRunBehaviourLateUpdate | ms | 342 | 0.152 | 0.706 |
| YQAuthoredSiteAssetImportWatcher.OnPostprocessAllAssets | ms | 342 | 0.000 | 0.000 |

Result: CAPTURED (not a performance acceptance PASS).
