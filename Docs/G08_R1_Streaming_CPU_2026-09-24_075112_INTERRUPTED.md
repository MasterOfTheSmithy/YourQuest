# G08-R1 streaming CPU capture

UTC: 2026-09-24T07:51:12.3696571Z
Evidence: direct Unity Profiler recorders in the Editor. Inclusive metrics overlap and must not be added together. Deep profiling and quality settings were not changed.
Focus at start/end: True/False; ending timeScale: 0
Real elapsed seconds: 12.775; gameplay frames: -7955

| Metric | Unit | Samples | Mean | Maximum |
| --- | --- | ---: | ---: | ---: |
| EditorLoop | ms | 128 | 0.000 | 0.000 |
| GC.Alloc | ms | 128 | 7.835 | 29.877 |
| GC.Collect | ms | 128 | 3.475 | 305.081 |
| Main Thread | ms | 128 | 95.653 | 1821.101 |
| PlayerLoop | ms | 128 | 74.368 | 195.362 |
| WaitForTargetFPS | ms | 128 | 0.000 | 0.001 |
| RenderLoop.Draw | ms | 128 | 1.503 | 2.496 |
| Gfx.WaitForPresentOnGfxThread | ms | 128 | 0.000 | 0.000 |
| CoroutinesDelayedCalls | ms | 128 | 57.432 | 159.782 |
| BehaviourUpdate | ms | 128 | 9.901 | 32.462 |
| Camera.Render | ms | 128 | 0.001 | 0.003 |
| Update.ScriptRunBehaviourUpdate | ms | 128 | 9.902 | 32.463 |
| Update.ScriptRunDelayedTasks | ms | 128 | 0.003 | 0.007 |
| PreLateUpdate.ScriptRunBehaviourLateUpdate | ms | 128 | 0.255 | 2.533 |
| YQAuthoredSiteAssetImportWatcher.OnPostprocessAllAssets | ms | 128 | 0.000 | 0.000 |

Result: CAPTURED (not a performance acceptance PASS).
