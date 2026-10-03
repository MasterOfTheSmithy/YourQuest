using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// note: Exercise the real equipped spell through Input System events; never replace accepted spells, profiles or world state.
public static class YQSpellCircleRuntimeVerification
{
    private static readonly List<string> Checks = new List<string>();
    private static YQInvestorCombat _combat;
    private static YQInvestorVitals _vitals;
    private static PlayerState _owner;
    private static SkillRecord _spell;
    private static string _acceptedSkills;
    private static int _phase;
    private static int _nextFrame;
    private static double _deadline;
    private static float _phaseAt;
    private static float _castAt;
    private static float _duration;
    private static float _counter;
    private static float _resource;
    private static float _lastResource;
    private static float _lastPendingAt;
    private static float _cooldown;
    private static bool _sawWindup;
    private static bool _running;

    [MenuItem("YourQuest/Spells/Inspect Live Spell Menu %#F7")]
    public static void InspectLiveMenu()
    {
        // note: This preparation shortcut opens the existing menu owner; selection and equipping still use its visible controls.
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || _running) return;
        YourQuestTutorialMenuUI menu = UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialMenuUI>();
        if (menu != null) menu.OpenFullMenu();
    }

    [MenuItem("YourQuest/Spells/Run Live Casting Checks %#F8")]
    public static void Run()
    {
        if (_running) return;
        Checks.Clear();
        _spell = null;
        _duration = 0f;
        _nextFrame = Time.frameCount;
        try
        {
            Require(EditorApplication.isPlaying && !EditorApplication.isCompiling, "Fresh compiled PlaySafe session");
            Require(!RuntimeModalUiBlocker.IsBlocked && !YourQuestTutorialMenuUI.CapturesPointerInput, "Gameplay input is available");
            int authoritative = 0;
            foreach (YQInvestorPlayerMotor motor in UnityEngine.Object.FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsSortMode.None))
            {
                if (!motor.IsAuthoritative) continue;
                authoritative++;
                _combat = motor.GetComponent<YQInvestorCombat>();
            }
            Require(authoritative == 1 && _combat != null, "Exactly one authoritative gameplay player");
            _vitals = _combat.GetComponent<YQInvestorVitals>();
            _owner = PlayerStateManager.Instance?.state;
            string id = null;
            _owner?.equippedSkillBySlot?.TryGetValue("spell", out id);
            _spell = _owner?.FindSkillById(id);
            Require(YQSpellCircleRules.IsSpell(_spell) && _vitals != null && !_vitals.IsDead, "An accepted spell is equipped on the living player");
            Require(Mouse.current != null && Keyboard.current != null && !_combat.IsCasting, "Input devices are available and no cast is in flight");
            _acceptedSkills = JsonConvert.SerializeObject(_owner.skills);
            _cooldown = _spell.cooldownSeconds > 0f ? _spell.cooldownSeconds : _combat.spellCooldown;
            _phase = 0;
            _phaseAt = Time.time;
            _deadline = EditorApplication.timeSinceStartup + 60d;
            _running = true;
            EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish("BLOCKED", error.ToString()); }
    }

    private static void Tick()
    {
        if (!_running || Time.frameCount < _nextFrame) return;
        _nextFrame = Time.frameCount + 1;
        try
        {
            RequireCurrentOwner();
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Live spell witness exceeded 60 seconds at phase " + _phase);
            switch (_phase)
            {
                case 0:
                case 3:
                    if (Time.time - _phaseAt < _cooldown + 0.15f) return;
                    _counter = CastCounter();
                    _resource = Resource();
                    Require(_resource >= YQSpellCircleRules.GetResourceCost(_spell), "Resources available before cast " + (_phase == 0 ? "release" : "interruption"));
                    _sawWindup = false;
                    // note: Synthetic input uses the same production input path; report it as a harness, not an unassisted user journey.
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right));
                    _phase++;
                    _phaseAt = Time.time;
                    break;
                case 1:
                case 4:
                    if (!_combat.IsCasting)
                    {
                        if (Time.time - _phaseAt > 2f) throw new InvalidOperationException("Right-click input did not start casting.");
                        return;
                    }
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState());
                    _duration = _combat.CastDurationSeconds;
                    _castAt = Time.time - _combat.CastProgress * _duration;
                    int bonus = GeneratedRpgContentService.Instance != null ? GeneratedRpgContentService.Instance.GetManaBonus(_owner) : 0;
                    Require(Mathf.Approximately(_duration, YQSpellCircleRules.GetCastSeconds(_spell, bonus)), "Live wind-up uses the displayed cast-time formula");
                    YQSpellCircleVfx circles = _combat.GetComponentInChildren<YQSpellCircleVfx>();
                    Require(circles != null && circles.CircleCount == _combat.CastingCircle, "Live ring count matches the equipped circle");
                    if (_phase == 1)
                        ScreenCapture.CaptureScreenshot("Docs/Spell_Circles_Live_Cast_2026-10-01.png");
                    else
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftAlt));
                    _phase++;
                    break;
                case 2:
                    if (_combat.IsCasting)
                    {
                        if (CastCounter() != _counter) throw new InvalidOperationException("Cast counters advanced before release.");
                        if (Resource() + 0.01f < _resource) throw new InvalidOperationException("Resources were spent during wind-up.");
                        _lastResource = Resource();
                        _lastPendingAt = Time.time;
                        _sawWindup = true;
                        return;
                    }
                    Require(_sawWindup && Time.time + 0.001f >= _castAt + _duration, "Spell remains pending until its full cast time");
                    Require(CastCounter() > _counter, "Successful release records the ordinary cast counter");
                    float cost = YQSpellCircleRules.GetResourceCost(_spell);
                    float regen = YQSpellCircleRules.GetResourceType(_spell) == "stamina" ? _vitals.staminaRegenPerSecond : _vitals.manaRegenPerSecond;
                    float tolerance = Mathf.Max(0.5f, regen * (Time.time - _lastPendingAt + 0.05f));
                    Require(cost == 0f || Mathf.Abs((_lastResource - Resource()) - cost) <= tolerance, "Release spends the declared resource cost once (allowing observed regeneration)");
                    _phase = 3;
                    _phaseAt = Time.time;
                    break;
                case 5:
                    if (!YourQuestTutorialMenuUI.CapturesPointerInput || _combat.IsCasting) return;
                    Require(CastCounter() == _counter && Resource() + 0.01f >= _resource, "Opening the live menu cancels without spending resources or recording a cast");
                    Require(_combat.GetComponentInChildren<YQSpellCircleVfx>() == null, "Interrupted casting rings are removed");
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    _phase = 6;
                    break;
                case 6:
                    // note: The realtime menu is held open by Alt; releasing it closes the same presentation owner.
                    if (YourQuestTutorialMenuUI.CapturesPointerInput) return;
                    Require(JsonConvert.SerializeObject(_owner.skills) == _acceptedSkills, "Accepted spell records and IDs remain unchanged");
                    Finish("PASS", null);
                    break;
            }
        }
        catch (Exception error) { Finish("FAIL", error.ToString()); }
    }

    private static void RequireCurrentOwner()
    {
        // note: Never continue a witness against a switched profile, stopped session or destroyed authoritative player.
        if (!EditorApplication.isPlaying || _combat == null || !ReferenceEquals(_owner, PlayerStateManager.Instance?.state))
            throw new InvalidOperationException("The observed gameplay owner changed.");
    }

    private static float Resource() => YQSpellCircleRules.GetResourceType(_spell) == "stamina" ? _vitals.CurrentStamina : _vitals.CurrentMana;

    private static float CastCounter()
    {
        _owner.behaviorCounters.TryGetValue("cast:pulse", out float pulse);
        _owner.behaviorCounters.TryGetValue("cast:projectile", out float projectile);
        return pulse + projectile;
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Checks.Add(label);
    }

    private static void Finish(string status, string failure)
    {
        EditorApplication.update -= Tick;
        if (_running)
        {
            // note: Release only synthetic device state; no profile reload, spell replacement or resource reset is allowed.
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }
        _running = false;
        string assemblyHash;
        using (SHA256 hash = SHA256.Create())
            assemblyHash = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(typeof(SkillRecord).Assembly.Location))).Replace("-", string.Empty).ToLowerInvariant();
        string receipt = Path.GetFullPath("Docs/Spell_Circles_Runtime_Receipt_2026-10-01.json");
        File.WriteAllText(receipt, JsonConvert.SerializeObject(new
        {
            status, utc = DateTime.UtcNow.ToString("O"), mode = "PlaySafe Input System harness using the existing accepted equipped spell",
            assemblyHash, spellId = _spell?.skillId, spellName = _spell?.name, circle = _spell != null ? YQSpellCircleRules.GetCircle(_spell) : 0,
            castSeconds = _duration, checks = Checks, failure,
            screenshot = "Docs/Spell_Circles_Live_Cast_2026-10-01.png",
            remaining = "Other circles are covered by detached contracts; unassisted casting and third-person presentation are not certified by this harness."
        }, Formatting.Indented));
        if (failure != null) Debug.LogError("[YourQuest Spell Circles] " + status + ": " + failure);
        else Debug.Log("[YourQuest Spell Circles] Live casting checks PASS: " + receipt);
    }
}
