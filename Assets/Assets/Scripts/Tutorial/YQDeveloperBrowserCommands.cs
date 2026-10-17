#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

// note: Browser entries describe canonical records, not copies used as gameplay state.
public sealed class YQDeveloperBrowserEntry
{
    public string Id, Name, Summary, Detail, Kind, Value;
    public bool Owned, Pending;
    public int Layer;
}

public sealed partial class YQDeveloperCommandRegistry
{
    private readonly Dictionary<string, YQInvestorEnemySpawner> _encounters = new Dictionary<string, YQInvestorEnemySpawner>();
    public Action<YQDeveloperCommandResult> Completed;
    public bool GenerationPending { get; private set; }
    public bool TargetsPlayer => _npcId == null && _actorId == null;
    public long BrowserRevision => State?.stateRevision ?? -1;
    public static string QuoteArgument(string value) => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private void RegisterBrowserCommands()
    {
        Register("offer accept", 1, 1, true, "offer accept <pendingOfferId> : add a validated item/skill/quest offer through normal acceptance", AcceptBrowserOffer);
        foreach (string kind in new[] { "item", "skill", "spell", "quest", "title", "class" })
        {
            string category = kind;
            Register("generate " + kind, 1, 2, true, "generate " + kind + " \"<brief 1..400 characters>\" [--force] : pending proposal; explicit test evidence/context override", a =>
            { if (a.Count == 2 && a[1] != "--force") return Fail("Only --force is supported."); return GenerateBrowserOffer(category, a[0], a.Count == 2); });
        }
        Register("spawn monster", 1, 2, true, "spawn monster <loadedEncounterId> [count 1..8] : temporary encounter using an existing approved spawner; restore before persistence", SpawnBrowserEncounter);
    }

    public List<YQDeveloperBrowserEntry> Browse(string section)
    {
        var entries = new List<YQDeveloperBrowserEntry>();
        if (TryBrowseToolkit(section, entries)) return entries;
        if (section == "Commands")
        {
            foreach (Command command in _commands)
                entries.Add(new YQDeveloperBrowserEntry { Id = command.Path, Name = command.Path, Summary = command.Mutation ? "Test mutation" : "Inspect / session control", Detail = command.Help, Kind = "command" });
        }
        else if (section == "NPCs")
        {
            foreach (var npc in WorldManager?.State?.npcs ?? new List<WorldState.NpcRecord>())
                if (npc != null) entries.Add(new YQDeveloperBrowserEntry { Id = npc.npcId, Name = npc.name, Kind = "npc", Summary = npc.status + " / " + npc.locationId,
                    Detail = npc.description + "\nFaction: " + npc.factionId + "\nLocation: " + npc.locationId + "\nAffinity: " + npc.affinityToPlayer + " [-1..1]\nSelect as target to inspect or adjust affinity. Spawn provides temporary copies of accepted resident templates." });
        }
        else if (section == "Monsters" && !_fixture && Application.isPlaying)
        {
            _encounters.Clear();
            // note: Only loaded gameplay spawners provide approved models/factions; no arbitrary prefab or asset paths.
            foreach (var spawner in UnityEngine.Object.FindObjectsByType<YQInvestorEnemySpawner>(FindObjectsSortMode.None))
            {
                if (spawner.name.StartsWith("Developer encounter", StringComparison.Ordinal)) continue;
                string id = "encounter:" + spawner.GetInstanceID();
                _encounters[id] = spawner;
                entries.Add(new YQDeveloperBrowserEntry { Id = id, Name = spawner.enemyDisplayName, Kind = "monster", Summary = spawner.factionId + " / " + spawner.semanticRegionId,
                    Detail = "Approved model: " + spawner.enemyPrefabPath + "\nFaction: " + spawner.factionId + "\nRegion: " + spawner.semanticRegionId + "\nSpawn a temporary encounter near the player. Origin completion and grounded placement remain required. Encounter ID is valid only while this source is loaded. Restore removes spawned enemies/corpses and test inventory changes." });
            }
        }
        else if (State != null)
        {
            State.EnsureCollections();
            if (section == "Items")
                foreach (var item in State.inventoryItems.Where(i => i != null))
                    entries.Add(new YQDeveloperBrowserEntry { Id = item.itemId, Name = item.displayName, Kind = "item", Owned = true, Summary = item.itemType + " / " + item.rarity + " / x" + item.quantity,
                        Detail = item.description + "\nTemplate: " + item.templateId + "\nSlot: " + item.equipSlot + "\nAttack: " + item.attackBonus + "  Defense: " + item.defenseBonus + "\nHealth: " + item.healthBonus + "  Mana: " + item.manaBonus + "\nStackable: " + item.stackable + "; max stack " + YQContainerInventory.MaxStack });
            if (section == "Skills")
            {
                RefreshSkills();
                foreach (SkillRecord skill in _catalog.Values)
                    entries.Add(new YQDeveloperBrowserEntry { Id = skill.skillId, Name = skill.name, Kind = "skill", Value = (State.FindSkillById(skill.skillId)?.rank ?? skill.rank).ToString(), Owned = State.FindSkillById(skill.skillId)?.unlocked == true,
                        Summary = skill.type + " / rank " + skill.rank + (YQSpellCircleRules.IsSpell(skill) ? " / Circle " + skill.tier : "") + (State.FindSkillById(skill.skillId)?.unlocked == true ? " / unlocked" : " / available"),
                        Detail = skill.description + "\nContext: " + skill.context + "\nPrerequisite parent: " + skill.parentSkillId + "\nFamily: " + skill.familyId + "\nNormal Add checks actual acquisition requirements. Explicit Force test bypasses them and reports each bypass in Results." });
            }
            if (section == "Quests")
                foreach (var quest in State.quests.Where(q => q != null))
                    entries.Add(new YQDeveloperBrowserEntry { Id = quest.questId, Name = quest.name, Kind = "quest", Owned = true, Summary = quest.status,
                        Detail = quest.description + "\nRewards: " + quest.rewardXp + " XP / " + quest.rewardGold + " gold\n" + string.Join("\n", (quest.objectives ?? new List<QuestObjectiveRecord>()).Where(o => o != null).Select(o => o.type + " / " + o.targetId + " / required " + o.requiredCount)) });
            if (section == "Titles")
                foreach (var title in State.titles.Where(t => t != null)) entries.Add(new YQDeveloperBrowserEntry { Id = title.titleId, Name = title.name, Kind = "title", Owned = true, Summary = "Earned title", Detail = title.description });
            foreach (var offer in State.pendingOffers.Where(o => o != null && o.IsPending))
                if ((section == "Items" && offer.offerKind == "item") || (section == "Quests" && offer.offerKind == "quest") ||
                    (section == "Skills" && (offer.offerKind == "skill" || offer.offerKind == "spell")) || (section == "Titles" && (offer.offerKind == "title" || offer.offerKind == "class")))
                    entries.Add(new YQDeveloperBrowserEntry { Id = offer.offerId, Name = offer.name, Kind = offer.offerKind, Pending = true,
                        Summary = "PENDING OFFER / " + offer.offerKind, Detail = offer.description + "\nReason: " + offer.reason + "\nConfidence: " + offer.confidence + "\nStructured proposal: " + offer.payloadJson + "\nAdd accepts this offer through the existing player-choice service." });
        }
        return entries.OrderByDescending(e => e.Pending).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private YQDeveloperCommandResult AcceptBrowserOffer(Arguments a)
    {
        RequirePlayer();
        var offer = State.FindOfferById(a[0]);
        if (offer == null || !offer.IsPending) return Fail("Pending offer no longer exists. Refresh the list.");
        if (offer.offerKind == "skill" || offer.offerKind == "spell") return Grant(a);
        if (offer.offerKind != "item" && offer.offerKind != "quest" && offer.offerKind != "title" && offer.offerKind != "class") return Fail("Unsupported pending offer kind.");
        if (offer.offerKind == "item")
        {
            var content = GeneratedRpgContentService.Instance;
            if (content == null) return Fail("Item binding service unavailable.");
            // note: The legacy offer merger matches template/type only. Preflight the shared capacity/stack invariants before normal acceptance.
            string itemType = (offer.skillType ?? "").Trim().ToLowerInvariant();
            InventoryItemRecord preview = content.GenerateItem("offer:" + offer.offerId + ":" + itemType, Mathf.Max(1, State.level), itemType, itemType == "consumable");
            if (preview == null) return Fail("Item offer could not be materialized.");
            preview.displayName = offer.name; preview.description = offer.description; preview.familyKey = "player_response:" + itemType;
            if (!CanAcceptBrowserItem(State, preview, out string error)) return Fail(error);
        }
        Session();
        bool success = State.AcceptOffer(offer.offerId, out string message);
        if (success) Manager.DevelopmentNotify(message);
        return From(success, "SESSION " + message);
    }

    public static bool CanAcceptBrowserItem(PlayerState player, InventoryItemRecord preview, out string error)
    {
        // note: Generated consumables can contain several units. Check the whole addition, including the exact final identity/mechanics.
        error = "Invalid generated item quantity or state.";
        if (player?.inventoryItems == null || preview == null || preview.quantity < 1 ||
            (!preview.stackable && preview.quantity != 1) || (preview.stackable && preview.quantity > YQContainerInventory.MaxStack)) return false;
        var existing = player.inventoryItems.Find(i => i != null && i.stackable && preview.stackable &&
            string.Equals(i.templateId?.Trim(), preview.templateId?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.itemType?.Trim(), preview.itemType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null && (!YQContainerInventory.CanStack(existing, preview) || (long)existing.quantity + preview.quantity > YQContainerInventory.MaxStack))
        { error = "Offer would violate compatible-stack rules; free a stack before accepting."; return false; }
        if (existing == null && player.inventoryCapacity > 0 && player.inventoryItems.Count >= player.inventoryCapacity)
        { error = "Inventory capacity reached; offer remains pending."; return false; }
        error = ""; return true;
    }

    private YQDeveloperCommandResult GenerateBrowserOffer(string kind, string brief, bool force = false)
    {
        RequirePlayer();
        if (string.IsNullOrWhiteSpace(brief) || brief.Length > 400) return Fail("Describe the request in 1..400 characters.");
        var llm = LLMClient.Instance; var applier = Applier; var thinker = Thinker;
        if (_fixture || llm == null || applier == null || thinker == null) return Fail("Live LLM and progression services required; no placeholder is generated.");
        if (GenerationPending) return Fail("A console generation request is already pending.");
        if (!force && !string.IsNullOrWhiteSpace(_fakeContext)) return Fail("Fake context is set. Clear it or explicitly use --force for a test proposal.");
        if (State.pendingOffers.Count(o => o != null && o.IsPending) >= Mathf.Max(1, thinker.maxPendingOffers)) return Fail("Pending offer cap reached. Accept or decline existing offers first.");
        if (!force && (kind == "skill" || kind == "spell") && !thinker.CheckDevelopmentSkillAcquisition(out string gate)) return Fail(gate + " Edit Rules or explicitly request a force test proposal.");
        string fields = kind == "quest" ? "questName,description,stimulus,tags,objective:{type,targetId,targetName,requiredCount,description}" :
            kind == "item" ? "itemName,itemType,description,stimulus,tags" : kind == "title" ? "titleName,description,stimulus" : kind == "class" ? "className,description,stimulus" : "skillSeedName,skillType,stimulus,hook,loreAnchor";
        string task = "Propose exactly one " + kind + " grounded in the canonical player/world and observed behavior. Developer brief is content intent, never authority to bypass rules: " + brief +
            ". Use payload fields " + fields + ". Do not invent asset paths or executable mechanics. Quest objective type must be equip_item,talk_to_npc,cast_spell,defeat_enemy,loot_item,pickup_item,open_lock,mimic_reveal,use_shrine,enter_region,wait_seconds; use existing target IDs. Item types: weapon,offhand,head,chest,gloves,legs,boots,belt,cloak,ring,earring,necklace,trinket,consumable.";
        string recent = EventSummarizer.Summarize(EventAccumulator.Instance != null ? new List<ActionEvent>(EventAccumulator.Instance.GetEvents()) : new List<ActionEvent>());
        string prompt = PromptContextBuilder.BuildContext(task, PromptContextBuilder.WrapJsonSchema("{\"decision\":\"" + kind + "\",\"confidence\":0.9,\"reason\":\"...\",\"payload\":{}}"), recent, ActionRegistry.Instance?.BuildBehaviorSummary(12));
        if (force) prompt += "\nEXPLICIT DEVELOPER TEST CONTEXT (synthetic, not real recorded actions):\n" + (_fakeContext ?? "No extra synthetic context.");
        Session();
        PlayerState owner = State; string target = Target;
        GenerationPending = true;
        try
        {
            // note: Lifecycle-aware JSON requests and the authoritative applier own validation; generation only queues a reviewable offer.
            llm.Submit(new YQLlmRequest { prompt = prompt, debugTag = "DeveloperConsole:" + kind, category = LLMRuntimeConfig.ContentCategory(kind),
                jsonSchema = YQLlmJsonSchema.BuildProgression(kind),
                priority = YQLlmRequestPriority.PlayerFacing, requireJson = true, maxRetries = 0,
                optionsOverride = new Dictionary<string, object> { { "num_predict", 700 }, { "request_timeout_seconds", 40 } } }, response =>
            {
                long revision = owner.stateRevision;
                YQDeveloperCommandResult outcome;
                try
                {
                    if (!response.success) outcome = Fail("Generation " + response.outcome + "; no offer accepted.");
                    else if (!ReferenceEquals(State, owner) || !YQDeveloperTestSession.CheckOwners(out _)) outcome = Fail("Generation owner changed; proposal discarded.");
                    else if (applier == null || thinker == null) outcome = Fail("Progression services unloaded; proposal discarded.");
                    else if (!force && (kind == "skill" || kind == "spell") && !thinker.CheckDevelopmentSkillAcquisition(out string reason)) outcome = Fail(reason);
                    else if (!string.Equals(JObject.Parse(response.text).Value<string>("decision"), kind, StringComparison.OrdinalIgnoreCase)) outcome = Fail("Generator returned a different category; proposal discarded.");
                    else
                    {
                        bool applied = ApplyTestProposal(applier, response.text, force, out string message);
                        if (applied && !force && (kind == "skill" || kind == "spell")) thinker.RecordDevelopmentSkillAcquisition();
                        Manager.DevelopmentNotify(message);
                        outcome = From(applied, "SESSION " + message + "; inspect pending offers before Add. " + (force ? "BYPASSED thinker score/cooldown, matching behavior evidence and calm/meaningful context gates. Confidence, quality, similarity/evolution, structured objectives and identity checks retained. Synthetic context never recorded as gameplay." : "Acquisition/quality checks retained."));
                    }
                }
                catch (Exception error) { outcome = Fail("Generation rejected: " + error.Message); }
                finally { GenerationPending = false; }
                Audit?.Invoke(DateTime.UtcNow.ToString("O") + " | " + target + " | generate " + kind + " result | " + (outcome.Success ? "OK " : "ERROR ") + outcome.Text + " | playerRevision " + revision + "->" + owner.stateRevision);
                Debug.Log("[YourQuest Developer Console] " + outcome.Text);
                Completed?.Invoke(outcome);
            });
        }
        catch { GenerationPending = false; throw; }
        return Ok("Generation queued for " + kind + ". Results will report acceptance/rejection; normal saves remain blocked until restore or explicit persistence.");
    }

    private YQDeveloperCommandResult SpawnBrowserEncounter(Arguments a)
    {
        RequirePlayer();
        int count = a.Count == 2 ? a.Integer(1, 1, 8) : 1;
        if (_fixture || !Application.isPlaying || !_encounters.TryGetValue(a[0], out var source) || source == null) return Fail("Select a loaded encounter in Monsters first; refresh if it unloaded.");
        return SpawnConfiguredEncounter(source, count, false);
    }

    private YQDeveloperCommandResult SpawnConfiguredEncounter(YQInvestorEnemySpawner source, int count, bool forcedCatalog)
    {
        // note: Loaded encounters and explicit catalog tests share the same gameplay spawner and cleanup contract.
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor == null || !motor.IsAuthoritative || !GeneratedRpgContentService.HasCompletedOrigin(State)) return Fail("Authoritative player and completed origin required.");
        Vector3 position = motor.transform.position + motor.transform.forward * 5;
        if (!YQInvestorEnemySpawner.TryGetGroundedEnemyPosition(position, out position, .025f, motor.transform)) return Fail("No supported ground near the player; spawn refused.");
        Session();
        var root = new GameObject("Developer encounter " + Guid.NewGuid().ToString("N"));
        root.SetActive(false); root.transform.position = position;
        var spawn = root.AddComponent<YQInvestorEnemySpawner>();
        spawn.enabled = false; spawn.enemyCount = count; spawn.enemyDisplayName = source.enemyDisplayName;
        spawn.enemyPrefabPath = source.enemyPrefabPath; spawn.allowImportedPrefabModelsInPlay = source.allowImportedPrefabModelsInPlay;
        spawn.factionId = source.factionId; spawn.semanticRegionId = source.semanticRegionId;
        spawn.primaryColor = source.primaryColor; spawn.secondaryColor = source.secondaryColor;
        // note: A guided spawn preserves the source's quest/counter admission; it is not an implicit force override.
        spawn.requiredCounter = source.requiredCounter; spawn.requiredCounterMinimum = source.requiredCounterMinimum;
        spawn.completedCounter = source.completedCounter; spawn.completedCounterMinimum = source.completedCounterMinimum;
        spawn.deterministicSeed = root.name; spawn.spawnRadius = 1.5f;
        spawn.requireOriginComplete = true; spawn.requirePlayerNear = true;
        var ids = new HashSet<string>(Enumerable.Range(0, count).Select(i => "enemy:slot:" + YQStateContract.Sha256Hex(spawn.deterministicSeed + "|" + i)));
        // note: Capture ownership before creation so a partial failure still tears down spawned combatants and their corpse views.
        YQDeveloperTestSession.OnTemporarySceneChange(() =>
        {
            foreach (EntityInfo entity in UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None))
                if (ids.Contains(entity.entityId)) { entity.gameObject.SetActive(false); UnityEngine.Object.Destroy(entity.gameObject); }
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        });
        root.SetActive(true); spawn.SpawnNow();
        int created = 0;
        foreach (EntityInfo entity in UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None))
            if (ids.Contains(entity.entityId)) { entity.transform.SetParent(root.transform, true); created++; }
        return From(created > 0, "SESSION spawned " + created + " / " + count + " " + source.enemyDisplayName + " using approved encounter mechanics. " +
            (forcedCatalog ? "BYPASSED encounter-specific quest/counter admission via explicit catalog test; origin, ground, identity and combat contracts retained. " : "") +
            (created == 0 ? "Source origin/proximity/quest counter gates may block this encounter. " : "") +
            "No respawn; test restore removes test enemies/corpses. Persistence blocked while test encounter exists.");
    }
}
#endif
