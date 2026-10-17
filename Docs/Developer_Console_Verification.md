# Developer console verification — October 3, 2026

Status: current code compiles; gameplay verification deferred at the user's request. No Play session was entered. Computer Use was stopped with the physical Escape key, and no further Unity UI actions were issued. The pending final fixture request was removed so checks will not run unexpectedly after the user returns to the Editor.

| Evidence | Result |
|---|---|
| Current runtime source compilation | PASS — installed Unity Roslyn compiler with actual project response-file references, including both new toolkit scripts |
| Current Editor source compilation | PASS — fixture compiles against the current runtime assembly; unrelated existing unreachable-code warning |
| Current full-source public symbol compilation | PASS — metadata includes only `YQDeveloperConsoleGate`; registry, UI, animation preview and developer resident mutation API absent |
| Development symbols without opt-in | PASS — same toolkit exclusions |
| Development symbols with `YQ_DEVELOPER_CONSOLE` | PASS — toolkit and development resident API present |
| Detached Edit Mode state fixture | PASS — 97 checks at 2026-10-03T17:27:08.1266079Z, assembly 0046fe29-9ef1-436f-95d6-e1313dcfbcd5; this predates the final catalog presets and value-prefill refinements |
| Final five additional catalog/prefill checks | COMPILED, NOT RUN — source now checks real value-prefill, real DOT asset ID and invalid creature count/force/path; rerun the Edit Mode menu when convenient |
| Patch whitespace / affected references | PASS — no whitespace errors; normal resident callers retain default production fallback behavior |
| Ordinary visible console, physical typing capture, HUD refresh, scene actor picking | NOT VERIFIED — no Play testing requested |
| Successful live model generation, NPC/pickup/catalog monster spawning, animation previews and cleanup | NOT VERIFIED — no Play test; detached fixtures verify rejection/owner/rollback contracts, not successful scene materialization |
| Launched public/development players | NOT VERIFIED — full-source compiler/metadata gating only; no player build launched |

The 97-check receipt covers the existing force-granted committed Battlefield Explorer skill, real learning threshold/cooldown overrides, stat notifications and the derived-health service used by HUD, numeric rejection, XP level-up, stack/capacity rules, NPC affinity, snapshot restore, save barriers, parser/autocomplete/audit, structured quest acceptance, synthetic prompt context without evidence mutation, rejected force-proposal flag cleanup, title acceptance/restoration, and scene actor target restrictions/stale-target rejection. Detached owners never load/reset a real profile.

`Developer_Console_Build_Gate_Receipt.json` records the current full-source configuration and metadata results. `Developer_Console_Contract_Receipt.json` retains the actual 97-check run; it is not presented as a final runtime PASS.

Run `Tools > YourQuest > Testing > Verify Developer Console Contracts` for the updated detached checks after the Editor finishes importing the latest fixture source. The fixture refuses Play Mode, stale assemblies, compiler failures and an active test snapshot. No automatic request remains queued.

Material integration limits: NPCs expose canonical affinity, not player-style stats/skills/inventory. NPC creation copies an accepted resident template; synthetic identity/persona is a labeled test override rather than a new LLM world-plan transaction. Catalog monster models use an existing Editor-only spawner import path and therefore reject clearly in development players. Runtime animation state graph enumeration is unavailable; actual triggers and validated typed state paths remain supported. Animation restoration captures parameters/states, not pending trigger/transition history. Snapshots preserve current position/streamed world rather than rewinding the whole world.

A later authorized PlaySafe check should cover guided forms and small-screen scrolling, collider picking with target indication, a pending generated offer and separate Add, temporary NPC/pickup/enemy creation and Restore, selected controller preview/Stop, visible derived HP changes and physical input capture. Public/development player launch gating remains a separate check. These are deferred acceptance checks, not claims made from compilation.
