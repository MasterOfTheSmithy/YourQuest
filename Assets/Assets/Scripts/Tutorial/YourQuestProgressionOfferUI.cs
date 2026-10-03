// Assets/Assets/Scripts/Tutorial/YourQuestProgressionOfferUI.cs
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YourQuestProgressionOfferUI : MonoBehaviour
{
    private Canvas _canvas;
    private TMP_Text _title;
    private TMP_Text _body;
    private TMP_Text _footer;
    private PendingProgressionOfferRecord _current;
    private string _suppressedStartupOfferId = string.Empty;

    private void Awake()
    {
        BuildUi();
        if (_canvas != null)
            _canvas.enabled = false;
    }


    public void ForceHideFromBootstrap()
    {
        _current = null;
        PendingProgressionOfferRecord offer = PlayerStateManager.Instance != null && PlayerStateManager.Instance.state != null
            ? PlayerStateManager.Instance.state.GetActiveOffer()
            : null;
        _suppressedStartupOfferId = offer != null ? offer.offerId : string.Empty;
        if (_canvas != null)
            _canvas.enabled = false;
    }

    private void LateUpdate()
    {
        Render();
        HandleInput();
    }

    private void HandleInput()
    {
        if (_current == null || YQInvestorDialogueUI.IsOpenNow || YourQuestTutorialMenuUI.IsOpenNow || YourQuestPauseMenuUI.IsOpenNow)
            return;
        if (!string.IsNullOrWhiteSpace(_suppressedStartupOfferId) &&
            string.Equals(_current.offerId, _suppressedStartupOfferId, System.StringComparison.OrdinalIgnoreCase))
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.rKey.wasPressedThisFrame)
            AcceptCurrent();
        else if (kb.fKey.wasPressedThisFrame)
            DeclineCurrent();
    }

    private void AcceptCurrent()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null || _current == null)
            return;

        string message;
        if (psm.state.AcceptOffer(_current.offerId, out message))
        {
            GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
            psm.Save();
        }
    }

    private void DeclineCurrent()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null || _current == null)
            return;

        string message;
        if (psm.state.DeclineOffer(_current.offerId, out message))
        {
            GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
            psm.Save();
        }
    }

    private void Render()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        _current = psm != null && psm.state != null ? psm.state.GetActiveOffer() : null;
        bool suppressedStartupOffer = _current != null &&
                                      !string.IsNullOrWhiteSpace(_suppressedStartupOfferId) &&
                                      string.Equals(_current.offerId, _suppressedStartupOfferId, System.StringComparison.OrdinalIgnoreCase);
        if (suppressedStartupOffer && ShouldReleaseSuppressedOffer(psm != null ? psm.state : null))
        {
            _suppressedStartupOfferId = string.Empty;
            suppressedStartupOffer = false;
        }
        if (_current == null)
            _suppressedStartupOfferId = string.Empty;
        bool blocked = YQInvestorDialogueUI.IsOpenNow || YourQuestTutorialMenuUI.IsOpenNow || YourQuestPauseMenuUI.IsOpenNow;
        if (_canvas != null)
            _canvas.enabled = _current != null && !blocked && !suppressedStartupOffer;

        if (_current == null || blocked || suppressedStartupOffer)
            return;

        _title.text = BuildHeading(_current);
        StringBuilder sb = new StringBuilder(256);
        // note: The offer presents its benefit and progression tier; generation confidence stays in diagnostics.
        sb.AppendLine(YQBlueglassText.Description(_current.description));
        if (_current.proposedTier > 0)
            // note: Spell offers show their circle; skill offers retain the existing tier vocabulary.
            sb.Append(YQSpellCircleRules.IsSpell(_current) ? "Circle " + YQSpellCircleRules.ClampCircle(_current.proposedTier) :
                "Tier " + _current.proposedTier);
        if (_current.isUpgrade && !string.IsNullOrWhiteSpace(_current.upgradeTargetName))
            sb.Append("   •   " + Safe(_current.upgradeTargetName, _current.upgradeTargetId));
        _body.text = sb.ToString();
        _footer.text = "R Accept   •   F Decline";
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YourQuestProgressionOfferCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5200;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        // note: Keep the offer toast on the same scale grid as the other runtime UI.
        YQUITheme.ApplyCanvasScaler(scaler);

        RectTransform panel = CreatePanel(canvasGo.transform, "Panel", new Vector2(1f, 0.5f), new Vector2(480f, 244f), new Vector2(-18f, 0f), YQUITheme.Panel);
        panel.pivot = new Vector2(1f, 0.5f);
        YQUITheme.ApplyPanel(panel.GetComponent<Image>());

        _title = CreateText(panel, "Title", 22f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(20f, -16f), new Vector2(440f, 52f));
        _title.color = YQUITheme.Gold;
        _body = CreateText(panel, "Body", 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(20f, -78f), new Vector2(440f, 116f));
        _body.textWrappingMode = TextWrappingModes.Normal;
        _footer = CreateText(panel, "Footer", 18f, FontStyles.Bold, TextAlignmentOptions.BottomRight, new Vector2(20f, -210f), new Vector2(440f, 24f));
        _footer.color = YQUITheme.Muted;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        // note: The color parameter lets tiny popups opt into the shared theme without losing layout helpers.
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = dimensions;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static string BuildHeading(PendingProgressionOfferRecord offer)
    {
        string kind = string.IsNullOrWhiteSpace(offer.offerKind) ? "Offer" : char.ToUpperInvariant(offer.offerKind[0]) + offer.offerKind.Substring(1);
        return offer.isUpgrade ? kind + " Upgrade — " + offer.name : kind + " Offer — " + offer.name;
    }

    private static bool ShouldReleaseSuppressedOffer(PlayerState state)
    {
        QuestRecord quest = state != null ? state.GetActiveQuest() : null;
        if (quest == null)
            return false;

        string text = ((quest.name ?? string.Empty) + " " + (quest.description ?? string.Empty)).ToLowerInvariant();
        return text.Contains("progression offer") || text.Contains("accept or decline");
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
