# Player animation authoring and repair

This is the entry point for later Codex runs, including Sol 6.1. Read the current user request, this guide, and the latest repair receipt. Work on the requested actions; this guide does not authorize unrelated gameplay or save changes.

## Start here

1. Run `pwsh -File Tools/player-animation-status.ps1`. It reads editor state, source/assembly hashes and receipt freshness. It does not compile or certify runtime behavior.
2. Inspect the relevant working-tree diff. Coordinate with any chat currently using the shared Unity editor; prepare patches outside `Assets` during its test window.
3. Read `Docs/Player_Animation_Repair_Receipt_2026-10-02.md` for the unresolved repair checkpoint. Do not treat old `PASS` filenames as current proof.
4. Use `YourQuest/Player Animation` in Unity. Discover installed Unity MCP capabilities first. If none are available, follow `UNITY_MCP_GUIDE.md` and the installed computer-use skill. In this session, Windows control uses `@oai/sky` through `node_repl`; `cua_repl` has native apps disabled. Never invent a Unity tool name or automate input through PowerShell.

## Tools and effects

All helpers below are in `Assets/Assets/Scripts/Tutorial/Editor/`.

| Unity menu action | Owner | Effect and evidence |
| --- | --- | --- |
| Verify Controller and Humanoid Motions | `YQPlayerAnimationAssets.Verify` | Ctrl+Shift+F10. Disposable editor rig; checks controller/clip contracts. Writes `Docs/Player_Animation_Editor_Receipt_2026-10-01.json`; read its internal UTC. |
| Repair and Verify Body Locomotion | `YQPlayerLocomotionRepair.Verify` | **Mutates the existing gait setup in Edit Mode**, calibrates foot speed, samples the approved body and renders poses. Writes `Docs/Player_Animation_Locomotion_Editor_Receipt_2026-10-02.json` and `Docs/Player_Animation_Locomotion_Repair_2026-10-02/`. |
| Verify and Preview Authored Motions | `YQAuthoredPlayerAnimations.VerifyAndRender` | Edit Mode preview of authored actions and first-person arms. Outputs `AnimationPreview/receipt.json` and images. Disposable preview evidence, not a live gameplay test. |
| Apply Authored Motions and First Person Arms | `YQAuthoredPlayerAnimations.Apply` | **Broad authored-animation setup mutation.** Rebinds actions/gait and bakes derived arms when needed. Do not run this merely to inspect a pose. Review its final mappings before applying. |
| Apply Complete Action Coverage | `YQPlayerActionAnimationSetup.Apply` | Explicit setup for the accepted interaction, pickup, consume, equip, unarmed attacks and crouch-dash roll states. Repairs hidden-preview references while preserving the controller GUID. |
| Verify Every Action and Render Poses | `YQPlayerActionAnimationVerification.Run` | Read-only on controller assets and saves. Renders four phases of gait/actions, compares native pack gaits, tests accepted trigger entry/recovery and full-body roll recovery. Writes `Docs/Player_Animation_Action_Previews_2026-10-02/evidence.txt` and pose images. |
| Run Bounded Motion Witness in Current Play Session | `YQPlayerAnimationRuntimeVerification.Run` | Ctrl+Shift+F11 after ordinary Continue startup. Up to 55 seconds of synthetic keyboard input on the real motor; actions use presentation callbacks. Restores keyboard/view and checks inventory preservation. Writes `Docs/Player_Animation_Runtime_Receipt_2026-10-02.json`. |
| Verify Current First Person Dodge Roll | `YQPlayerAnimationRuntimeVerification.RunRollOnly` | Short real-motor check from grounded rest: crouch, Q, observe body roll and first-person tuck/recovery. Writes a separate `Docs/Player_Animation_Roll_Runtime_Receipt_2026-10-02.json`. Use clear terrain; do not repeatedly walk a long witness into a cliff. |
| Repair Traversal Phases and Rider Motions | `YQPlayerTraversalAnimationRepair.Apply` | Explicit controller authoring for dash/roll entry, travel and recovery, jump/land timing, in-place climb and native rider motions. Also runs the disposable traversal preview. |
| Verify Traversal Phases and Rider Motions | `YQPlayerTraversalAnimationVerification.Run` | Disposable body and first-person phase renders and transition checks. A numerical PASS requires separate visual review. Outputs `outputs/Player_Animation_20261002/TraversalPreview/`. |

The locomotion helper no longer mutates the gait on reload. Applying or recalibrating motion is explicit so a reload cannot silently undo a reviewed authoring choice. F12 is not assigned because it conflicted with another editor tool.

After a script refresh, wait for compilation **and domain reload**, inspect fresh errors, then confirm the loaded controller's actual clip references. One earlier compilation reported that source changed during compilation and the old helper ran until another refresh. An assembly timestamp alone did not prove the intended source was loaded.

## Owners and source assets

| Responsibility | Existing owner / path |
| --- | --- |
| Input, displacement, stamina acceptance, jump/dash/crouch/climb | `YQInvestorPlayerMotor.cs` |
| Combat and interaction acceptance | `YQInvestorCombat.cs`; accepted pickup/loot/consumable methods supply their own success boundary |
| Parameters, action presentation, body/equipment binding | `YQPlayerEquipmentVisual.cs` |
| First-person view | `YQFirstPersonArmsView.cs`, `YQFirstPersonArmsAssets.cs`; mirrors the same body bones, no second Animator |
| Controller | `Assets/Assets/Resources/Player/YQPlayer.controller`, GUID `151a16946511aff48bf0a723cb3119c7` |
| Approved production character | `Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab` |
| Approved body used for retargeting | `Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Models/Bodies/Human Male (v4.1).FBX` |
| Existing body-native motions | `Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Animations/Human Generic.FBX` |
| Authored universal motions | `Assets/Assets/Art/PlayerAnimation/Quaternius/UAL1_Standard.fbx` and `UAL2_Standard.fbx`; original CC0 notices are beside them |

Search those packs narrowly before creating a new clip. `YQAuthoredPlayerAnimations.Library` resolves exact humanoid, event-free clip names. Preserve imported assets/settings; put necessary adapters or derived clips in project-owned assets. Do not use arbitrary generated names as executable animation paths.

Exclude names beginning with `__preview__` when resolving imported clips. Unity's hidden preview subassets can precede the real clips and do not carry the same import settings. The isolated October 2 comparison confirmed two independent defects: Actions.Empty at weight one retained a combat pose, and Quaternius Original root orientation made the actual clips face backward on this body. The prepared setup lowers the empty action layer to zero and uses body-forward orientation only for these project-added animation models. The native pack's idle and walk looked more relaxed on its own body once the overlay was removed. Never apply this orientation change indiscriminately to other imported packs.

Useful existing clips: UAL1 `Idle_Loop`, **`Walk_Loop`**, `Sprint_Loop`, `Crouch_Idle_Loop`, `Crouch_Fwd_Loop`, `Jump_Start`, `Jump_Loop`, `Jump_Land`, `Roll`, `Interact`, `PickUp_Table`, `Punch_Jab`, `Punch_Cross`, `Spell_Simple_*`, `Hit_Chest`, `Death01`. UAL2 has `Consume`, `Chest_Open`, `ClimbUp_1m`, `Sword_Dash`, `Sword_Block`, `Sword_Regular_A/B` and matching recovery clips. The native body pack has `WalkBack`, `GrabWeaponR/L`, `PutAwayWeaponR/L`, and hand poses. Clip names are candidates, not visual acceptance.

## Design an action before wiring it

For each implemented player action, record its accepting owner, trigger/state, source clip, timing, interruption rules, return state, first-person treatment, and evidence. Include idle, directional walking/running, crouch idle/move, takeoff/airborne/landing, simple dash, **grounded crouch+dash dodge roll**, interact, pickup/loot, melee/recovery, cast/hold/release/cancel, guard, consume, equipment changes, hit, death/revival and climb. Do not invent unsupported gameplay such as swimming solely because the pack has a clip.

The user explicitly requested crouch+dash rolls. Plain Q remains a simple dash; ordinary jumping must not end in a roll. Animation observes accepted actions and cannot spend stamina, damage targets, collect items or save state. Hook successful gameplay boundaries rather than raw button presses. Rejected/cooldown actions must not play a success animation. Preserve the existing melee actions the user said looked decent unless a verified defect requires a change.

### Implemented action map

This table describes the October 2 implementation. Read the repair receipt for the current evidence boundary; implementation does not imply a live PASS. All views use the same source Animator. First-person arms mirror it and keep the camera upright during the roll.

| Player action | Accepting owner / presentation signal | Controller / source |
| --- | --- | --- |
| Idle, walk, backward walk | Motor collision-resolved displacement | Locomotion tree; native `Idle`, `Walk`, `WalkBack`; native `Idle_Combat` for first-person ready |
| Run | Motor accepted sprint and resolved speed | Locomotion tree; body-native `Run`, measured stance-speed cadence |
| Crouch idle / travel | Motor `IsCrouching` | Crouch tree; UAL1 `Crouch_Idle_Loop`, `Crouch_Fwd_Loop` |
| Jump / fall / land | Accepted motor jump, vertical velocity and ground contact | JumpRise / Fall / Land; UAL1 `Jump_Start`, `Jump_Loop`, `Jump_Land` |
| Plain dash | Motor accepts Q and calls `PlayRollFeedback` | Dash; UAL2 `Sword_Dash`; existing 0.18-second movement |
| Dodge roll | Motor accepts grounded crouch+Q | DodgeRoll; UAL1 `Roll`; 3.2 m / 0.6 s, same existing cost/cooldown/collision owner |
| Climb | Motor `IsClimbing`, accepted `ClimbEnter` | ClimbStart / Climb / ClimbRecover; project-owned in-place stroke. Current visual quality remains rejected; UAL2 `ClimbUp_1m` is a vault and must not loop as continuous wall climbing. |
| Armed melee | Combat calls `PlayMeleeFeedback` after acceptance | MeleeLeft / Right and recoveries; existing UAL2 sword A/B motions |
| Unarmed melee | Same callback with no equipped weapon record | PunchLeft / Right; UAL1 `Punch_Jab`, `Punch_Cross` |
| Cast / hold / release / cancel | Combat timer, `PlayAbilityFeedback`, interruption observation | UAL1 `Spell_Simple_*`; animation cannot release a spell itself |
| Guard / channel / emote | Accepted structured ability intent | Existing Guard / Channel / Emote states |
| Interact | Successful service/door/lockpick/dialogue/shrine path | `PlayInteractionFeedback`; UAL1 `Interact` |
| Pickup / loot / resource | Existing accepted collection or loot method | `PlayInteractionFeedback(true)`; UAL1 `PickUp_Table` |
| Consume | Successful existing consumable transaction | `PlayConsumeFeedback`; UAL2 `Consume` |
| Equip / unequip | Changed accepted equipment signature, excluding initial bind | Native event-free `GrabWeaponR` / `PutAwayWeaponR` copies |
| Hit / death / revive | Existing vitals presentation callbacks | Hit / Death / return to Locomotion; UAL1 hit/death clips |

Actions.Empty must have zero layer weight at rest. Runtime raises the layer only for active actions and suppresses it during full-body dash/roll, climb and death. A state-name check alone cannot detect an unwanted pose retained by an empty override layer. Keep the finger grip separate and release it for interaction, pickup, consume and casting.

## What good motion must show

- **Idle:** relaxed, balanced, grounded posture; no unexplained forward hunch, raised combat arms, crossed knees or constant fidget. Inspect side and front views. Head-to-hips angle alone is insufficient.
- **Locomotion:** clear alternating strides, planted stance feet, natural cadence at the accepted motor speed, correct direction, clean acceleration/stop/turn. Check the rendered foot against world travel. Do not “fix” sliding by making legs cycle implausibly fast. `Jog_Fwd_Loop` is not a walking clip.
- **Actions:** readable anticipation, contact/reach, follow-through and recovery. Match the visible contact to gameplay timing. Avoid squeezing an entire long clip into a short dash window. Maintain motion during transitions; no stuck cast, queued attack or snap to bind pose.
- **First person:** visible authored hand/weapon motion, stable camera framing, intact wrists, no torso/head intrusions, no near-plane clipping. A body roll should not force a disorienting camera revolution. Inspect movement and each action at gameplay FOV.
- **First-person arm continuity:** derived geometry version 5 retains upper arms through fingers. Two-joint framing preserves bone lengths and places the shoulder cut behind the camera. Inspect anticipation, contact, follow-through and recovery; a forearm-only mesh can expose an open elbow when reach increases. The preview now samples nine phases per attack/reach/roll and renders five. Roll hands tuck below view during inversion and return on exit; camera aim stays unchanged.
- **Equipment:** weapon grip remains in the palm; paired boots and armor follow canonical animated bones. Preserve grip pivots when sizing. Cosmetic colliders are disabled. Imported wardrobe bind skeletons and Animators must not compete with the body.
- **Quality evidence:** render multiple phases, inspect them, then observe real-time motion in both views. A numerical stride check, static image or state-name check is only one part of acceptance.

## Debug the actual cause

| Symptom | Check first |
| --- | --- |
| Sliding / barely moving legs | Actual collision-resolved speed, blend weights, normalized-time progress, correct loaded clip, playback cadence, repeated Fall/Land transitions, other pose writers |
| Hunched or combat-ready idle | Direct source clip, then base layer alone, then Actions and Grip layers independently; inspect masks and transform curves as well as humanoid muscles |
| Straight sprint leans sideways | Measure signed head-to-hips bank across a full gait cycle. On this rig UAL1 Sprint_Loop averaged +13.96 degrees; the native Run averaged +0.13 degrees. Require less than 3 degrees of sustained bank, then inspect motion visually. |
| Armor frozen or detached | Count Animators, inspect weighted bone references, exclude imported wardrobe skeletons from the canonical map; inspect normal and delayed sizing passes |
| Wrong-direction strafe / yaw resets | Presentation yaw and later visibility-repair writes; keep motor/aim ownership intact |
| FP hand reset after equip or camera change | Stable ready-pose reference, mirrored bone lifecycle and authored grip socket parents |
| Gem or another unrelated object under cloak | Accepted item visual binding and binder compatibility; never rewrite the saved item to disguise a presentation mismatch |

The earlier raised-arm idle was traced to the Empty action layer retaining a pose at full weight. The current runtime fades that layer to zero. Keep `idle-layers-0/1/2.png` as a diagnostic if it regresses. Calibrate all three gait speeds before writing controller defaults, because each write invalidates a live preview Animator's bindings.

## Acceptance and reporting

Compile after a coherent patch. Inspect fresh diagnostics. Run the focused editor check and inspect renders. Start `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`, use ordinary Continue on the selected existing save, wait for presentation release, then run the bounded witness. Follow it with ordinary input and visual checks for the changed actions. Never reset a profile or spawn rewards merely to make a test pass.

Record build/source hashes, receipt UTC, tested action/view, actual result and missing evidence. Report compile, disposable preview, assisted runtime and ordinary gameplay separately. Stop a repeated repair loop after three unsuccessful attempts at the same cause. Preserve failed receipts or their details rather than overwriting the history with an unsupported success statement.

### October 3 verified checkpoint

The current controller passed 138 disposable humanoid checks at 04:28:09 UTC. The real PlaySafe motor witness passed 87 checks at 04:24:05 UTC after ordinary title Continue and player release. It preserved inventory and exercised physical locomotion, jump, dash, roll and first-person gestures. This is assisted runtime evidence; gameplay damage, actual potion transactions, wall climbing and mounted creature attachment are separate gates.

Mouse activation of the runtime menu produced a melee effect and the first witness failed its upright-idle precondition. That failure is preserved as `outputs/Player_Animation_20261002/Runtime_FAIL_20261003_042223.json`. Keyboard activation from settled idle passed without changing any limits. Prefer keyboard activation for this witness; do not infer a motion regression solely from menu input overlapping an attack.

The latest traversal preview passes numerical checks but visual review rejects the low climbing hand pose. The vault loop and the replacement in-place stroke have both failed visual acceptance. Keep that limitation visible rather than treating the preview receipt as professional motion certification. Rider framing is repaired and motions are prepared, but the production creature attachment owner is still missing.

DOT equipment integration is documented in `Docs/DOT_Equipment_Integration_2026-10-03.md`. The catalog and approved rigid generation assets are published; fitted garments remain excluded pending skeleton, body-mask, morph and cloth verification. Source and assembly identity for these runs is `outputs/DOT_Integration_20261002/verified-build-identity.json`.

Reference examples: [Quaternius library](https://quaternius.itch.io/universal-animation-library), [library 2](https://quaternius.itch.io/universal-animation-library-2), [Unity FPS Sample animation](https://github.com/Unity-Technologies/FPSSample/blob/master/Documentation/Animation.md), [Unity motion/animation coupling](https://docs.unity3d.com/ru/530/Manual/nav-CouplingAnimationAndNavigation.html). Use examples to judge timing, contacts and motion, then verify the result on this project's body and equipment.
