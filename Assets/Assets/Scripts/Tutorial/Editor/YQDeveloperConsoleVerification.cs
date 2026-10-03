#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: State-level command fixtures are intentionally detached; they do not create/load/reset a real profile or enter Play Mode.
[InitializeOnLoad]
public static class YQDeveloperConsoleVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/VerifyDeveloperConsoleBrowser.request";
    private static double _nextCheck;
    static YQDeveloperConsoleVerification() { EditorApplication.update += CheckRequest; }
    private static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + 2;
        if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request) || !SourcesAreFresh()) return;
        File.Delete(Request);
        RunContracts();
    }
    [MenuItem("Tools/YourQuest/Testing/Verify Developer Console Contracts")]
    public static void RunContracts()
    {
        if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || !SourcesAreFresh() || EditorApplication.isPlayingOrWillChangePlaymode || YQDeveloperTestSession.Active)
        { Debug.LogWarning("Console fixture requires fresh successful compilation, Edit Mode and no active test snapshot."); return; }
        var checks = new List<string>(); string failure = "";
        GameObject fixture = null;
        ProgressionBalanceConfig balance = null;
        try
        {
            fixture = new GameObject("Detached developer console fixture") { hideFlags = HideFlags.HideAndDontSave };
            fixture.SetActive(false);
            var manager = fixture.AddComponent<PlayerStateManager>();
            manager.state = new PlayerState { playerId = "console-verification-fixture" }; manager.state.EnsureCollections();
            var world = fixture.AddComponent<WorldStateManager>(); world.ReplaceState(WorldState.CreateDefault());
            var thinker = fixture.AddComponent<ProgressionThinkCycle>();
            var applier = fixture.AddComponent<ProgressionDecisionApplier>();
            balance = ScriptableObject.CreateInstance<ProgressionBalanceConfig>(); thinker.balance = balance;
            var content = fixture.AddComponent<GeneratedRpgContentService>();
            SkillData committed = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/GeneratedSkills/Committed/Battlefield Explorer.asset");
            Require(committed != null && committed.skillId == "d2726112a91448f69a1b1b0eb4b889af", "Actual committed skill asset/id", checks);
            manager.state.UpsertSkill(new SkillRecord { skillId = committed.skillId, familyId = committed.familyId,
                name = committed.skillName, description = committed.description, type = committed.type.ToString(), rank = 1, tier = 1, unlocked = false });
            world.State.npcs.Add(new WorldState.NpcRecord { npcId = "fixture-npc", name = "Detached NPC", affinityToPlayer = .2f });
            manager.state.AddOrUpdateItem(new InventoryItemRecord { itemId = "fixture-item", templateId = "fixture-stack", displayName = "Detached item", itemType = "consumable", stackable = true, quantity = 2 });
            var registry = new YQDeveloperCommandRegistry(manager, world, thinker, applier);
            var itemEntry = registry.Browse("Items").Find(e => e.Id == "fixture-item");
            Require(itemEntry != null && YQDeveloperConsole.MatchesBrowserSearch(itemEntry, "Detached") &&
                YQDeveloperConsole.MatchesBrowserSearch(itemEntry, "fixture-item") && !YQDeveloperConsole.MatchesBrowserSearch(itemEntry, "not-present"), "Browser searches canonical item names and IDs", checks);
            Require(registry.Browse("Skills").Exists(e => e.Id == committed.skillId) && registry.Browse("NPCs").Exists(e => e.Id == "fixture-npc"), "Browser uses authoritative skill and NPC IDs", checks);
            InventoryItemRecord previewItem = JsonConvert.DeserializeObject<InventoryItemRecord>(JsonConvert.SerializeObject(manager.state.inventoryItems[0]));
            previewItem.quantity = 3;
            Require(YQDeveloperCommandRegistry.CanAcceptBrowserItem(manager.state, previewItem, out _), "Generated item acceptance allows compatible complete stacks", checks);
            manager.state.inventoryItems[0].quantity = 98;
            Require(!YQDeveloperCommandRegistry.CanAcceptBrowserItem(manager.state, previewItem, out _), "Generated multi-unit stack cannot exceed shared maximum", checks);
            manager.state.inventoryItems[0].quantity = 2;
            previewItem.attackBonus = 9;
            Require(!YQDeveloperCommandRegistry.CanAcceptBrowserItem(manager.state, previewItem, out _), "Generated item cannot merge incompatible mechanics via legacy offer path", checks);
            previewItem.stackable = false; previewItem.quantity = 1; manager.state.inventoryCapacity = 1;
            Require(!YQDeveloperCommandRegistry.CanAcceptBrowserItem(manager.state, previewItem, out _), "Generated item acceptance respects owned inventory capacity", checks);
            manager.state.inventoryCapacity = 0;
            int auditCount = 0, events = 0; registry.Audit = _ => auditCount++;
            manager.DevelopmentStateChanged += _ => events++;
            void Pass(string command) { var r = registry.Execute(command); Require(r.Success, command + " => " + r.Text, checks); }
            void Reject(string command) { var r = registry.Execute(command); Require(!r.Success, "Reject " + command + " => " + r.Text, checks); }
            Pass("test snapshot");
            Reject("test snapshot");
            Reject("skill grant " + committed.skillId);
            Require(!manager.state.FindSkillById(committed.skillId).unlocked, "Normal acquisition fails without behavior evidence", checks);
            Pass("skill grant " + committed.skillId + " 3 --force");
            Require(manager.state.FindSkillById(committed.skillId).unlocked && manager.state.FindSkillById(committed.skillId).rank == 3, "Force grants without gameplay through canonical owner", checks);
            Pass("progression set skillCandidateScore 2 --force");
            Require(thinker.EffectiveSkillCandidateScore == 2 && balance.scoreForSkillCandidate == 24, "Effective learning threshold changes; authored balance unchanged", checks);
            Reject("progression set minimumScore 0");
            Pass("progression set minimumScore 0 --force");
            Pass("progression set minSkillEvidenceScore 0.1");
            Reject("progression set skillCooldown 5");
            Pass("progression set skillCooldown 5 --force");
            Pass("progression set repeatPenaltyPerSameVerb 0.2");
            Require(thinker.EffectiveSkillCooldownSeconds == 5 && balance.skillCooldown == 420 && thinker.balance != balance && balance.repeatPenaltyPerSameVerb == .15f,
                "Effective cooldown changes; authored balance protected by runtime clone for scorer settings", checks);
            Pass("stat set maxHealth 170");
            Require(events >= 2 && content.GetDerivedMaxHealth(manager.state) == 170, "Stat mutation emits owner notification; actual HUD derived-health service observes 170", checks);
            long beforeReject = manager.state.stateRevision;
            Reject("stat set critChance 2"); Reject("stat set strength 1.5"); Reject("stat set maxHealth NaN"); Reject("stat add currency -1"); Reject("stat set level 999");
            Require(manager.state.stateRevision == beforeReject, "Invalid numeric values do not mutate state/revision", checks);
            Reject("skill grant " + committed.skillId + " --eval"); Reject("skill xp add " + committed.skillId + " 5");
            Pass("stat add xp 100"); Require(manager.state.level == 2 && manager.state.xp == 0, "XP uses actual GrantXp level-up path", checks);
            Pass("item give fixture-item 3"); Require(manager.state.FindInventoryItemById("fixture-item").quantity == 5, "Accepted inventory stacking path", checks);
            Pass("item remove fixture-item 1");
            Reject("item give fixture-item 100");
            manager.state.inventoryCapacity = 1;
            manager.state.FindInventoryItemById("fixture-item").quantity = YQContainerInventory.MaxStack;
            Reject("item give fixture-item 1");
            Pass("target npc fixture-npc"); Pass("stat set affinityToPlayer 0.7");
            Reject("stat set strength 99"); Reject("skill grant " + committed.skillId + " --force");
            Require(registry.Target.Contains("fixture-npc") && world.State.npcs[0].affinityToPlayer == .7f, "NPC target is canonical NpcRecord; player commands rejected", checks);
            Require(registry.Complete("stat set affinity").Length == 1, "NPC autocomplete exposes its actual variable", checks);
            Reject("target npc unknown");
            Require(!manager.TrySave(out _) && !world.TrySave(out _) && YQDeveloperConsoleGate.BlocksPersistence, "Save barrier rejects actual player/world save entry points", checks);
            Pass("test restore");
            Require(manager.state.stats.maxHealth == 100 && manager.state.level == 1 && !manager.state.FindSkillById(committed.skillId).unlocked &&
                world.State.npcs[0].affinityToPlayer == .2f && manager.state.FindInventoryItemById("fixture-item").quantity == 2, "Snapshot restores canonical player/NPC/inventory state", checks);
            Require(manager.state.equippedSkillBySlot.Comparer.Equals(StringComparer.OrdinalIgnoreCase) && manager.state.stateRevision > beforeReject,
                "Restoration retains dictionary comparer and monotonic revisions", checks);
            Require(thinker.EffectiveSkillCandidateScore == 24 && thinker.EffectiveMinimumScore == 12 && thinker.balance == balance &&
                applier.minSkillEvidenceScore == .34f && !YQDeveloperConsoleGate.BlocksPersistence, "All transient rules restored and save barrier released", checks);
            Require(!YQDeveloperConsoleGate.AllowsBuild(false, false, true) && !YQDeveloperConsoleGate.AllowsBuild(false, true, false) &&
                YQDeveloperConsoleGate.AllowsBuild(false, true, true) && YQDeveloperConsoleGate.AllowsBuild(true, false, false), "Public/opt-in/development/editor build gate matrix", checks);
            Pass("target player");
            string quotedBrief = "a \"storm\" sigil \\ path";
            Require(YQDeveloperCommandRegistry.Tokenize("generate item " + YQDeveloperCommandRegistry.QuoteArgument(quotedBrief))[2] == quotedBrief,
                "Guided request quotes/backslashes round-trip through typed parsing", checks);
            Reject("generate quest \"\""); Reject("generate item \"detached request\""); Reject("spawn monster unknown 9");
            Pass("test snapshot");
            manager.state.pendingOffers.Add(new PendingProgressionOfferRecord { offerId = "fixture-quest-offer", offerKind = "quest", name = "Detached structured quest", offerState = "pending",
                payloadJson = "{\"objective\":{\"type\":\"wait_seconds\",\"requiredCount\":3,\"description\":\"Wait three seconds\"}}" });
            Require(registry.Browse("Quests").Exists(e => e.Id == "fixture-quest-offer" && e.Pending), "Generated quest offers appear separately for review", checks);
            Pass("offer accept fixture-quest-offer");
            Require(manager.state.quests.Exists(q => q.name == "Detached structured quest" && q.objectives.Count == 1 && q.objectives[0].type == "wait_seconds"), "Guided Add uses canonical structured quest acceptance", checks);
            Reject("offer accept fixture-quest-offer");
            bool cleaned = false;
            YQDeveloperTestSession.OnTemporarySceneChange(() => cleaned = true);
            Require(!YQDeveloperTestSession.Persist(out string persistenceError) && persistenceError.Contains("Temporary test encounters"), "Temporary encounter blocks normal profile publication", checks);
            Pass("test restore");
            Require(cleaned && !manager.state.quests.Exists(q => q.name == "Detached structured quest") && !YQDeveloperTestSession.Active,
                "Restore removes accepted test quest and invokes temporary encounter cleanup", checks);
            Require(YQDeveloperCommandRegistry.Tokenize("target npc \"fixture npc\"")[2] == "fixture npc", "Typed tokenizer preserves quoted identifiers", checks);
            Require(registry.Complete("stat set max").Length == 3 && auditCount > 20, "Identifier autocomplete and mutation/result audit", checks);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError("[Developer Console Verification] " + failure); }
        finally
        {
            if (YQDeveloperTestSession.Active) YQDeveloperTestSession.Restore(out _);
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            if (balance != null) UnityEngine.Object.DestroyImmediate(balance);
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/Developer_Console_Contract_Receipt.json", JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow.ToString("O"), status = failure.Length == 0 ? "PASS" : "FAIL", evidence = "Edit Mode detached state-level command fixture",
                checks, failure, runtime = "NOT VERIFIED: ordinary PlaySafe overlay, physical input capture, visible HUD and built-player gating",
                assembly = typeof(YQDeveloperCommandRegistry).Assembly.ManifestModule.ModuleVersionId.ToString()
            }, Formatting.Indented));
        }
    }
    private static void Require(bool condition, string label, List<string> checks)
    { if (!condition) throw new InvalidOperationException(label); checks.Add("PASS " + label); }

    private static bool SourcesAreFresh()
    {
        // note: Never publish a PASS from the previous loaded assembly while new source is queued or another integration has compiler errors.
        DateTime runtimeBuilt = File.GetLastWriteTimeUtc(typeof(YQDeveloperCommandRegistry).Assembly.Location);
        DateTime editorBuilt = File.GetLastWriteTimeUtc(typeof(YQDeveloperConsoleVerification).Assembly.Location);
        foreach (string path in Directory.GetFiles("Assets/Assets/Scripts/Tutorial", "YQDeveloper*.cs"))
            if (File.GetLastWriteTimeUtc(path) > runtimeBuilt) return false;
        return File.GetLastWriteTimeUtc("Assets/Assets/Scripts/Tutorial/Editor/YQDeveloperConsoleVerification.cs") <= editorBuilt;
    }
}
#endif
