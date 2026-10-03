using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQBlueglassCanvasSkin : MonoBehaviour
{
    private void Start() => Apply();
    public void Apply()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // note: One setup pass skins existing UI; dynamic menu rows use YQUITheme when constructed, never a per-frame scene scan.
        foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
        {
            if (control is Button button) YQBlueglassStyle.Button(button, button.GetComponent<YQBlueglassControlFeedback>()?.Selected ?? false);
            else if (!(control is Scrollbar))
            {
                if (control.GetComponent<YQBlueglassControlFeedback>() == null)
                    control.gameObject.AddComponent<YQBlueglassControlFeedback>();
                if (control is TMP_InputField || control is TMP_Dropdown)
                    YQBlueglassStyle.Panel(control.GetComponent<Image>());
            }
        }
        foreach (ScrollRect scroll in GetComponentsInChildren<ScrollRect>(true))
        {
            YQBlueglassScrollAffordance.Attach(scroll);
            if (scroll.GetComponent<YQBlueglassControlFeedback>() == null)
                scroll.gameObject.AddComponent<YQBlueglassControlFeedback>();
        }
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Selectable>() != null || image.GetComponent<Mask>() != null) continue;
            string n = image.name;
            // note: Scroll/mask interiors keep their owner's clipping and flat substrate rather than acquiring a second glass frame.
            if (n=="DetailScrollRoot" || n=="ListArea" || n=="MapBorder" || n=="ClassBadgeFrame" || n=="QuestIconFrame") continue;
            if (n == "Dim" || n == "Backdrop" || n.StartsWith("__YQ_") || n.Contains("Fill") || n.Contains("Track") || n.Contains("Guide") || n.Contains("Interaction") || n.Contains("Selection")) continue;
            if (image.transform.childCount > 0 && image.color.a >= .25f && image.rectTransform.rect.width >= 100f && image.rectTransform.rect.height >= 48f)
                YQBlueglassStyle.Panel(image);
        }
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
    }
}
