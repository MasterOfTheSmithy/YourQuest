#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class YQDeveloperCommandResult
{
    public bool Success;
    public string Text;
    public YQDeveloperCommandResult(bool success, string text) { Success = success; Text = text; }
}

// note: Closed command names and typed arguments; no reflection, arbitrary evaluation, asset paths or code execution.
public sealed partial class YQDeveloperCommandRegistry
{
    private sealed class Command
    {
        public string Path, Help;
        public int Min, Max;
        public bool Mutation;
        public Func<Arguments, YQDeveloperCommandResult> Run;
    }
    private sealed class Arguments
    {
        public string[] Values;
        public string this[int index] => Values[index];
        public int Count => Values.Length;
        public float Number(int index)
        {
            if (!float.TryParse(this[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Expected a finite number; got '" + this[index] + "'.");
            return value;
        }
        public int Integer(int index, int min, int max)
        {
            if (!int.TryParse(this[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
                throw new ArgumentException("Expected integer " + min + ".." + max + "; got '" + this[index] + "'.");
            return value;
        }
    }
    private sealed class Setting
    {
        public string Name, Unit;
        public float Min, Max;
        public bool Integer;
        public Func<float> Get;
        public Action<float> Set;
    }
    private readonly List<Command> _commands = new List<Command>();
    private readonly Dictionary<string, SkillRecord> _catalog = new Dictionary<string, SkillRecord>(StringComparer.OrdinalIgnoreCase);
    private string _npcId;
    private PlayerState _catalogOwner;
#if UNITY_EDITOR
    private bool _fixture;
    private PlayerStateManager _fixturePlayer;
    private WorldStateManager _fixtureWorld;
    private ProgressionThinkCycle _fixtureThinker;
    private ProgressionDecisionApplier _fixtureApplier;
    // note: Focused editor fixtures inject detached owners without Awake, profile loading, scene changes or real saves.
    public YQDeveloperCommandRegistry(PlayerStateManager player, WorldStateManager world, ProgressionThinkCycle thinker, ProgressionDecisionApplier applier) : this()
    { _fixture = true; _fixturePlayer = player; _fixtureWorld = world; _fixtureThinker = thinker; _fixtureApplier = applier; }
#else
    private const bool _fixture = false;
#endif
    public string Target => _npcId == null ? "PLAYER " + (Manager?.state?.playerId ?? "unavailable") : "NPC " + _npcId;
    private PlayerStateManager Manager
    {
        get
        {
#if UNITY_EDITOR
            if (_fixture) return _fixturePlayer;
#endif
            return PlayerStateManager.Instance;
        }
    }
    private WorldStateManager WorldManager
    {
        get
        {
#if UNITY_EDITOR
            if (_fixture) return _fixtureWorld;
#endif
            return WorldStateManager.Instance;
        }
    }
    private PlayerState State => Manager?.state;
    private ProgressionThinkCycle Thinker
    {
        get
        {
#if UNITY_EDITOR
            if (_fixture) return _fixtureThinker;
#endif
            return UnityEngine.Object.FindFirstObjectByType<ProgressionThinkCycle>();
        }
    }
    private ProgressionDecisionApplier Applier
    {
        get
        {
#if UNITY_EDITOR
            if (_fixture) return _fixtureApplier;
#endif
            return UnityEngine.Object.FindFirstObjectByType<ProgressionDecisionApplier>();
        }
    }
    public Action<string> Audit;
    public Action<UnityEngine.InputSystem.Key> BindKey;

    public YQDeveloperCommandRegistry()
    {
        RegisterContainerCommands();
        RegisterBrowserCommands();
        Register("help", 0, 8, false, "help [command] : list syntax, supported operations and ranges", Help);
        Register("console key", 1, 1, false, "console key <InputSystem Key name> : default Backquote, saved developer preference", a =>
        {
            if (!Enum.TryParse(a[0], true, out UnityEngine.InputSystem.Key key) || !Enum.IsDefined(typeof(UnityEngine.InputSystem.Key), key) || key == UnityEngine.InputSystem.Key.None ||
                UnityEngine.InputSystem.Keyboard.current?[key] == null || BindKey == null) return Fail("Unknown keyboard key or UI unavailable. Example: console key F2.");
            BindKey(key); return Ok("Console key=" + key + " (developer preference only).");
        });
        Register("target player", 0, 0, false, "target player", a => { _npcId = null; return Ok(Target); });
        Register("target npc", 1, 1, false, "target npc <npcId> : target a persisted NPC, not a scene object name", a =>
        { var npc = FindNpc(a[0]); if (npc == null) return Fail("Unknown NPC ID. Use target list."); _npcId = npc.npcId; return Ok(Target + " / " + npc.name); });
        Register("target list", 0, 0, false, "target list : player and persisted NPC IDs", a =>
            Ok(Targets()));
        Register("target inspect", 0, 0, false, "target inspect : identity/capabilities", a => _npcId == null ? Ok(Target + "; stat list, skill list, item list, quest list") : NpcInspect());
        Register("stat list", 0, 0, false, "stat list : actual names, units and console safety ranges", a => Ok(_npcId == null ?
            string.Join("\n", YQDeveloperVariable.Stats.Select(v => v.Name + " [" + v.Min + ".." + v.Max + "] " + v.Unit + (v.Integer ? " integer" : ""))) : "affinityToPlayer [-1..1] normalized affinity; NPC stats/XP/skills/inventory are not modeled."));
        Register("stat get", 1, 1, false, "stat get <name>", StatGet);
        Register("stat set", 2, 2, true, "stat set <name> <value> : SESSION edit", a => StatEdit(a, false));
        Register("stat add", 2, 2, true, "stat add <name> <delta> : XP uses normal level-up service", a => StatEdit(a, true));
        Register("skill list", 0, 0, false, "skill list : accepted/committed skill IDs and pending offer IDs", a => { RequirePlayer(); RefreshSkills(); return Ok(Skills()); });
        Register("ability list", 0, 0, false, "ability list : abilities are canonical SkillRecords (same IDs/commands)", a => { RequirePlayer(); RefreshSkills(); return Ok(Skills()); });
        Register("skill grant", 1, 3, true, "skill grant <skillId|pendingOfferId> [rank 1..100] [--force] : default earned acceptance; force reports bypasses", Grant);
        Register("skill revoke", 1, 1, true, "skill revoke <skillId> : lock accepted record and clear equipped slots", a =>
        { RequirePlayer(); SkillRecord s = State.FindSkillById(a[0]); if (s == null) return Fail("Target does not own skill ID."); Session(); return From(Manager.DevelopmentSetSkill(s, s.rank, true, out string message), message); });
        Register("skill rank set", 2, 2, true, "skill rank set <skillId> <rank 1..100> : direct SESSION rank edit", a =>
        { RequirePlayer(); int rank = a.Integer(1, 1, 100); SkillRecord s = State.FindSkillById(a[0]); if (s == null || !s.unlocked) return Fail("Grant/unlock the accepted skill first."); Session(); return From(Manager.DevelopmentSetSkill(s, rank, false, out string message), message); });
        Register("skill xp add", 2, 2, false, "skill xp add <id> <amount> : unsupported (SkillRecord has no XP field)", a => Fail("Unsupported: SkillRecord has rank and tier/circle, no per-skill XP or learning rate. No state changed."));
        Register("skill xp set", 2, 2, false, "skill xp set <id> <value> : unsupported (SkillRecord has no XP field)", a => Fail("Unsupported: SkillRecord has no XP field. Use skill rank set for the supported progression variable."));
        Register("progression inspect", 0, 0, false, "progression inspect : live settings, units, effective score floors", a =>
        { RequirePlayer(); return Ok(ProgressionInspect()); });
        Register("progression set", 2, 3, true, "progression set <name> <value> [--force] : temporary settings; below normal score floors requires --force", ProgressionSet);
        Register("item list", 0, 0, false, "item list : accepted inventory item IDs/template IDs (no static generated-item catalogue)", a =>
        { RequirePlayer(); return Ok(string.Join("\n", State.inventoryItems.Where(i => i != null).Select(i => i.itemId + " / template=" + i.templateId + " / " + i.displayName + " x" + i.quantity + " stackable=" + i.stackable))); });
        Register("inventory", 0, 0, false, "inventory : alias of item list", a =>
        { RequirePlayer(); return Ok(string.Join("\n", State.inventoryItems.Where(i => i != null).Select(i => i.itemId + " / " + i.displayName + " x" + i.quantity))); });
        Register("item give", 2, 2, true, "item give <acceptedItemId> <count 1..99> : non-stackable count 1; shared capacity/mechanical matching rules", a => Item(a, false));
        Register("item remove", 2, 2, true, "item remove <itemId> <count 1..10000>", a => Item(a, true));
        Register("quest list", 0, 0, false, "quest list : actual quest IDs and supported hook", a =>
        { RequirePlayer(); return Ok(string.Join("\n", State.quests.Where(q => q != null).Select(q => q.questId + " / " + q.name + " / " + q.status))); });
        Register("quest activate", 1, 1, true, "quest activate <questId> : existing SetActiveQuest", a =>
        { RequirePlayer(); if (!State.quests.Any(q => q != null && q.questId == a[0])) return Fail("Unknown quest ID."); Session(); bool ok = State.SetActiveQuest(a[0]); if (ok) Manager.DevelopmentNotify("Active quest " + a[0]); return From(ok, ok ? "SESSION active quest=" + a[0] : "Quest cannot be activated."); });
        Register("quest complete", 2, 2, true, "quest complete <questId> --force : existing completion/reward hook; bypass objectives", a =>
        { RequirePlayer(); if (a[1] != "--force") return Fail("Completion bypass requires --force; normal quest director evaluates objectives.");
          QuestRecord q = State.quests.Find(x => x != null && x.questId == a[0]); if (q == null) return Fail("Unknown quest ID.");
          if ((long)State.currency + Mathf.Max(q.rewardGold, 10000) > 1000000 || State.level > 30) return Fail("Quest reward would risk exceeding console XP/currency safety bounds.");
          Session(); bool ok = State.TryCompleteQuest(a[0], out string message); if (ok) Manager.DevelopmentNotify(message); return From(ok, message + (ok ? " SESSION; bypassed objective completion checks." : "")); });
        Register("test snapshot", 0, 0, false, "test snapshot : capture rollback; block normal saves/profile transitions", a => From(YQDeveloperTestSession.Begin(Manager, WorldManager, out string message), message));
        Register("test restore", 0, 0, false, "test restore : restore captured player/NPC test values; release save barrier", a => From(YQDeveloperTestSession.Restore(out string message), message));
        Register("test reset", 0, 0, false, "test reset : alias of test restore; never factory-reset a save", a => From(YQDeveloperTestSession.Restore(out string message), message));
        Register("test persist", 1, 1, false, "test persist --confirm : explicitly publish current test state through paired profile commit", a =>
        { if (a[0] != "--confirm") return Fail("Explicit --confirm required to write the active normal profile."); return From(YQDeveloperTestSession.Persist(out string message), message); });
    }

    private void Register(string path, int min, int max, bool mutation, string help, Func<Arguments, YQDeveloperCommandResult> run) =>
        _commands.Add(new Command { Path = path, Min = min, Max = max, Mutation = mutation, Help = help, Run = run });

    public YQDeveloperCommandResult Execute(string input)
    {
        string target = Target;
        long before = State?.stateRevision ?? -1;
        YQDeveloperCommandResult result;
        try
        {
            if (!YQDeveloperConsoleGate.Enabled || (!_fixture && !Application.isPlaying)) return Fail("Console commands require an enabled development Play session.");
            List<string> tokens = Tokenize(input);
            Command command = _commands.OrderByDescending(c => c.Path.Length).FirstOrDefault(c =>
                tokens.Count >= c.Path.Split(' ').Length && string.Equals(string.Join(" ", tokens.Take(c.Path.Split(' ').Length)), c.Path, StringComparison.OrdinalIgnoreCase));
            if (command == null) result = Fail("Unknown command. Use help or Tab completion.");
            else
            {
                string[] args = tokens.Skip(command.Path.Split(' ').Length).ToArray();
                if (args.Length < command.Min || args.Length > command.Max) result = Fail("Usage: " + command.Help);
                else
                {
                    if (command.Mutation || command.Path.StartsWith("test ", StringComparison.Ordinal))
                    {
                        if (!_fixture && (YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked || YQStartupLoadingScreen.IsGenerationVisible))
                            throw new ArgumentException("Wait for production startup/world generation before test mutations.");
                        if (LLMClient.Instance != null && LLMClient.Instance.IsBusy)
                            throw new ArgumentException("Wait for in-flight LLM work before mutating/restoring test state.");
                        if (YQDeveloperTestSession.Active && !YQDeveloperTestSession.CheckOwners(out string error)) throw new ArgumentException(error);
                    }
                    result = command.Run(new Arguments { Values = args });
                }
            }
        }
        catch (ArgumentException e) { result = Fail(e.Message); }
        catch (Exception e) { Debug.LogException(e); result = Fail("Command failed: " + e.Message + ". Snapshot retained; use test restore."); }
        string audit = DateTime.UtcNow.ToString("O") + " | " + target + " | " + input + " | " + (result.Success ? "OK " : "ERROR ") +
            result.Text + " | playerRevision " + before + "->" + (State?.stateRevision ?? -1);
        Audit?.Invoke(audit);
        Debug.Log("[YourQuest Developer Console] " + audit);
        return result;
    }

    public static List<string> Tokenize(string text)
    {
        if (text == null || text.Length > 1024) throw new ArgumentException("Command length must be 0..1024 characters.");
        var tokens = new List<string>(); var token = new StringBuilder(); bool quoted = false;
        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            // note: Guided descriptions and IDs round-trip quotes/backslashes without allowing executable input.
            if (quoted && c == '\\' && index + 1 < text.Length && (text[index + 1] == '"' || text[index + 1] == '\\'))
            { token.Append(text[++index]); continue; }
            if (c == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(c) && !quoted) { if (token.Length > 0) { tokens.Add(token.ToString()); token.Clear(); } }
            else token.Append(c);
        }
        if (quoted) throw new ArgumentException("Unclosed double quote.");
        if (token.Length > 0) tokens.Add(token.ToString());
        return tokens;
    }

    private YQDeveloperCommandResult Help(Arguments a)
    {
        string prefix = string.Join(" ", a.Values);
        return Ok(string.Join("\n", _commands.Where(c => c.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(c => c.Help)) +
            "\nSESSION edits never save automatically. test persist --confirm writes the active normal profile. Numeric ranges are console safety limits, not invented gameplay caps. NPC only supports affinityToPlayer; per-skill XP unsupported.");
    }
    private void RequirePlayer()
    {
        if (_npcId != null) throw new ArgumentException("Target " + _npcId + " has no player progression/inventory contract; use target player.");
        if (State == null) throw new ArgumentException("Player state owner unavailable.");
        State.EnsureCollections();
    }
    private void Session()
    {
        if (!YQDeveloperTestSession.Active && !YQDeveloperTestSession.Begin(Manager, WorldManager, out string message)) throw new ArgumentException(message);
        if (!YQDeveloperTestSession.CheckOwners(out string error)) throw new ArgumentException(error);
    }
    private WorldState.NpcRecord FindNpc(string id) => WorldManager?.State?.npcs?.Find(n => n != null && string.Equals(n.npcId, id, StringComparison.OrdinalIgnoreCase));
    private string Targets() => "player / " + (State?.playerId ?? "unavailable") + "\n" + string.Join("\n",
        WorldManager?.State?.npcs?.Where(n => n != null).Select(n => n.npcId + " / " + n.name) ?? Enumerable.Empty<string>());
    private YQDeveloperCommandResult NpcInspect()
    {
        var npc = FindNpc(_npcId);
        return npc == null ? Fail("NPC target no longer exists; use target list.") : Ok(Target + " / " + npc.name + "; affinityToPlayer=" + npc.affinityToPlayer +
            " [-1..1]; status=" + npc.status + "; faction=" + npc.factionId + "; location=" + npc.locationId + "; stats, skills, XP and inventory unsupported.");
    }
    private YQDeveloperCommandResult StatGet(Arguments a)
    {
        if (_npcId != null) return a[0] == "affinityToPlayer" ? NpcInspect() : Fail("NPC only supports affinityToPlayer.");
        RequirePlayer(); var v = YQDeveloperVariable.FindStat(a[0]); return v == null ? Fail("Unknown stat. Use stat list (names are case-sensitive).") : Ok(v.Name + "=" + v.Read(State) + " " + v.Unit);
    }
    private YQDeveloperCommandResult StatEdit(Arguments a, bool add)
    {
        float value = a.Number(1);
        if (_npcId != null)
        {
            var npc = FindNpc(_npcId);
            if (npc == null || a[0] != "affinityToPlayer") return Fail("NPC only supports affinityToPlayer; use target inspect.");
            float after = add ? npc.affinityToPlayer + value : value;
            if (after < -1 || after > 1 || float.IsInfinity(after)) return Fail("affinityToPlayer requires -1..1 normalized affinity.");
            Session(); return From(WorldManager.DevelopmentSetNpcAffinity(npc.npcId, after, out string message), message);
        }
        RequirePlayer(); var variable = YQDeveloperVariable.FindStat(a[0]);
        if (variable == null) return Fail("Unknown stat; use stat list.");
        if (!variable.Validate(add ? variable.Read(State) + value : value, out string error)) return Fail(error);
        Session(); return From(Manager.DevelopmentSetStat(a[0], value, add, out string result), result);
    }

    private void RefreshSkills()
    {
        if (!ReferenceEquals(_catalogOwner, State)) { _catalog.Clear(); _catalogOwner = State; }
        // note: Preserve known accepted templates after revocation; development builds use accepted records and offers, Editor also discovers existing committed assets.
        foreach (SkillRecord skill in State.skills)
            if (skill != null && !string.IsNullOrWhiteSpace(skill.skillId)) _catalog[skill.skillId] = skill;
#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/GeneratedSkills/Committed" }))
        {
            SkillData asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || string.IsNullOrWhiteSpace(asset.skillId) || _catalog.ContainsKey(asset.skillId)) continue;
            _catalog[asset.skillId] = new SkillRecord { skillId = asset.skillId, familyId = asset.familyId, parentSkillId = asset.parentSkillId,
                name = asset.skillName, description = asset.description, type = asset.type.ToString(), context = asset.context, environment = asset.environment,
                tier = asset.tier, rank = asset.level, unlocked = true };
        }
#endif
    }
    private string Skills() => string.Join("\n", _catalog.Values.Select(s => s.skillId + " / " + s.name + " / rank=" + s.rank +
        " / " + (YQSpellCircleRules.IsSpell(s) ? "circle=" : "tier=") + s.tier + " / " + (State.FindSkillById(s.skillId) == null ? "committed template" : s.unlocked ? "owned" : "revoked"))) +
        "\n" + string.Join("\n", State.pendingOffers.Where(o => o != null && o.IsPending && (o.offerKind == "skill" || o.offerKind == "spell")).Select(o => o.offerId + " / " + o.name + " / earned pending offer"));

    private YQDeveloperCommandResult Grant(Arguments a)
    {
        RequirePlayer();
        bool force = a.Values.Contains("--force");
        if (a.Values.Skip(1).Any(s => s.StartsWith("--", StringComparison.Ordinal) && s != "--force") || a.Values.Count(s => s == "--force") > 1)
            return Fail("Only --force is supported, once.");
        string[] ranks = a.Values.Skip(1).Where(s => s != "--force").ToArray();
        if (ranks.Length > 1) return Fail("Expected one optional rank.");
        int rank = 1;
        if (ranks.Length == 1 && (!int.TryParse(ranks[0], out rank) || rank < 1 || rank > 100)) return Fail("Rank requires integer 1..100.");
        if (!force && rank > 1) return Fail("Direct rank advancement requires --force, or use the explicit skill rank set test command.");
        PendingProgressionOfferRecord offer = State.FindOfferById(a[0]);
        if (offer != null && offer.IsPending && (offer.offerKind == "skill" || offer.offerKind == "spell"))
        {
            if (!string.IsNullOrWhiteSpace(offer.upgradeTargetId) && State.FindSkillById(offer.upgradeTargetId) == null)
                return Fail("Missing upgrade target; force cannot create a dangling parent.");
            Session(); bool accepted = State.AcceptOffer(offer.offerId, out string message);
            SkillRecord actual = State.FindSkillByName(offer.name);
            if (accepted && actual != null)
            {
                if (force && ranks.Length > 0) actual.rank = rank;
                Manager.DevelopmentNotify(message);
            }
            return From(accepted, message + (actual != null ? " id=" + actual.skillId + " rank=" + actual.rank : "") +
                "; SESSION; pending offer already passed acquisition gates." + (force && ranks.Length > 0 ? " BYPASSED rank advancement through explicit test rank." : ""));
        }
        RefreshSkills();
        if (!_catalog.TryGetValue(a[0], out SkillRecord template)) return Fail("Unknown accepted/committed skill or pending offer ID. Use skill list.");
        if (force && ranks.Length == 0) rank = State.FindSkillById(template.skillId)?.rank ?? Mathf.Max(1, template.rank);
        if (force)
        {
            Session(); bool granted = Manager.DevelopmentSetSkill(template, rank, false, out string message);
            return From(granted, message + (granted ? "; BYPASSED: evidence score/window, cooldown, pending cap, calm/threat context, proposal confidence, matching behavior evidence, similarity/evolution/oddity acquisition. Identity/parent/rank/circle invariants retained." : ""));
        }
        if (State.FindSkillById(template.skillId)?.unlocked == true) return Ok("Already unlocked; id=" + template.skillId + ". Use skill rank set for direct rank edits.");
        ProgressionThinkCycle thinker = Thinker; ProgressionDecisionApplier applier = Applier;
        if (thinker == null || applier == null) return Fail("Live progression services unavailable; normal grant refused.");
        if (!thinker.CheckDevelopmentSkillAcquisition(out string reason)) return Fail(reason + " Explicit --force is available for testing.");
        Session();
        JObject proposal = new JObject { ["decision"] = YQSpellCircleRules.IsSpell(template) ? "spell" : "skill", ["confidence"] = 1f, ["reason"] = "Developer requests existing committed content through earned acquisition gates.",
            ["payload"] = new JObject { ["skillSeedName"] = template.name, ["skillType"] = template.type, ["hook"] = template.description, ["stimulus"] = template.context, ["loreAnchor"] = template.environment } };
        if (!applier.TryApply(proposal.ToString(), out _, out reason)) return Fail(reason);
        thinker.RecordDevelopmentSkillAcquisition();
        PendingProgressionOfferRecord queued = State.GetActiveOffer();
        // note: Resolve the newly queued offer by its curated identity, never accept an unrelated pending title/item offer.
        queued = State.pendingOffers.LastOrDefault(o => o != null && o.IsPending && (o.offerKind == "skill" || o.offerKind == "spell") &&
            string.Equals(o.payloadJson, proposal["payload"].ToString(Newtonsoft.Json.Formatting.None), StringComparison.Ordinal));
        if (queued == null) return Fail("No matching queued skill offer; inspect skill list.");
        bool ok = State.AcceptOffer(queued.offerId, out string acceptedMessage);
        if (ok) Manager.DevelopmentNotify(acceptedMessage);
        return From(ok, acceptedMessage + "; SESSION; actual accepted ID=" + State.FindSkillByName(queued.name)?.skillId);
    }

    private YQDeveloperCommandResult Item(Arguments a, bool remove)
    {
        RequirePlayer(); int count = a.Integer(1, 1, remove ? 10000 : YQContainerInventory.MaxStack);
        InventoryItemRecord item = State.FindInventoryItemById(a[0]); if (item == null) return Fail("Unknown accepted item ID. Use item list; arbitrary generated templates are not executable IDs.");
        Session(); return From(Manager.DevelopmentItem(item, count, remove, out string message), message);
    }

    private List<Setting> Settings()
    {
        var result = new List<Setting>();
        var t = Thinker; var p = Applier; var b = t != null ? t.balance : null;
        void Add(string name, string unit, float min, float max, bool integer, Func<float> get, Action<float> set) =>
            result.Add(new Setting { Name = name, Unit = unit, Min = min, Max = max, Integer = integer, Get = get, Set = set });
        if (t != null && b != null)
        {
            Add("skillCandidateScore", "earned-ness points (normal floor 24)", 0, 10000, false, ()=>t.EffectiveSkillCandidateScore, v=>t.developmentSkillCandidateScore=v);
            Add("minimumScore", "earned-ness points (normal floor 12)", 0, 10000, false, ()=>t.EffectiveMinimumScore, v=>t.developmentMinimumScore=v);
            Add("skillCooldown", "seconds (normal floor 420; future acquisitions)", 0, 86400, false, ()=>t.EffectiveSkillCooldownSeconds, v=>t.developmentSkillCooldownSeconds=v);
            Add("repeatPenaltyPerSameVerb", "score points per repeated verb", .05f, 1, false, ()=>b.repeatPenaltyPerSameVerb, v=>t.balance.repeatPenaltyPerSameVerb=v);
            Add("varietyBonusMultiplier", "score multiplier", 0, 10, false, ()=>b.varietyBonusMultiplier, v=>t.balance.varietyBonusMultiplier=v);
            Add("maxPendingOffers", "pending offers", 1, 100, true, ()=>t.maxPendingOffers, v=>t.maxPendingOffers=(int)v);
        }
        if (p != null)
        {
            Add("minConfidence", "proposal probability", 0, 1, false, ()=>p.minConfidence, v=>p.minConfidence=v);
            Add("minSkillConfidence", "proposal probability", 0, 1, false, ()=>p.minSkillConfidence, v=>p.minSkillConfidence=v);
            Add("minSkillEvidenceScore", "normalized evidence score", 0, 1, false, ()=>p.minSkillEvidenceScore, v=>p.minSkillEvidenceScore=v);
            Add("duplicateSkillThreshold", "similarity 0..1", 0, 1, false, ()=>p.duplicateSkillThreshold, v=>p.duplicateSkillThreshold=v);
            Add("upgradeSkillThreshold", "similarity 0..1", 0, 1, false, ()=>p.upgradeSkillThreshold, v=>p.upgradeSkillThreshold=v);
            Add("evolutionStepsRequired", "qualifying precursor steps", 1, 100, true, ()=>p.evolutionStepsRequired, v=>p.evolutionStepsRequired=(int)v);
            Add("oddityEvolutionStepsRequired", "qualifying incubation steps", 1, 100, true, ()=>p.oddityEvolutionStepsRequired, v=>p.oddityEvolutionStepsRequired=(int)v);
            Add("requirePlayerEvidenceForSkills", "boolean 0/1", 0, 1, true, ()=>p.requirePlayerEvidenceForSkills ? 1 : 0, v=>p.requirePlayerEvidenceForSkills=v==1);
            Add("gateSkillsToCalmLowThreat", "boolean 0/1", 0, 1, true, ()=>p.gateSkillsToCalmLowThreat ? 1 : 0, v=>p.gateSkillsToCalmLowThreat=v==1);
        }
        return result;
    }
    private string ProgressionInspect() => string.Join("\n", Settings().Select(s => s.Name + "=" + s.Get() + " " + s.Unit + " [" + s.Min + ".." + s.Max + "]")) +
        "\nSkill ranks 1..100 console safety; spells circles 1..7. No skill XP/learning-rate mechanic. Danger bonus is not applied by ProgressionMath. Existing cooldown deadlines are not cleared by changing skillCooldown.";
    private YQDeveloperCommandResult ProgressionSet(Arguments a)
    {
        RequirePlayer(); float value = a.Number(1); bool force = a.Count == 3 && a[2] == "--force";
        if (a.Count == 3 && !force) return Fail("Only --force is supported.");
        Setting setting = Settings().Find(s => s.Name == a[0]);
        if (setting == null) return Fail("Unknown setting or owner unavailable. Use progression inspect.");
        if (value < setting.Min || value > setting.Max || (setting.Integer && value != Mathf.Floor(value))) return Fail(setting.Name + " requires " + setting.Min + ".." + setting.Max + " " + setting.Unit + (setting.Integer ? ", integer" : ""));
        bool bypass = (setting.Name == "skillCandidateScore" && value < 24) || (setting.Name == "minimumScore" && value < 12) ||
            (setting.Name == "skillCooldown" && value < 420) ||
            ((setting.Name == "requirePlayerEvidenceForSkills" || setting.Name == "gateSkillsToCalmLowThreat" || setting.Name == "minSkillEvidenceScore" ||
                setting.Name == "minConfidence" || setting.Name == "minSkillConfidence") && value == 0);
        if (bypass && !force) return Fail("Lowering a production floor or disabling a requirement needs explicit --force.");
        Session();
        var thinker = Thinker;
        if (thinker != null && thinker.balance != null && setting.Name != "maxPendingOffers" &&
            (setting.Name == "repeatPenaltyPerSameVerb" || setting.Name == "varietyBonusMultiplier"))
        {
            // note: Never mutate an imported/authored ScriptableObject; the owner receives a disposable runtime clone.
            ProgressionBalanceConfig original = thinker.balance;
            ProgressionBalanceConfig clone = UnityEngine.Object.Instantiate(original);
            clone.hideFlags = HideFlags.DontSave;
            thinker.balance = clone;
            YQDeveloperTestSession.OnRestore(() =>
            {
                if (thinker != null) thinker.balance = original;
                if (Application.isPlaying) UnityEngine.Object.Destroy(clone); else UnityEngine.Object.DestroyImmediate(clone);
            });
        }
        setting = Settings().Find(s => s.Name == a[0]);
        float before = setting.Get();
        if (a[0] == "skillCandidateScore")
        { float? prior = thinker.developmentSkillCandidateScore; YQDeveloperTestSession.OnRestore(()=> { if(thinker != null) thinker.developmentSkillCandidateScore=prior; }); }
        else if (a[0] == "minimumScore")
        { float? prior = thinker.developmentMinimumScore; YQDeveloperTestSession.OnRestore(()=> { if(thinker != null) thinker.developmentMinimumScore=prior; }); }
        else if (a[0] == "skillCooldown")
        { float? prior = thinker.developmentSkillCooldownSeconds; YQDeveloperTestSession.OnRestore(()=> { if(thinker != null) thinker.developmentSkillCooldownSeconds=prior; }); }
        else
        { Setting captured = setting; UnityEngine.Object owner = a[0] == "maxPendingOffers" || a[0] == "skillCooldown" || a[0] == "repeatPenaltyPerSameVerb" || a[0] == "varietyBonusMultiplier" ? (UnityEngine.Object)thinker : Applier; YQDeveloperTestSession.OnRestore(()=> { if(owner != null) captured.Set(before); }); }
        setting.Set(value);
        return Ok("SESSION override " + setting.Name + " " + before + " -> " + setting.Get() + " " + setting.Unit + (bypass ? "; BYPASSED production score floor/acquisition requirement." : "") + "; never written to assets or saves.");
    }

    public string[] Complete(string prefix)
    {
        var choices = new List<string>(_commands.Select(c => c.Path));
        if (_npcId != null)
            foreach (string op in new[] { "get", "set", "add" }) choices.Add("stat " + op + " affinityToPlayer");
        else
            foreach (var variable in YQDeveloperVariable.Stats)
                foreach (string op in new[] { "get", "set", "add" }) choices.Add("stat " + op + " " + variable.Name);
        foreach (var setting in Settings()) choices.Add("progression set " + setting.Name);
        if (State != null)
        {
            RefreshSkills(); foreach (string id in _catalog.Keys) { choices.Add("skill grant " + id); choices.Add("skill revoke " + id); choices.Add("skill rank set " + id); }
            foreach (var offer in State.pendingOffers.Where(o => o != null && o.IsPending && (o.offerKind == "skill" || o.offerKind == "spell"))) choices.Add("skill grant " + offer.offerId);
            foreach (var item in State.inventoryItems.Where(i => i != null)) { choices.Add("item give " + item.itemId); choices.Add("item remove " + item.itemId); }
        }
        foreach (var npc in WorldManager?.State?.npcs ?? new List<WorldState.NpcRecord>()) if (npc != null) choices.Add("target npc " + npc.npcId);
        foreach (var offer in State?.pendingOffers ?? new List<PendingProgressionOfferRecord>())
            if (offer != null && offer.IsPending) choices.Add("offer accept " + QuoteArgument(offer.offerId));
        foreach (string id in _encounters.Keys) choices.Add("spawn monster " + QuoteArgument(id));
        return choices.Distinct().Where(c => c.StartsWith(prefix ?? "", StringComparison.OrdinalIgnoreCase)).OrderBy(c => c).Take(40).ToArray();
    }
    private static YQDeveloperCommandResult Ok(string text) => new YQDeveloperCommandResult(true, text);
    private static YQDeveloperCommandResult Fail(string text) => new YQDeveloperCommandResult(false, text);
    private static YQDeveloperCommandResult From(bool success, string text) => new YQDeveloperCommandResult(success, text);
}
#endif
