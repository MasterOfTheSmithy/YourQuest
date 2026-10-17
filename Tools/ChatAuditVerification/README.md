# October 17 chat repair verification

These checks execute selected current production C# methods against explicitly controlled Unity and data-owner boundaries. They do not launch Unity, alter a profile, rebuild imported assets, or certify native performance.

## Run

Requirements: Python 3, Git, and a locally installed .NET 8 SDK with its reference pack, Roslyn assemblies, and SDK Newtonsoft.Json assembly. The verification session used SDK 8.0.426 and runtime/reference pack 8.0.32. No package download is performed by these scripts.

```sh
python Tools/ChatAuditVerification/run.py --sdk-root /path/to/dotnet --output /path/to/verification-output
python Tools/ChatAuditVerification/run_assets.py --dotnet-root /path/to/dotnet
```

The first command defaults to source baseline `ee485e38ab6d66610e5ce3293b615b6b1a9dc1b8`. Use `--baseline` to select another comparison commit. It includes modified and new project C# files, parses both player and Editor conditional configurations using C# 9, then compiles and executes the focused publication, population, and movement fixtures. The scripts invoke the SDK compiler directly; they do not need a generated Unity project or restored NuGet packages. The asset script currently targets a Linux .NET 8 installation and removes its temporary executable output on completion.

## What the checks establish

| Area | Executed production code | Controlled boundary |
| --- | --- | --- |
| Source syntax | Complete changed project C# files, player and Editor branches | Parsing only; Unity types are not resolved |
| Paired publication | Verbatim profile preparation/publication/validation, actual receipt/hash definitions, complete real commit store with temporary filesystem revisions | Player/world/construction owners, auxiliary callbacks, and fault injection |
| Population | Verbatim scoped snapshot/body/life checks, prefab preparation coroutine, LOD stabilization, and metadata classification | Unity objects, JSON codec, DTOs, native instantiation, materials, and bounds |
| Movement | Verbatim motor admission, movement gate, motion accounting, streamer availability, budget, terrain-ticket registration, synchronization arbitration, and publication iterators | Unity object lifetime, controller motion, physics synchronization, clock, appearance work, and final resource cleanup |
| Assets | Complete current runtime registry and dependency descriptor; current fallback approval methods | Resources requests, residency clock, and ScriptableObject behavior; imported dependency discovery is not simulated |

Assertions cover both successful publication and rejected stale/corrupt states. Fixture methods are extracted from the checked-out source rather than copied implementations of their control flow. The asset checks also use the supplied 8,174-entry runtime manifest and the published intake records for the eight historical undergrowth fallback paths.

The second repair batch adds a twelve-frame replay of continuous provisional ground work before canonical publication coroutines resume. It checks progress for both pending terrain tickets, the flush and cook frame boundaries, shared terrain synchronization limits, ordinary and urgent budget eligibility, cancellation, ownership replacement, and native-sync failure handoff. Asset checks keep one selected prefab pending while an unrelated cold prefab and dependency become eligible for retirement; implicit preload leases, external demand leases, single-pass input ordering, and delayed native completion are covered.

The verification session emitted an SDK reference-unification warning (`CS1701`) for the SDK's Newtonsoft.Json assembly. The registry fixture also reports `CS0649` for fields populated through JSON deserialization. No compilation errors occurred in the focused managed fixtures. This is separate from Unity compilation, which was unavailable.

## Required Unity follow-up

In the complete local project, build and validate prefab dependency preloads through the explicit `YourQuest > Assets` menu operations. Run `YourQuest > Verification > Run Movement Admission Regression` in stable Edit mode. Then validate the affected paths in the production PlaySafe scene, including cold activation, cancellation/profile changes, save/reload, and sustained 200 m/s travel. The editor fixture and these runtime checks were not executed in the chat environment.

See [the initial repair report](../../Docs/Audits/2026-10-17/Repair_Report.md) and [the second repair batch](../../Docs/Audits/2026-10-17/Beta_Progress_02.md) for the repair scope, before/after evidence, and remaining acceptance work.
