// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypeHUD.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif

public sealed class YQPrototypeHUD : MonoBehaviour
{
    [SerializeField] private bool visible = true;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.BackQuote;
#endif

    private Canvas _canvas;
    private Text _text;
    private Text _hintText;
    public YQPrototypeTutorialDirector tutorialDirector;
    private void Awake()
    {
        BuildUi();
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            visible = !visible;
            if (_canvas != null)
            {
                _canvas.enabled = visible;
            }
        }

        if (!visible || _text == null)
        {
            return;
        }

        RenderHud();
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(legacyToggleKey);
#endif
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YQPrototypeHUDCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        GameObject hudGo = new GameObject("HUDText");
        hudGo.transform.SetParent(canvasGo.transform, false);

        _text = hudGo.AddComponent<Text>();
        _text.font = font;
        _text.fontSize = 16;
        _text.alignment = TextAnchor.UpperLeft;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.color = Color.white;

        RectTransform hudRect = _text.rectTransform;
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(16f, -16f);
        hudRect.sizeDelta = new Vector2(760f, 920f);

        GameObject hintGo = new GameObject("HUDHintText");
        hintGo.transform.SetParent(canvasGo.transform, false);

        _hintText = hintGo.AddComponent<Text>();
        _hintText.font = font;
        _hintText.fontSize = 14;
        _hintText.alignment = TextAnchor.LowerLeft;
        _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _hintText.verticalOverflow = VerticalWrapMode.Overflow;
        _hintText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        RectTransform hintRect = _hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0f);
        hintRect.anchoredPosition = new Vector2(16f, 16f);
        hintRect.sizeDelta = new Vector2(960f, 120f);

        _canvas.enabled = visible;
    }

    private void RenderHud()
    {
        StringBuilder sb = new StringBuilder(4096);

        sb.AppendLine("YOURQUEST PROTOTYPE");
        sb.AppendLine();

        AppendPlayerBlock(sb);
        AppendWorldBlock(sb);
        AppendHintBlock();

        _text.text = sb.ToString();
    }

    private void AppendPlayerBlock(StringBuilder sb)
    {
        PlayerStateManager playerStateManager = FindFirstObjectByType<PlayerStateManager>();
        if (playerStateManager == null)
        {
            sb.AppendLine("PLAYER");
            sb.AppendLine("  PlayerStateManager unavailable");
            sb.AppendLine();
            return;
        }

        object state = GetFieldOrPropertyValue(playerStateManager, "state");
        if (state == null)
        {
            sb.AppendLine("PLAYER");
            sb.AppendLine("  state unavailable");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("PLAYER");

        AppendNamedValue(sb, "Name", GetBestString(state, new[] { "playerName", "name", "displayName" }));
        AppendNamedValue(sb, "Level", GetBestString(state, new[] { "level", "playerLevel" }));
        AppendNamedValue(sb, "XP", GetBestString(state, new[] { "xp", "experience" }));
        AppendNamedValue(sb, "Region", GetBestString(state, new[] { "currentRegionId", "currentRegionName", "regionId" }));
        AppendNamedValue(sb, "Class", GetBestString(state, new[] { "currentClassId", "classId", "currentClass", "playerClass" }));

        AppendNamedCollection(sb, "Titles", GetBestEnumerable(state, new[] { "titles", "titleIds" }));
        AppendNamedCollection(sb, "Skills", GetBestEnumerable(state, new[] { "skills", "skillIds", "unlockedSkills" }));
        AppendNamedCollection(sb, "Quests", GetBestEnumerable(state, new[] { "activeQuestIds", "quests", "questIds" }));

        AppendNamedValue(sb, "Behavior Summary", GetBehaviorSummary(state));

        sb.AppendLine();
    }

    private void AppendWorldBlock(StringBuilder sb)
    {
        WorldStateManager worldStateManager = FindFirstObjectByType<WorldStateManager>();
        if (worldStateManager == null)
        {
            sb.AppendLine("WORLD");
            sb.AppendLine("  WorldStateManager unavailable");
            sb.AppendLine();
            return;
        }

        object state = GetFieldOrPropertyValue(worldStateManager, "state");
        if (state == null)
        {
            sb.AppendLine("WORLD");
            sb.AppendLine("  state unavailable");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("WORLD");

        AppendNamedValue(sb, "Tension", GetBestString(state, new[] { "tension", "worldTension", "threatLevel" }));
        AppendNamedValue(sb, "Last Event", GetBestString(state, new[] { "lastEventSummary", "lastEvent", "summary", "lastRationale" }));

        IEnumerable canon = GetBestEnumerable(state, new[] { "canonLedger", "worldLore", "history", "eventLog" });
        if (canon != null)
        {
            List<string> lines = ToStringList(canon, 5);
            if (lines.Count > 0)
            {
                sb.AppendLine("  Canon:");
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.Append("    - ");
                    sb.AppendLine(lines[i]);
                }
            }
            else
            {
                sb.AppendLine("  Canon: <none>");
            }
        }
        else
        {
            sb.AppendLine("  Canon: <none>");
        }

        sb.AppendLine();
    }

    private void AppendHintBlock()
    {
#if ENABLE_INPUT_SYSTEM
        string toggleHint = "Backquote (`) toggles HUD";
#else
        string toggleHint = legacyToggleKey + " toggles HUD";
#endif

        if (_hintText != null)
        {
            _hintText.text =
                "Controls: move with your existing PlayerController bindings, interact, attack, tutorial systems\n" +
                toggleHint;
        }
    }

    private static void AppendNamedValue(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sb.Append("  ");
        sb.Append(label);
        sb.Append(": ");
        sb.AppendLine(value);
    }

    private static void AppendNamedCollection(StringBuilder sb, string label, IEnumerable values)
    {
        if (values == null)
        {
            return;
        }

        List<string> items = ToStringList(values, 8);
        if (items.Count == 0)
        {
            return;
        }

        sb.Append("  ");
        sb.Append(label);
        sb.Append(": ");
        sb.AppendLine(string.Join(", ", items));
    }

    private static string GetBehaviorSummary(object state)
    {
        IEnumerable ledger = GetBestEnumerable(state, new[] { "behaviorLedger", "ledger", "playerLedger" });
        if (ledger != null)
        {
            List<string> items = ToStringList(ledger, 3);
            if (items.Count > 0)
            {
                return string.Join(" | ", items);
            }
        }

        object counters = GetFieldOrPropertyValue(state, "behaviorCounters");
        if (counters != null)
        {
            Type counterType = counters.GetType();
            if (typeof(IDictionary).IsAssignableFrom(counterType))
            {
                IDictionary dict = (IDictionary)counters;
                List<string> parts = new List<string>();
                int taken = 0;

                foreach (DictionaryEntry entry in dict)
                {
                    if (taken >= 4)
                    {
                        break;
                    }

                    parts.Add(Convert.ToString(entry.Key) + "=" + Convert.ToString(entry.Value));
                    taken++;
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
        }

        return null;
    }

    private static string GetBestString(object target, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            object value = GetFieldOrPropertyValue(target, names[i]);
            if (value == null)
            {
                continue;
            }

            string text = ConvertValueToString(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static IEnumerable GetBestEnumerable(object target, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            object value = GetFieldOrPropertyValue(target, names[i]);
            if (value == null || value is string)
            {
                continue;
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable;
            }
        }

        return null;
    }

    private static object GetFieldOrPropertyValue(object target, string name)
    {
        if (target == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Type type = target.GetType();

        FieldInfo field = type.GetField(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo property = type.GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try
            {
                return property.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static List<string> ToStringList(IEnumerable values, int maxItems)
    {
        List<string> result = new List<string>();
        if (values == null)
        {
            return result;
        }

        foreach (object item in values)
        {
            if (result.Count >= maxItems)
            {
                break;
            }

            string text = ConvertValueToString(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static string ConvertValueToString(object value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        return Convert.ToString(value);
    }
}
