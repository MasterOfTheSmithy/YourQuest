# G08-R1 streaming CPU capture

UTC: 2026-09-26T16:34:54.5717144Z
Evidence: direct Unity Profiler recorders in the Editor. Inclusive metrics overlap and must not be added together. Deep profiling and quality settings were not changed.
Focus at start/end: True/True; ending timeScale: 1
Real elapsed seconds: 15.021; gameplay frames: 459

| Metric | Unit | Samples | Mean | Maximum |
| --- | --- | ---: | ---: | ---: |
| EditorLoop | ms | 460 | 0.000 | 0.000 |
| GC.Alloc | ms | 460 | 1.259 | 36.167 |
| GC.Collect | ms | 460 | 0.250 | 17.641 |
| Main Thread | ms | 460 | 32.215 | 371.150 |
| PlayerLoop | ms | 460 | 28.583 | 189.087 |
| WaitForTargetFPS | ms | 460 | 0.001 | 0.185 |
| RenderLoop.Draw | ms | 460 | 3.373 | 8.513 |
| Gfx.WaitForPresentOnGfxThread | ms | 460 | 0.000 | 0.000 |
| CoroutinesDelayedCalls | ms | 460 | 6.301 | 119.637 |
| BehaviourUpdate | ms | 460 | 11.657 | 98.643 |
| Camera.Render | ms | 460 | 0.001 | 0.005 |
| Update.ScriptRunBehaviourUpdate | ms | 460 | 11.658 | 98.644 |
| Update.ScriptRunDelayedTasks | ms | 460 | 0.003 | 0.011 |
| PreLateUpdate.ScriptRunBehaviourLateUpdate | ms | 460 | 0.607 | 97.860 |
| YQAuthoredSiteAssetImportWatcher.OnPostprocessAllAssets | ms | 460 | 0.000 | 0.000 |

Result: CAPTURED (not a performance acceptance PASS).
