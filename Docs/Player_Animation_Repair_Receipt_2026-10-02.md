# Player animation repair — October 2, 2026

## Change

**Current checkpoint, 2026-10-02T14:48Z:** the follow-up for straight sprint bank and first-person attacks/rolls is integrated; native Run and derived arm geometry version 5 are applied. Combined compilation, 108 main controller checks and 28 main first-person preview checks passed. Live straight sprint and first-person attacks passed within a partial witness; the complete live sequence did not pass. A short roll-only check is prepared for the UI chat's next combined compile and normal Continue session. Start future work with `Player_Animation_Workflow.md` and `Tools/player-animation-status.ps1`.

The gait now uses the body author's `Idle`, `Walk`, `Run`, and `WalkBack`. Quaternius `Sprint_Loop` was rejected after the user reported a persistent right lean: full-cycle sampling measured +13.96 degrees of lateral torso bank, compared with +0.13 degrees for native Run. Its measured stance speed is 3.1437855 m/s before playback scaling. First person retains a combat-ready idle through a view parameter. Existing third-person armed attacks are preserved. Authored crouch, traversal, cast, hit and death motions remain, with added punches, interaction, pickup, consuming, equipment gestures and crouch+dash roll states.

The initial integrated authored pass failed visible locomotion acceptance: the user reported sliding, barely moving legs, and a hunched idle. Earlier state-entry checks did not establish animation quality. This follow-up measures actual foot travel and leg rotation on the approved body and adds the same checks to the production motion witness.

The user selected natural movement speeds: walking 1.8 m/s, running 5.4 m/s, crouching 1.1 m/s before existing bonuses. The previous 6.8 m/s walk and 11.25 m/s run outran the authored human strides. Saved speed stats and item bonuses remain accepted records; their previous 6.8 baseline is retained when calculating positive stat bonuses. Walk/run blend thresholds now map to the motor's actual pace, and playback observes measured stance-foot speed. A short descending contact grace prevents brief slope contact gaps from repeatedly restarting airborne poses. Visibility recovery preserves the body yaw instead of clearing it in LateUpdate.

Native armor's bind skeleton is no longer mistaken for a second player. Its imported Animator is disabled and removed before canonical bone rebinding; the bone map excludes equipment skeletons. A left boot binding can use its existing approved right boot partner. Both views prefer the body's authored grip sockets and preserve a weapon's pivot through initial and delayed sizing, instead of putting the blade's bounds center in the palm.

The selected gem asset was incorrectly bound to the cloak slot and centered beneath the feet. The inspected live gem root had no collider. The project binder now rejects that incompatible accessory presentation without changing the accepted cloak record. No authored cloak/cape prefab or model was found by targeted filename searches, so that cloak currently has an identifiable missing visual. Cosmetic equipment colliders are disabled immediately and removed, including inactive children; the motor's capsule remains authoritative.

Casting enters a charging pose, holds while `YQInvestorCombat.IsCasting` is true, releases when its timer completes, and returns directly to ready on interruption. Animation reads the combat timer and cannot commit a spell or spend resources. Movement parameters read collision-resolved displacement, and root motion remains disabled.

First-person arms are compact derivatives of the approved male body, with its existing skinning and materials. A view mirrors the single body Animator's evaluated bones after animation and frames the authored hand arcs within camera reach. Weapons and shields attach to these hands. The view has no Animator, input, movement, colliders, health, combat, inventory or save authority. Camera changes use a stable ready-pose reference, and equipment refresh preserves the arm skeleton.

The follow-up retains upper-arm geometry and solves both arm joints at authored lengths with shoulder cuts behind the camera. The earlier forearm-only geometry exposed open elbows when attack reach increased. Attacks and interactions now have larger action-specific reach; dash/landing brace the arms and rolls tuck them through inversion then recover, without rotating the camera. Existing derived mesh subasset references are updated in place. Nine-phase sampling checks every armed/unarmed strike, interaction, pickup, consume and roll; five phases per action are rendered for review.

## Files

- `Assets/Assets/Scripts/Tutorial/YQPlayerEquipmentVisual.cs`
- `Assets/Assets/Scripts/Tutorial/YQFirstPersonArmsAssets.cs`
- `Assets/Assets/Scripts/Tutorial/YQFirstPersonArmsView.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQPlayerAnimationAssets.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQAuthoredPlayerAnimations.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQPlayerAnimationRuntimeVerification.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQPlayerLocomotionRepair.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQPlayerActionAnimationSetup.cs`
- `Assets/Assets/Scripts/Tutorial/Editor/YQPlayerActionAnimationVerification.cs`
- `Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs`
- `Assets/Assets/Scripts/Tutorial/YQInvestorCombat.cs`
- `Assets/Assets/Scripts/Tutorial/YQInvestorWorldPickup.cs`
- `Assets/Assets/Scripts/Tutorial/YQInvestorLootableCorpse.cs`
- `Assets/Assets/Scripts/Generated/GeneratedRpgContentService.cs`
- `Assets/Assets/Resources/Player/YQPlayer.controller`
- `Assets/Assets/Resources/Player/YQFirstPersonArms.asset`
- `Assets/Assets/Art/PlayerAnimation/Quaternius/`

The earlier motor/vitals presentation hooks remain. This integration does not replace the other chats' combat, UI, startup or road changes. The controller GUID remains `151a16946511aff48bf0a723cb3119c7`. Existing scene/prefab GUIDs, body materials, native-pack import settings, save contracts and accepted records are preserved. The project-added Quaternius adapter uses body-forward root orientation after isolated rendering showed that Original made the actual clips face backward.

The idle regression had a separate cause: the Actions layer at weight one retained a combat pose even in its Empty state. The staged runtime fades that layer out at rest. Clip selection also admitted hidden `__preview__` subassets; the repair excludes them and replaces stale references with the actual authored clips. The old reload-time gait rewrite is removed so later authoring changes require explicit setup.

The new roll uses the motor's existing stamina/cooldown/collision path, 3.2 m over 0.6 seconds, and a low capsule. Plain dash remains separate. Interaction, loot and consume gestures observe successful gameplay methods; animation does not commit inventory or resource changes. New action states return to Empty. No new gameplay authority or invulnerability is introduced.

## Sources

- [Quaternius Universal Animation Library](https://quaternius.itch.io/universal-animation-library)
- [Quaternius Universal Animation Library 2](https://quaternius.itch.io/universal-animation-library-2)
- [Unity FPS Sample animation guidance](https://github.com/Unity-Technologies/FPSSample/blob/master/Documentation/Animation.md)
- [Unity directional blend trees](https://docs.unity.com/en-us/engine/6000.6/manual/animation-section/animation-mecanim/animation-animator-controller/animation-state-machines/animation-blend-trees/blend-tree-2dblending)
- [Unity coupling resolved movement and animation](https://docs.unity3d.com/ru/530/Manual/nav-CouplingAnimationAndNavigation.html)

The free Standard archives supplied the imported Unity FBX files. Their original CC0 1.0 notices are retained alongside the files. Existing imported body assets were not edited.

## Verification boundary

- **PASS — isolated Unity Editor, 2026-10-02T13:40:07.3471273Z:** 19 authored body/first-person checks including roll-state availability, arm skinning, one animation owner, cast hold/release/interruption and death/revival framing. A separate isolated check exercised entry/recovery for 13 action states and the full-body roll and rendered four phases of each. This is asset/presentation evidence, not production acceptance.
- **PASS — isolated gait calibration:** final prepared defaults are walk foot speed 1.1394 m/s, run 4.9256 m/s, backward 0.7295 m/s. Correct facing changes the run's stance measurement materially; do not reuse the earlier hidden-preview calibration.
- **PASS — integrated compilation and domain reload, 2026-10-02T14:03Z:** fresh Unity build success and assembly reload; no compiler errors. Runtime assembly SHA256 `8F5AE08C222875D2CABA0767FA6DE6E73AF861579D692C010F4B76597154DBD6`; Editor assembly `5D12FF5710A57D3E5266467AA18EB6E77971CEB9ABB49DA2259286B1E6B1FB91`. Other chats' later changes can supersede these shared assemblies.
- **PASS — integrated controller, 2026-10-02T14:05:28.4990953Z:** 108 checks after the explicit authored setup. Includes the added roll/actions, event-free owned clips, humanoid evaluation, action entry/recovery and root-motion ownership. Evidence: `Player_Animation_Editor_Receipt_2026-10-01.json` (use its internal UTC).
- **HISTORICAL — body sampling:** the 12:49 nine-check gait receipt passed numerical stride/lean thresholds but still rendered combat-ready arms. It did not establish visible quality.
- **HISTORICAL FAIL — 2026-10-02T12:52:18.9799333Z:** phase zero found multiple Animators inside the imported wardrobe hierarchy. This is superseded by the successful current-build live witness below.
- **PASS — expanded production PlaySafe witness, 2026-10-02T14:12:36.8631999Z:** ordinary Continue loaded the existing profile `1df9084b1a254ea5be0c4c030105ad7e`. All 83 checks passed on runtime assembly `8F5AE08C222875D2CABA0767FA6DE6E73AF861579D692C010F4B76597154DBD6`. Forward/backward/left/right travel showed continuous locomotion and approximately 0.59–0.60 m of foot travel. Sprint showed 0.91 m of foot travel at playback rate 1.098. The motor accepted crouch, jump/landing, simple dash and crouch-dash roll. Idle returned after deceleration. The production hierarchy had one Animator, canonical weighted armor bones, paired boots and no enabled cosmetic colliders. First-person arms had no Animator/colliders, and melee/cast/hit/interact/pickup/consume gestures entered/recovered. Inventory/equipment records and original camera mode were preserved. Evidence: `Player_Animation_Runtime_Receipt_2026-10-02.json`.
- **Evidence limit:** synthetic keyboard input drove the real motor. Interaction, pickup, consume, melee, cast and hit phases called presentation callbacks; those phases did not perform actual item transactions, commit attacks, damage the player or consume an item. Live screenshots showed the upright dressed third-person body and first-person weapon/gesture framing. Isolated multi-phase renders cover the remaining authored poses.
- **PASS — isolated follow-up, 2026-10-02T14:34:12Z:** 28 authored/first-person checks. Armed camera-space hand arcs 0.497/0.489 m, unarmed 0.165/0.117 m; roll tuck/recovery passed. Reviewed attack and roll phase renders show continuous arms. This is presentation preview evidence.
- **PASS — isolated native gait, 2026-10-02T14:36:21Z:** 13 checks; mean run bank +0.129 degrees, foot travel 0.770 m and upper-leg arc 53.78 degrees. The calibration warning is repaired by collecting all measurements before updating controller defaults; no missing-controller warning occurred in this run.
- **PASS — main follow-up compile, 2026-10-02T14:42Z:** Tundra build success and domain reload. Runtime SHA256 `E78A4E5121048DB6B0669A4B8E1CFED6D7F2A62EEA582F3738803DF0E239A911`; Editor SHA256 `FEC2FEBF64B120B9A9A607B8BF55A7701F773067F1185F7B9A3D8DA4749ABE29`. Main controller checks passed at 14:39:18Z and 28 main authored/first-person preview checks at 14:39:55Z.
- **PARTIAL — live follow-up at 14:45:20Z:** normal Continue, physical direction/stride checks and first-person melee/cast/hit checks passed. Sprint averaged -0.152 degrees of lateral torso bank, 0.800 m foot travel, 43/43 sampled frames in locomotion, playback 1.721. Sequence failed at crouch-dash because no roll/crouch/movement was observed; cause not proven. Preserved as `Player_Animation_Action_Preparation_2026-10-02/first-person-run-evidence/first-followup-roll-not-observed.json`.
- **FAIL — second live sequence at 14:46:54Z:** repeated travel crossed a steep terrain dip under a bridge; sprint was in locomotion for 13/27 moving frames and descended 2.61 m. This does not establish a flat-ground gait regression. Failure preserved in `second-followup-terrain-locomotion.json`; do not repeatedly move the long witness through unsuitable terrain. Roll-only verification from grounded rest remains pending.
- **UNVERIFIED — complete ordinary gameplay flow:** the assisted witness does not certify actual damage/death/respawn, climbing against a surface, all equipment variants, every generated spell circle, or a standalone player build.

The October 1 receipts describe the earlier animation build and must not be used as evidence for this quality pass. The casting chat's earlier live spell-release/cancellation checks preceded this animation integration.
