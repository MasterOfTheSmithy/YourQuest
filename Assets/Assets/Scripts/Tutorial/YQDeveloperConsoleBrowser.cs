#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class YQDeveloperConsole
{
    private static readonly string[] BrowserTabs = { "Actors", "Spawn", "Items", "Skills", "Quests", "Titles", "Stats", "Rules", "Animations", "Monsters", "NPCs", "Commands" };
    private bool _showBrowser = true, _showAdvanced = true, _browserDirty = true, _forceTest, _generateSpell;
    private int _browserTab = 2;
    private bool _scenePicker, _layoutScenePicker, _layoutPickerRequested, _generateClass, _forceGeneration;
    private float _panelWidth;
    private string _editValue = "1", _spawnLevel = "1", _npcName = "", _npcPersona = "", _contextInput = "";
    private string _browserSearch = "", _selectedId = "", _amount = "1", _rank = "1", _generationBrief = "";
    private string _entriesSection = "Items", _browserTarget;
    private string _hoveredId = "", _layoutHoveredId = "", _layoutSelectedId = "", _layoutSearch = "";
    private bool _layoutShowBrowser = true, _layoutShowAdvanced = true;
    private List<YQDeveloperBrowserEntry> _browserEntries = new List<YQDeveloperBrowserEntry>();
    private Vector2 _browserScroll, _detailsScroll;
    private Vector2 _consoleViewportScroll;
    private GUIStyle _browserRow;
    private long _browserRevision = -1;

    private void DrawBrowserToolbar()
    {
        if (Event.current.type == EventType.Layout) { _layoutShowBrowser = _showBrowser; _layoutShowAdvanced = _showAdvanced; }
        // note: The target and rollback controls stay visible independently of the chosen browser tab.
        GUILayout.BeginHorizontal();
        _showBrowser = GUILayout.Toggle(_showBrowser, "Browse", GUI.skin.button, GUILayout.Width(75));
        _showAdvanced = GUILayout.Toggle(_showAdvanced, "Advanced", GUI.skin.button, GUILayout.Width(90));
        _scenePicker = GUILayout.Toggle(_scenePicker, "Pick actor", GUI.skin.button, GUILayout.Width(90));
        if (GUILayout.Button("Player", GUILayout.Width(70))) Submit("target player");
        bool enabled = GUI.enabled;
        GUI.enabled = !YQDeveloperTestSession.Active && !_registry.GenerationPending;
        if (GUILayout.Button(new GUIContent("Snapshot", "Capture a test snapshot and block normal save writes."))) Submit("test snapshot");
        GUI.enabled = YQDeveloperTestSession.Active && !_registry.GenerationPending;
        if (GUILayout.Button(new GUIContent("Restore", "Undo test edits and remove temporary encounters; writes no save."))) Submit("test restore");
        GUI.enabled = enabled;
        GUILayout.EndHorizontal();
    }

    private void DrawBrowser(float height)
    {
        if (_browserRow == null) _browserRow = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = false, fontSize = 14, padding = new RectOffset(10, 10, 5, 5) };
        int tab = GUILayout.SelectionGrid(_browserTab, BrowserTabs, _panelWidth < 760 ? 3 : 6);
        if (tab != _browserTab)
        { _browserTab = tab; _selectedId = ""; _browserSearch = ""; _browserScroll = Vector2.zero; _detailsScroll = Vector2.zero; _forceTest = false; _browserDirty = true; }
        // note: Refresh on layout only; command callbacks never invalidate an IMGUI layout mid-event or scan assets per repaint.
        if (Event.current.type == EventType.Layout && (_browserDirty || _browserRevision != _registry.BrowserRevision || _browserTarget != _registry.Target))
        {
            _entriesSection = BrowserTabs[_browserTab];
            _browserEntries = _registry.Browse(_entriesSection);
            _browserRevision = _registry.BrowserRevision; _browserTarget = _registry.Target; _browserDirty = false;
        }
        // note: Hover/selection/filter changes take effect on the next layout pass, keeping IMGUI control counts stable during repaint.
        if (Event.current.type == EventType.Layout)
        { _layoutHoveredId = _hoveredId; _layoutSelectedId = _selectedId; _layoutSearch = _browserSearch; }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Find", GUILayout.Width(35));
        _browserSearch = GUILayout.TextField(_browserSearch, 128, _entry);
        if (GUILayout.Button("Refresh", GUILayout.Width(80))) _browserDirty = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.Height(height));
        GUILayout.BeginVertical(_panel, GUILayout.Width(Mathf.Max(120, (_panelWidth - 45) * .40f)), GUILayout.ExpandHeight(true));
        _browserScroll = GUILayout.BeginScrollView(_browserScroll);
        YQDeveloperBrowserEntry hovered = null;
        int shown = 0;
        foreach (var entry in _browserEntries)
        {
            if (!MatchesBrowserSearch(entry, _layoutSearch)) continue;
            shown++;
            Color tint = GUI.backgroundColor;
            if (entry.Id == _selectedId) GUI.backgroundColor = new Color(.3f, .7f, 1f);
            if (GUILayout.Button(new GUIContent(entry.Name + "\n" + entry.Summary, "Click to select. Hover to preview."), _browserRow, GUILayout.MinHeight(50)))
            { _selectedId = entry.Id; _detailsScroll = Vector2.zero; _forceTest = false; _editValue = entry.Value ?? "1"; if (entry.Kind == "skill") _rank = entry.Value ?? "1"; }
            GUI.backgroundColor = tint;
            if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) hovered = entry;
        }
        if (shown == 0) GUILayout.Label(EmptyBrowserMessage(), _text);
        GUILayout.EndScrollView();
        if (Event.current.type == EventType.Repaint) _hoveredId = hovered?.Id ?? "";
        GUILayout.Label(shown + " entries  •  hover to preview", _text);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(_panel, GUILayout.ExpandHeight(true));
        _detailsScroll = GUILayout.BeginScrollView(_detailsScroll);
        YQDeveloperBrowserEntry selected = _browserEntries.Find(e => e.Id == _layoutSelectedId);
        YQDeveloperBrowserEntry preview = _browserEntries.Find(e => e.Id == _layoutHoveredId) ?? selected;
        GUILayout.Label(preview?.Name ?? "Choose an entry", _header);
        if (preview != null)
        {
            GUILayout.Label((preview.Id != _layoutSelectedId ? "HOVER PREVIEW — click its row to select\n" : "SELECTED\n") + preview.Summary, _text);
            GUILayout.Label(preview.Detail, _text);
            GUILayout.Label("ID: " + preview.Id, _text);
        }
        else GUILayout.Label("Search by name, ID, type or status. Select an entry for actions below. Generated content appears here as a pending offer for review.", _text);
        GUILayout.Space(8);
        DrawBrowserActions(selected);
        DrawBrowserGeneration();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    public static bool MatchesBrowserSearch(YQDeveloperBrowserEntry entry, string search) => entry != null &&
        (string.IsNullOrWhiteSpace(search) || (entry.Name + " " + entry.Id + " " + entry.Summary + " " + entry.Detail).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

    private string EmptyBrowserMessage()
    {
        if (_browserSearch.Length > 0) return "No matches. Clear Find to see all entries.";
        if (_entriesSection == "Monsters") return "No approved encounter spawner is loaded nearby. Visit an encounter area, then Refresh.";
        if (_entriesSection == "NPCs") return "No canonical NPC records are available yet. Wait for world population.";
        return "No entries yet. Generate a proposal below, then review its pending offer here.";
    }

    private void DrawBrowserActions(YQDeveloperBrowserEntry selected)
    {
        GUILayout.Label("ACTIONS" + (selected == null ? "" : " — " + selected.Name), _header);
        bool enabled = GUI.enabled;
        // note: Action buttons always operate on the clicked selection, never a row merely under the mouse.
        GUI.enabled = selected != null && !_registry.GenerationPending;
        if (selected?.Kind == "command")
        {
            if (GUILayout.Button("Use in Advanced")) { _showAdvanced = true; _input = selected.Id + " "; _completionPrefix = null; _focus = true; }
        }
        else if (selected?.Kind == "actor")
        {
            if (GUILayout.Button("Select this actor")) Submit("target actor " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id));
            if (GUILayout.Button("Inspect current target")) Submit("target inspect");
            if (GUILayout.Button("Edit target stats")) OpenBrowserTab("Stats");
            if (GUILayout.Button("Preview target animations")) OpenBrowserTab("Animations");
        }
        else if (selected?.Kind == "stat" || selected?.Kind == "rule")
        {
            GUILayout.Label(selected.Kind == "rule" ? "New setting value" : "Value / amount to add", _text);
            _editValue = GUILayout.TextField(_editValue, 24, _entry);
            _forceTest = GUILayout.Toggle(_forceTest, "Force test: allow admission override");
            if (GUILayout.Button("Set " + selected.Name)) Submit((selected.Kind == "rule" ? "progression set " : "stat set ") + selected.Id + " " + _editValue + (selected.Kind == "rule" && _forceTest ? " --force" : ""));
            if (selected.Kind == "stat" && GUILayout.Button("Add amount to " + selected.Name)) Submit("stat add " + selected.Id + " " + _editValue);
        }
        else if (selected?.Kind == "animationState" || selected?.Kind == "animationTrigger")
        {
            _forceTest = GUILayout.Toggle(_forceTest, "Force presentation preview (suspend actor drivers)");
            GUI.enabled = GUI.enabled && _forceTest;
            if (GUILayout.Button("Preview " + selected.Name)) Submit((selected.Kind == "animationState" ? "animation play " : "animation trigger ") + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + (selected.Kind == "animationState" ? " " + selected.Layer : "") + " --force");
            GUI.enabled = selected != null && !_registry.GenerationPending;
            if (GUILayout.Button("Stop and restore animation")) Submit("animation stop");
        }
        else if (selected?.Kind == "npcTemplate" || selected?.Kind == "itemPreset" || selected?.Kind == "creaturePreset")
        {
            GUI.enabled = GUI.enabled && _registry.TargetsPlayer;
            if (selected.Kind == "npcTemplate")
            {
                GUILayout.Label("Optional test name (1–80 characters)", _text); _npcName = GUILayout.TextField(_npcName, 80, _entry);
                GUILayout.Label("Optional synthetic personality / background", _text); _npcPersona = GUILayout.TextArea(_npcPersona, 400, _prompt, GUILayout.MinHeight(50));
                _forceTest = GUILayout.Toggle(_forceTest, "Use synthetic name/persona with Force test");
                if (GUILayout.Button("Spawn NPC character near player")) Submit("spawn npc " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + (_forceTest ? " " + YQDeveloperCommandRegistry.QuoteArgument(_npcName) + " " + YQDeveloperCommandRegistry.QuoteArgument(_npcPersona) + " --force" : ""));
            }
            else if (selected.Kind == "creaturePreset")
            {
                GUILayout.Label("Enemy count 1–8", _text); _amount = GUILayout.TextField(_amount, 2, _entry);
                _forceTest = GUILayout.Toggle(_forceTest, "Force test: bypass encounter quest admission"); GUI.enabled = GUI.enabled && _forceTest;
                if (GUILayout.Button("Spawn catalog enemy near player")) Submit("spawn creature " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " " + _amount + " --force");
            }
            else
            {
                GUILayout.Label("Item level 1–40", _text); _spawnLevel = GUILayout.TextField(_spawnLevel, 2, _entry);
                GUILayout.Label("Quantity (1 equipment; 1–99 stackable)", _text); _amount = GUILayout.TextField(_amount, 2, _entry);
                _forceTest = GUILayout.Toggle(_forceTest, "Force mechanical test item (bypass earning)");
                GUI.enabled = GUI.enabled && _forceTest;
                if (GUILayout.Button("Create and add bound test item")) Submit("item create " + selected.Id + " " + _spawnLevel + " " + _amount + " --force");
            }
        }
        else if (selected?.Kind == "npc")
        {
            if (GUILayout.Button("Select NPC as target")) Submit("target npc " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id));
            if (GUILayout.Button("Inspect current target")) Submit("target inspect");
        }
        else
        {
            GUI.enabled = GUI.enabled && _registry.TargetsPlayer;
            if (selected?.Pending == true)
            {
                if (GUILayout.Button("Add pending " + selected.Kind + " offer")) Submit("offer accept " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id));
            }
            else if (selected?.Kind == "item" || selected?.Kind == "monster")
            {
                GUILayout.BeginHorizontal(); GUILayout.Label("Quantity", GUILayout.Width(65)); _amount = GUILayout.TextField(_amount, 6, _entry); GUILayout.EndHorizontal();
                GUILayout.Label(selected.Kind == "monster" ? "1–8 temporary monsters; approved encounter rules" : "Give: 1–99 stackable, 1 equipment. Remove: owned quantity.", _text);
                if (GUILayout.Button(selected.Kind == "monster" ? "Spawn near player" : "Add item")) Submit((selected.Kind == "monster" ? "spawn monster " : "item give ") + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " " + _amount);
                if (selected.Kind == "item" && GUILayout.Button("Remove item")) Submit("item remove " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " " + _amount);
                if (selected.Kind == "item" && GUILayout.Button("Spawn world pickup near player (1–8)")) Submit("spawn item " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " " + _amount);
            }
            else if (selected?.Kind == "skill")
            {
                _forceTest = GUILayout.Toggle(_forceTest, "Force test: bypass acquisition checks");
                GUILayout.Label(_forceTest ? "Bypasses evidence, cooldown, confidence and acquisition gates. IDs, parents, ranks and spell circles stay validated; Results records bypasses." : "Normal acquisition keeps the existing evidence, cooldown and prerequisite checks.", _text);
                bool actionEnabled = GUI.enabled;
                GUI.enabled = actionEnabled && (_forceTest || selected.Owned);
                GUILayout.BeginHorizontal(); GUILayout.Label("Skill rank 1–100", GUILayout.Width(115)); _rank = GUILayout.TextField(_rank, 3, _entry); GUILayout.EndHorizontal();
                GUI.enabled = actionEnabled;
                if (GUILayout.Button(_forceTest ? "Force add skill" : "Add skill (normal rules)"))
                    Submit("skill grant " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + (_forceTest ? " " + _rank + " --force" : ""));
                if (selected.Owned && GUILayout.Button("Revoke skill")) Submit("skill revoke " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id));
                if (selected.Owned && GUILayout.Button("Set owned skill rank (1–100)")) Submit("skill rank set " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " " + _rank);
            }
            else if (selected?.Kind == "quest")
            {
                if (GUILayout.Button("Set active quest")) Submit("quest activate " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id));
                _forceTest = GUILayout.Toggle(_forceTest, "Force test: bypass quest objectives");
                GUI.enabled = GUI.enabled && _forceTest;
                if (GUILayout.Button("Complete quest with test override")) Submit("quest complete " + YQDeveloperCommandRegistry.QuoteArgument(selected.Id) + " --force");
            }
        }
        GUI.enabled = enabled;
        if (!_registry.TargetsPlayer && (_entriesSection == "Items" || _entriesSection == "Skills" || _entriesSection == "Quests" || _entriesSection == "Spawn" || _entriesSection == "Rules" || _entriesSection == "Titles")) GUILayout.Label("Choose Player for inventory/progression/spawning. NPC stats expose supported affinity only.", _text);
    }

    private void DrawBrowserGeneration()
    {
        if (_entriesSection != "Items" && _entriesSection != "Skills" && _entriesSection != "Quests" && _entriesSection != "Titles") return;
        if (_entriesSection == "Skills") _generateSpell = GUILayout.Toggle(_generateSpell, "Generate a spell instead of a skill");
        if (_entriesSection == "Titles") _generateClass = GUILayout.Toggle(_generateClass, "Generate a class instead of a title");
        string kind = _entriesSection == "Items" ? "item" : _entriesSection == "Skills" ? (_generateSpell ? "spell" : "skill") : _entriesSection == "Titles" ? (_generateClass ? "class" : "title") : "quest";
        GUILayout.Space(10); GUILayout.Label("GENERATE " + kind.ToUpperInvariant(), _header);
        GUILayout.Label("Describe the idea. Generation queues an offer through normal validation; it does not automatically add it.", _text);
        _generationBrief = GUILayout.TextArea(_generationBrief, 400, _prompt, GUILayout.MinHeight(48));
        GUILayout.Label("Synthetic test context (optional, never recorded as gameplay)", _text);
        _contextInput = GUILayout.TextArea(_contextInput, 400, _prompt, GUILayout.MinHeight(40));
        bool contextEnabled = GUI.enabled; GUI.enabled = _registry.TargetsPlayer && !_registry.GenerationPending;
        if (GUILayout.Button("Set synthetic context — explicit test override")) Submit("generation context " + YQDeveloperCommandRegistry.QuoteArgument(_contextInput) + " --force");
        if (GUILayout.Button("Clear synthetic context")) Submit("generation context clear");
        GUI.enabled = contextEnabled;
        GUILayout.Label("Active context: " + (string.IsNullOrEmpty(_registry.SyntheticContext) ? "none" : _registry.SyntheticContext), _text);
        _forceGeneration = GUILayout.Toggle(_forceGeneration, "Force proposal: bypass behavior/context admission");
        if (GUILayout.Button("Edit real learning / offer thresholds")) OpenBrowserTab("Rules");
        bool enabled = GUI.enabled;
        GUI.enabled = _registry.TargetsPlayer && !_registry.GenerationPending && !string.IsNullOrWhiteSpace(_generationBrief);
        if (GUILayout.Button(_registry.GenerationPending ? "Generating… check Results" : "Generate " + kind + " proposal"))
            Submit("generate " + kind + " " + YQDeveloperCommandRegistry.QuoteArgument(_generationBrief) + (_forceGeneration ? " --force" : ""));
        GUI.enabled = enabled;
        GUILayout.Label(_registry.GenerationPending ? "Generation is running. Close the console to play; return here to review the result. Restore is available when it finishes." :
            "Validation may reject a proposal. Results explains why; Advanced exposes the real acquisition settings.", _text);
    }
    private void OpenBrowserTab(string name)
    {
        // note: Entries/actions change together on the next Layout pass.
        _browserTab = Array.IndexOf(BrowserTabs, name); _selectedId = ""; _browserSearch = ""; _browserDirty = true; _showBrowser = true;
    }
}
#endif
