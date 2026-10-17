#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed partial class YQDeveloperCommandRegistry
{
    private string _actorId, _fakeContext = "";
    private readonly Dictionary<string, GameObject> _actors = new Dictionary<string, GameObject>();
    public string SyntheticContext => _fakeContext;
    private static readonly string[] ItemKinds = { "weapon", "offhand", "head", "chest", "gloves", "legs", "boots", "belt", "cloak", "ring", "earring", "necklace", "trinket", "consumable" };

    private void RegisterToolkitCommands()
    {
        // note: Actor IDs identify loaded objects only; persistent NPC IDs remain the world document's IDs.
        Register("actor list", 0, 0, false, "actor list : loaded actors and their session IDs", a => Ok(string.Join("\n", Browse("Actors").Select(e => e.Id + " / " + e.Name + " / " + e.Summary))));
        Register("target actor", 1, 1, false, "target actor <actor:instanceId> : select a loaded actor from Actors or scene picker", a => SelectActor(a[0]));
        Register("actor inspect", 0, 0, false, "actor inspect : selected loaded actor capabilities", a => InspectActor());
        Register("animation list", 0, 0, false, "animation list : actual selected controller states/triggers", a => Ok(string.Join("\n", Browse("Animations").Select(e => e.Id + " / " + e.Summary))));
        Register("animation play", 2, 3, true, "animation play <statePath> [layer 0..7] --force : temporary presentation only; no root motion", a => PreviewAnimation(a, false));
        Register("animation trigger", 2, 2, true, "animation trigger <triggerName> --force : temporary presentation only", a => PreviewAnimation(a, true));
        Register("animation stop", 0, 0, true, "animation stop : restore captured controller state and animation drivers", a =>
        { var actor = ResolveTargetActor(); if (actor == null) return Fail("Selected actor is unavailable."); actor.GetComponent<YQDeveloperAnimationPreview>()?.Stop(); return Ok("Animation preview stopped. Restore the test snapshot before publishing."); });
        Register("generation context", 1, 2, true, "generation context clear | \"<synthetic context 1..400 characters>\" --force : prompt only; never gameplay evidence", a =>
        {
            if (a.Count == 1 && a[0] == "clear") { _fakeContext = ""; return Ok("Synthetic context cleared."); }
            if (a.Count != 2 || a[1] != "--force" || string.IsNullOrWhiteSpace(a[0]) || a[0].Length > 400) return Fail("Use generation context clear, or a 1..400 character quoted context followed by --force.");
            RequirePlayer(); Session(); string before = _fakeContext; YQDeveloperTestSession.OnRestore(() => _fakeContext = before); _fakeContext = a[0];
            return Ok("SESSION synthetic prompt context set; explicit --force generation required. No action counters or evidence ledger changed.");
        });
        Register("generation inspect", 0, 0, false, "generation inspect : current synthetic prompt context", a => Ok("Synthetic context: " + (string.IsNullOrEmpty(_fakeContext) ? "none" : _fakeContext)));
        Register("item create", 3, 4, true, "item create <kind> <level 1..40> [count 1..99] --force : existing mechanical item generator; labeled test scaffolding", a =>
        {
            RequirePlayer(); if (a[a.Count - 1] != "--force" || !ItemKinds.Contains(a[0])) return Fail("Choose an item kind from Spawn and explicitly use --force.");
            int level = a.Integer(1, 1, 40), count = a.Count == 4 ? a.Integer(2, 1, 99) : 1;
            if (GeneratedRpgContentService.Instance == null) return Fail("Item generation/binding service unavailable.");
            Session(); var item = GeneratedRpgContentService.Instance.GenerateItem("developer-test:" + Guid.NewGuid().ToString("N"), level, a[0], a[0] == "consumable");
            return From(Manager.DevelopmentItem(item, count, false, out string message), message + "; BYPASSED earning/reward acquisition. Existing mechanical generator, binding and inventory invariants retained.");
        });
        Register("spawn item", 1, 2, true, "spawn item <acceptedItemId> [count 1..8] : temporary existing world pickup", SpawnTestItem);
        Register("spawn npc", 1, 4, true, "spawn npc <acceptedNpcPlanId> [\"test name\" \"synthetic persona\" --force] : temporary population-service resident", SpawnTestNpc);
        Register("spawn character", 1, 4, true, "spawn character <acceptedNpcPlanId> [\"test name\" \"synthetic persona\" --force] : NPC character; preserves one player", SpawnTestNpc);
        Register("spawn creature", 2, 3, true, "spawn creature <DOT assetId> [count 1..8] --force : temporary catalog monster through existing enemy spawner", SpawnCatalogCreature);
    }

    public static bool ApplyTestProposal(ProgressionDecisionApplier applier, string json, bool force, out string message)
    {
        // note: Override only evidence/context admission for this synchronous proposal. Quality and structured validators remain authoritative.
        bool skills = applier.requirePlayerEvidenceForSkills, quests = applier.requirePlayerEvidenceForQuests;
        bool calm = applier.gateSkillsToCalmLowThreat, meaningful = applier.gateQuestsToMeaningfulContext;
        try
        {
            if (force) { applier.requirePlayerEvidenceForSkills = false; applier.requirePlayerEvidenceForQuests = false; applier.gateSkillsToCalmLowThreat = false; applier.gateQuestsToMeaningfulContext = false; }
            return applier.TryApply(json, out _, out message);
        }
        finally { applier.requirePlayerEvidenceForSkills = skills; applier.requirePlayerEvidenceForQuests = quests; applier.gateSkillsToCalmLowThreat = calm; applier.gateQuestsToMeaningfulContext = meaningful; }
    }

    public static GameObject ActorFromCollider(Collider collider)
    {
        if (collider == null) return null;
        // note: Selection resolves detached physical presentation back to the single existing player actor.
        var motor = YQInvestorPlayerMotor.ResolveFromCollider(collider);
        if (motor != null && motor.IsAuthoritative) return motor.gameObject;
        return collider.GetComponentInParent<EntityInfo>()?.gameObject;
    }
    public string RegisterActor(GameObject actor)
    {
        if (actor == null) return null;
        string id = "actor:" + actor.GetInstanceID(); _actors[id] = actor; return id;
    }
    private string ActorTargetLabel() => _actors.TryGetValue(_actorId, out var actor) && actor != null ? "ACTOR " + actor.name + " / " + _actorId : "UNLOADED ACTOR " + _actorId;
    private YQDeveloperCommandResult SelectActor(string id)
    {
        if (!_actors.TryGetValue(id, out var actor) || actor == null) return Fail("Unknown/unloaded actor. Refresh Actors or pick its collider in the scene.");
        var motor = actor.GetComponent<YQInvestorPlayerMotor>();
        if (motor != null && motor.IsAuthoritative) { _npcId = null; _actorId = null; return Ok(Target); }
        _actorId = id; string stable = actor.GetComponent<EntityInfo>()?.entityId;
        _npcId = FindNpc(stable) != null ? stable : null;
        return InspectActor();
    }
    private GameObject ResolveTargetActor()
    {
        if (_actorId != null) return _actors.TryGetValue(_actorId, out var actor) ? actor : null;
        if (_npcId == null) return YQInvestorPlayerMotor.ActiveMotor != null ? YQInvestorPlayerMotor.ActiveMotor.gameObject : null;
        var matches = UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None).Where(e => e.entityId == _npcId).ToArray();
        return matches.Length == 1 ? matches[0].gameObject : null;
    }
    private YQDeveloperCommandResult InspectActor()
    {
        var actor = ResolveTargetActor(); if (actor == null) return Fail("Selected actor unloaded or its persistent identity is ambiguous. Refresh Actors.");
        var info = actor.GetComponent<EntityInfo>();
        return Ok(Target + "\nStable ID: " + (info?.entityId ?? State?.playerId) + "\nPosition: " + actor.transform.position + "\n" +
            (TargetsPlayer ? "Player stats, inventory, progression and animation." : _npcId != null ? "NPC affinity and animation; no NPC XP/skill/inventory owner." : "Animation inspection/preview; no editable character-state contract.") +
            "\nAnimator: " + (TargetAnimator() != null ? TargetAnimator().runtimeAnimatorController.name : "unavailable"));
    }
    private Animator TargetAnimator()
    {
        var actor = ResolveTargetActor(); if (actor == null) return null;
        var visual = actor.GetComponent<YQPlayerEquipmentVisual>();
        if (visual != null && visual.CharacterAnimator != null) return visual.CharacterAnimator;
        return actor.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.runtimeAnimatorController != null);
    }
    private YQDeveloperCommandResult PreviewAnimation(Arguments args, bool trigger)
    {
        if (args[args.Count - 1] != "--force") return Fail("Animation previews suspend drivers and require --force.");
        var actor = ResolveTargetActor(); var animator = TargetAnimator();
        if (actor == null || animator == null || !animator.isActiveAndEnabled) return Fail("Selected actor has no active supported Animator.");
        int layer = !trigger && args.Count == 3 ? args.Integer(1, 0, 7) : 0;
        if (trigger ? !animator.parameters.Any(p => p.type == AnimatorControllerParameterType.Trigger && p.name == args[0]) : layer >= animator.layerCount || !animator.HasState(layer, Animator.StringToHash(args[0])))
            return Fail("Unknown trigger/state or layer for this controller. Use Animations; no state changed.");
        Session(); var preview = actor.GetComponent<YQDeveloperAnimationPreview>();
        if (preview == null) preview = actor.AddComponent<YQDeveloperAnimationPreview>();
        if (!preview.IsPreviewing) { preview.Begin(animator); var captured = preview; YQDeveloperTestSession.OnTemporarySceneChange(() => { if (captured != null) captured.Stop(); }); }
        if (trigger) animator.SetTrigger(args[0]); else animator.Play(Animator.StringToHash(args[0]), layer, 0);
        return Ok("SESSION animation preview " + args[0] + "; BYPASSED gameplay animation admission/drivers. No combat effect/resource use; root motion disabled. animation stop or test restore restores presentation.");
    }

    private bool TryBrowseToolkit(string section, List<YQDeveloperBrowserEntry> entries)
    {
        if (section == "Actors")
        {
            foreach (string dead in _actors.Where(p => p.Value == null).Select(p => p.Key).ToArray()) _actors.Remove(dead);
            if (!_fixture && Application.isPlaying)
            {
                if (YQInvestorPlayerMotor.ActiveMotor != null) RegisterActor(YQInvestorPlayerMotor.ActiveMotor.gameObject);
                foreach (var entity in UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None)) RegisterActor(entity.gameObject);
            }
            foreach (var pair in _actors)
            {
                var info = pair.Value.GetComponent<EntityInfo>();
                entries.Add(new YQDeveloperBrowserEntry { Id = pair.Key, Name = info?.displayName ?? pair.Value.name, Kind = "actor", Summary = info != null ? info.hostility + " / level " + info.level : "Authoritative player",
                    Detail = "Stable ID: " + (info?.entityId ?? State?.playerId) + "\nPosition: " + pair.Value.transform.position + "\nSelect target, then use Stats or Animations. Session actor IDs expire when the object unloads." });
            }
        }
        else if (section == "Stats")
        {
            if (TargetsPlayer && State != null) foreach (var v in YQDeveloperVariable.Stats) entries.Add(new YQDeveloperBrowserEntry { Id = v.Name, Name = v.Name, Kind = "stat", Value = v.Read(State).ToString(CultureInfo.InvariantCulture), Summary = v.Read(State).ToString(CultureInfo.InvariantCulture) + " " + v.Unit,
                Detail = "Allowed: " + v.Min + " .. " + v.Max + " " + v.Unit + (v.Integer ? " (integer)" : "") + "\nConsole safety range. XP is current-level progress; Add uses normal level-up events. Derived stats refresh through the real state owner." });
            else if (_npcId != null && FindNpc(_npcId) != null) entries.Add(new YQDeveloperBrowserEntry { Id = "affinityToPlayer", Name = "Player affinity", Kind = "stat", Value = FindNpc(_npcId).affinityToPlayer.ToString(CultureInfo.InvariantCulture), Summary = FindNpc(_npcId).affinityToPlayer + " normalized affinity", Detail = "Allowed: -1 .. 1. Real WorldState NPC attitude; other character stats are unsupported." });
        }
        else if (section == "Rules")
        {
            if (TargetsPlayer) foreach (var setting in Settings()) entries.Add(new YQDeveloperBrowserEntry { Id = setting.Name, Name = setting.Name, Kind = "rule", Value = setting.Get().ToString(CultureInfo.InvariantCulture), Summary = setting.Get().ToString(CultureInfo.InvariantCulture) + " " + setting.Unit,
                Detail = "Allowed: " + setting.Min + " .. " + setting.Max + " " + setting.Unit + (setting.Integer ? " (integer)" : "") + "\nTemporary live service setting; Restore reverts it. Lowering normal admission floors or disabling checks requires explicit Force test. Skill tolerances here are acquisition scores, evidence, confidence, cooldown and context gates; per-skill XP/learning rates are not modeled." });
        }
        else if (section == "Spawn")
        {
            foreach (var npc in WorldManager?.State?.generatedWorldPlan?.generatedNpcs ?? new List<GeneratedNpcPlanRecord>()) if (npc != null && string.IsNullOrEmpty(npc.encampmentId))
                entries.Add(new YQDeveloperBrowserEntry { Id = npc.npcId, Name = npc.displayName, Kind = "npcTemplate", Summary = "NPC template / " + npc.role,
                    Detail = npc.appearanceSummary + "\n" + npc.personality + "\nUses the real population binder and approved visual. Creates a unique temporary NPC record, never a second player or a regenerated world plan. Restore removes it." });
            foreach (string kind in ItemKinds) entries.Add(new YQDeveloperBrowserEntry { Id = kind, Name = "Create " + kind, Kind = "itemPreset", Summary = "Mechanical test item / existing generator", Detail = "Level 1..40; quantity 1 equipment or 1..99 stackable. Explicit test scaffolding through the real item generator. Use Items to request an LLM-authored identity instead." });
            foreach (var creature in YQDotCreatureCatalog.Current?.entries ?? new List<YQDotCreatureEntry>())
                if (creature != null && creature.kind == "monster" && string.IsNullOrEmpty(creature.moduleSlot)) entries.Add(new YQDeveloperBrowserEntry { Id = creature.assetId, Name = creature.species + " / " + System.IO.Path.GetFileNameWithoutExtension(creature.prefabPath), Kind = "creaturePreset", Summary = "Catalog enemy / " + creature.category,
                    Detail = "Approved catalog prefab: " + creature.prefabPath + "\nSpawn 1..8 temporary enemies through the real encounter service. Explicit Force test bypasses encounter-specific quest admission; origin, ground, asset binding and combat invariants stay required. Accepted world plan unchanged." });
        }
        else if (section == "Animations")
        {
            var animator = TargetAnimator(); if (animator == null) return true;
            foreach (var p in animator.parameters) if (p.type == AnimatorControllerParameterType.Trigger) entries.Add(new YQDeveloperBrowserEntry { Id = p.name, Name = p.name, Kind = "animationTrigger", Summary = "Controller trigger", Detail = "Presentation preview only. Explicit force suspends this actor's animation/gameplay drivers, disables root motion and captures controller state. No attacks or spell effects are executed." });
#if UNITY_EDITOR
            var controller = animator.runtimeAnimatorController;
            while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
            if (controller is UnityEditor.Animations.AnimatorController graph)
                for (int layer = 0; layer < graph.layers.Length; layer++) AddStates(graph.layers[layer].stateMachine, graph.layers[layer].name, layer, animator, entries);
#endif
        }
        else return false;
        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name)); return true;
    }
#if UNITY_EDITOR
    private static void AddStates(UnityEditor.Animations.AnimatorStateMachine machine, string path, int layer, Animator animator, List<YQDeveloperBrowserEntry> entries)
    {
        // note: Editor presets come from actual controller graphs, not assumed clip or state names.
        foreach (var state in machine.states)
        {
            string name = path + "." + state.state.name;
            if (animator.HasState(layer, Animator.StringToHash(name))) entries.Add(new YQDeveloperBrowserEntry { Id = name, Name = state.state.name, Kind = "animationState", Layer = layer, Summary = "State / layer " + layer, Detail = "Layer " + layer + "\n" + name + "\nPresentation only; explicit force bypasses transitions. Stop/Restore returns captured controller state." });
        }
        foreach (var child in machine.stateMachines) AddStates(child.stateMachine, path + "." + child.stateMachine.name, layer, animator, entries);
    }
#endif
    private YQDeveloperCommandResult SpawnTestItem(Arguments args)
    {
        RequirePlayer(); int count = args.Count == 2 ? args.Integer(1, 1, 8) : 1;
        var source = State.FindInventoryItemById(args[0]);
        if (source == null || _fixture || YQInvestorPlayerMotor.ActiveMotor == null || !GeneratedRpgContentService.HasCompletedOrigin(State)) return Fail("Accepted inventory ID, active player and completed origin required.");
        Session(); var before = new HashSet<int>(UnityEngine.Object.FindObjectsByType<YQInvestorWorldPickup>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()));
        // note: Register cleanup before creation; even partial failures cannot escape the save barrier/rollback.
        var spawned = new List<YQInvestorWorldPickup>();
        YQDeveloperTestSession.OnTemporarySceneChange(() => { foreach (var pickup in spawned) if (pickup != null) { pickup.gameObject.SetActive(false); UnityEngine.Object.Destroy(pickup.gameObject); } });
        for (int i = 0; i < count; i++) YQInvestorWorldPickup.TrySpawnForPlayer(source, false);
        foreach (var pickup in UnityEngine.Object.FindObjectsByType<YQInvestorWorldPickup>(FindObjectsSortMode.None)) if (!before.Contains(pickup.GetInstanceID())) { pickup.developmentTestPickup = true; spawned.Add(pickup); }
        return Ok("SESSION spawned " + spawned.Count + " existing world pickups. Inventory unchanged until collection; restore removes pickups and test inventory changes.");
    }
    private YQDeveloperCommandResult SpawnTestNpc(Arguments args)
    {
        RequirePlayer(); if (args.Count != 1 && (args.Count != 4 || args[3] != "--force")) return Fail("Use accepted NPC template ID, optionally quoted name/persona and --force.");
        var source = WorldManager?.State?.generatedWorldPlan?.generatedNpcs?.Find(n => n != null && n.npcId == args[0] && string.IsNullOrEmpty(n.encampmentId));
        if (source == null || _fixture || !GeneratedRpgContentService.HasCompletedOrigin(State)) return Fail("Select an accepted resident template in Spawn; live completed origin required.");
        if (args.Count == 4 && (string.IsNullOrWhiteSpace(args[1]) || args[1].Length > 80 || args[2].Length > 400)) return Fail("Test name requires 1..80 characters; synthetic persona at most 400.");
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor == null || !motor.IsAuthoritative) return Fail("Authoritative player unavailable.");
        Vector3 position = motor.transform.position + motor.transform.forward * 3;
        if (!YQInvestorEnemySpawner.TryGetGroundedEnemyPosition(position, out position, .025f, motor.transform)) return Fail("No supported ground near player.");
        var record = JsonConvert.DeserializeObject<GeneratedNpcPlanRecord>(JsonConvert.SerializeObject(source));
        record.npcId = "developer_npc:" + Guid.NewGuid().ToString("N"); record.settlementId = ""; record.encampmentId = "";
        record.displayName = args.Count == 4 ? args[1] : "Test " + source.displayName;
        if (args.Count == 4) record.personality = "SYNTHETIC TEST PERSONA: " + args[2];
        Session(); var world = WorldManager.State; var root = new GameObject("Developer NPC " + record.npcId); root.SetActive(false);
        YQDeveloperTestSession.OnTemporarySceneChange(() => { world.npcs.RemoveAll(n => n != null && n.npcId == record.npcId); world.TouchNow(); if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); } });
        if (!YQGeneratedWorldPopulation.DevelopmentCreateResident(root.transform, record, position, record.npcId)) return Fail("Approved resident visual/ground binding unavailable; no NPC record added. Restore clears temporary root.");
        foreach (var dialogue in root.GetComponentsInChildren<NpcDialogueAgent>(true)) dialogue.persistTranscriptAcrossSessions = false;
        world.npcs.Add(new WorldState.NpcRecord { npcId = record.npcId, name = record.displayName, description = record.personality, factionId = record.factionId, status = "developer_test" }); world.TouchNow(); root.SetActive(true);
        return Ok("SESSION spawned temporary NPC " + record.npcId + (args.Count == 4 ? "; BYPASSED generated identity/persona with explicitly labeled test input." : "; copied accepted template through population service.") + " Accepted world plan unchanged; transcript persistence disabled. Restore removes NPC and record.");
    }
    private YQDeveloperCommandResult SpawnCatalogCreature(Arguments args)
    {
        RequirePlayer(); if (args[args.Count - 1] != "--force") return Fail("Catalog encounter test requires explicit --force.");
        int count = args.Count == 3 ? args.Integer(1, 1, 8) : 1;
        var entry = YQDotCreatureCatalog.Current?.entries.Find(e => e != null && e.assetId == args[0] && e.kind == "monster" && string.IsNullOrEmpty(e.moduleSlot));
        if (entry == null || _fixture || !Application.isPlaying) return Fail("Unknown/live-only creature ID. Select a catalog enemy in Spawn; asset paths are not accepted.");
#if !UNITY_EDITOR
        // note: The existing enemy spawner's imported-model path is Editor-only. Do not silently substitute its wisp fallback for a chosen catalog monster.
        return Fail("Catalog monster models are Editor-only in the existing enemy spawner. Use a loaded encounter's supported runtime representation; no creature spawned.");
#else
        var registry = YQRuntimeWorldAssetRegistry.Instance;
        if (registry == null || registry.ResolvePrefab(entry.prefabPath) == null) return Fail("Catalog creature's approved prefab is unavailable.");
        // note: A detached configuration supplies only the existing spawner's typed fields; it never owns or generates a competing encounter system.
        var config = new GameObject("Developer creature configuration"); config.SetActive(false);
        try
        {
            var source = config.AddComponent<YQInvestorEnemySpawner>(); source.enabled = false;
            source.enemyPrefabPath = entry.prefabPath; source.enemyDisplayName = "Test " + entry.species;
            source.allowImportedPrefabModelsInPlay = true;
            source.semanticRegionId = State.currentRegionId;
            source.requiredCounter = ""; source.completedCounter = "";
            return SpawnConfiguredEncounter(source, count, true);
        }
        finally { UnityEngine.Object.Destroy(config); }
#endif
    }
}
#endif
