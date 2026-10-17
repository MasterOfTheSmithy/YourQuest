using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class YQPlayerMovementPassVerification
{
    [MenuItem("YourQuest/Player Animation/Verify Movement and Combat Timing Pass %#F9")]
    public static void Run()
    {
        var checks = new List<string>();
        string output = "outputs/Player_Movement_20261007";
        Directory.CreateDirectory(output);
        try
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                float speed = 25f;
                for (int i = 0; i < fps; i++) speed = YQPlayerMotionRules.BrakeSpeed(speed, 12f, 2.5f, 1f / fps);
                Require(Mathf.Abs(speed - YQPlayerMotionRules.BrakeSpeed(25f, 12f, 2.5f, 1f)) < .001f, "slide braking matches analytical integration at " + fps + " FPS", checks);
            }
            Require(YQPlayerMotionRules.BrakeSpeed(2f, 12f, 2.5f, 1f) == 0f, "slide tail stops rather than reversing or drifting", checks);
            Require(YQPlayerMotionRules.BrakeSpeed(25f, 12f, 2.5f, 0f) == 25f, "paused integration preserves speed", checks);
            Require(YQPlayerMotionRules.FallDamage(2f, 30f, 3f, 22f, 12f, 35f, 100f) == 0f, "short fall is safe", checks);
            Require(YQPlayerMotionRules.FallDamage(12f, 5f, 3f, 22f, 12f, 35f, 100f) == 0f, "a gently caught high fall is safe", checks);
            float damaging = YQPlayerMotionRules.FallDamage(8f, 20f, 3f, 22f, 12f, 35f, 100f);
            Require(damaging > 0f && damaging < 100f, "moderate fall damages without guaranteed death", checks);
            Require(YQPlayerMotionRules.FallDamage(25f, 38f, 3f, 22f, 12f, 35f, 100f) >= 100f, "extreme landing is lethal without a clutch", checks);
            Require(YQPlayerMotionRules.ClutchHealth(100f, 100f, 0f) == 1f, "low-skill fatal clutch retains one HP", checks);
            Require(YQPlayerMotionRules.ClutchHealth(20f, 100f, 1f) == 20f, "clutch never heals above pre-impact health", checks);
            Require(YQPlayerMotionRules.ClutchHealth(100f, 100f, 1f) == 65f, "high acrobatics retains bounded health", checks);
            Require(YQPlayerMotionRules.ClutchDamage(10f, 100f, 100f, 100f, 0f) == 10f, "clutch cannot worsen a moderate injury", checks);
            Require(YQPlayerMotionRules.InClutchWindow(10f, 9.7f, 10f, .4f), "pre-impact input buffer qualifies", checks);
            Require(YQPlayerMotionRules.InClutchWindow(10f, 10.3f, 10.3f, .4f), "post-impact input qualifies", checks);
            Require(!YQPlayerMotionRules.InClutchWindow(10f, 9.5f, 10f, .4f), "old roll press is rejected", checks);
            Require(!YQPlayerMotionRules.InClutchWindow(10f, 10.5f, 10.5f, .4f), "late roll press is rejected", checks);
            Require(!YQPlayerMotionRules.InClutchWindow(10f, 10f, 11f, .4f), "expired landing cannot resolve another clutch", checks);
            var state = new PlayerState(); state.EnsureCollections();
            Require(YQInvestorPlayerMotor.Acrobatics(state) == 0f, "old saves without acrobatics receive zero mastery", checks);
            state.IncCounter("skill:acrobatics:xp", 50f);
            Require(Mathf.Abs(YQInvestorPlayerMotor.Acrobatics(state) - .5f) < .001f, "canonical behavior progression supplies acrobatics", checks);
            var sword = new InventoryItemRecord { itemId = "test_sword", equipSlot = "weapon", itemType = "weapon", familyKey = "sword" };
            state.inventoryItems.Add(sword); state.equippedItemBySlot["weapon"] = sword.itemId;
            Require(YQInvestorCombat.ResolveAttackKind(state, sword) == YQPlayerAttackKind.OneHanded, "one-hand equipment selects one-hand action", checks);
            state.equippedItemBySlot["offhand"] = sword.itemId;
            Require(YQInvestorCombat.ResolveAttackKind(state, sword) == YQPlayerAttackKind.TwoHanded, "shared hand occupancy selects two-hand action", checks);
            sword.familyKey = "weapon:bow";
            Require(YQInvestorCombat.ResolveAttackKind(state, sword) == YQPlayerAttackKind.Ranged, "structured bow family selects ranged action", checks);
            sword.familyKey = "sword"; sword.displayName = "Narrative Bow of Dreams";
            state.equippedItemBySlot.Remove("offhand");
            Require(YQInvestorCombat.ResolveAttackKind(state, sword) == YQPlayerAttackKind.OneHanded, "narrative name cannot turn melee gear into a bow", checks);
            Require(YQInvestorCombat.ResolveAttackKind(state, null) == YQPlayerAttackKind.Unarmed, "empty hand selects unarmed action", checks);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Assets/Resources/Player/YQPlayer.controller");
            foreach (string name in new[] { "MeleeLeft", "MeleeRight", "PunchLeft", "PunchRight", "TwoHandAttack", "RangedAttack" })
            {
                AnimatorState action = Find(controller.layers[1].stateMachine, name);
                Require(action != null && action.motion != null && action.timeParameterActive && action.timeParameter == "ActionPhase", name + " follows combat phase", checks);
                Require(action.motion is AnimationClip clip && clip.events.Length == 0 && clip.humanMotion, name + " uses event-free humanoid motion", checks);
            }
            foreach (string name in new[] { "PowerSlideStart", "PowerSlide", "PowerSlideEnd", "GetUp", "Climb", "Swim" })
                Require(Find(controller.layers[0].stateMachine, name)?.motion != null, name + " has bound motion", checks);
            Require(AssetDatabase.LoadAssetAtPath<YQPlayerActionAssets>("Assets/Assets/Resources/Player/YQPlayerActionAssets.asset")?.arrowPrefab != null, "approved native arrow binding is present", checks);
            Write(output, "STATIC_EDITOR_PASS", checks, null);
            Debug.Log("[YQMovementPass] " + checks.Count + " pure/controller checks passed. PlaySafe behavior and visual feel remain unverified.");
        }
        catch (Exception exception)
        { Write(output, "FAIL", checks, exception.ToString()); Debug.LogException(exception); }
    }

    private static void Write(string output, string status, List<string> checks, string failure)
    {
        // note: Timestamped receipts preserve failures and clearly distinguish pure/editor evidence from live gameplay.
        string file = Path.Combine(output, "Verification_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(file, "UTC=" + DateTime.UtcNow.ToString("o") + "\nSTATUS=" + status + "\nRUNTIME=NOT_VERIFIED\nVISUAL=NOT_VERIFIED\n" + string.Join("\n", checks) + "\n" + failure);
    }
    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    { foreach (var state in machine.states) if (state.state.name == name) return state.state; return null; }
    private static void Require(bool condition, string description, List<string> checks)
    { if (!condition) throw new InvalidOperationException(description); checks.Add(description); }
}
