using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// note: Shared presentation assets do not own gameplay records, input, or pause state.
public static class YQBlueglassStyle
{
    private static Dictionary<string, Sprite> _sprites;
    private static GUIStyle _guiButton;

    public static Sprite Sprite(string name)
    {
        if (_sprites == null)
        {
            _sprites = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/Blueglass/BlueglassChrome"))
                _sprites[sprite.name] = sprite;
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/Blueglass/ItemPortraits"))
                _sprites[sprite.name] = sprite;
        }
        return _sprites.TryGetValue(name, out Sprite result) ? result : null;
    }

    public static void Panel(Image image)
    {
        if (image == null) return;
        // note: Gameplay surfaces use native geometry; startup/legacy surfaces keep their established atlas assets.
        if(image.GetComponentInParent<YourQuestTutorialMenuUI>()!=null || image.GetComponentInParent<YourQuestTutorialHud>()!=null || image.GetComponentInParent<YQGeneratedWorldMinimap>()!=null)
        {
            var form=image.name=="Header" || image.name=="Footer" ? YQBlueglassSurface.Form.Rule :
                image.name=="ObjectivePanel" ? YQBlueglassSurface.Form.Tracker : YQBlueglassSurface.Form.Panel;
            YQBlueglassSurface.Attach(image,form);
            return;
        }
        Sprite sprite = Sprite("Panel_Dark");
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = Color.white;
        Outline outline = image.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    public static void Button(Button button, bool selected)
    {
        if (button == null) return;
        if(button.GetComponentInParent<YourQuestTutorialMenuUI>()!=null || button.GetComponentInParent<YQBlueglassAbilityHotbar>()!=null)
        {
            // note: Keep the existing button and its listeners; native chrome receives explicit hover/selection state without textured distortion.
            Image target=button.GetComponent<Image>();
            var form=button.name.StartsWith("EquipmentSlot_") ? YQBlueglassSurface.Form.Socket :
                button.name=="InventoryListButton" || button.name=="ListButton" ? YQBlueglassSurface.Form.Tile : YQBlueglassSurface.Form.Navigation;
            var native=YQBlueglassSurface.Attach(target,form);
            button.transition=Selectable.Transition.None;
            var response=button.GetComponent<YQBlueglassControlFeedback>();
            if(response==null) response=button.gameObject.AddComponent<YQBlueglassControlFeedback>();
            response.Surface=native;
            response.SetSelected(selected);
            return;
        }
        string family = button.name.StartsWith("EquipmentSlot_") ? "Socket" :
            button.name == "InventoryListButton" ? "Tile" :
            button.name.Contains("Nav") || button.name.StartsWith("Quick") ? "Navigation" : "Action";
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        Sprite idle = Sprite(family + "_Idle");
        if (image != null && idle != null)
        {
            image.sprite = Sprite(family + (selected ? family == "Action" ? "_Active" : "_Selected" : "_Idle"));
            image.overrideSprite = null;
            image.color = Color.white;
            image.type = family == "Socket" ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = family == "Socket";
            image.pixelsPerUnitMultiplier = 2f;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = Sprite(family == "Action" ? "Action_Active" : family + "_Hover");
            state.selectedSprite = state.highlightedSprite;
            state.pressedSprite = Sprite(family == "Navigation" ? "Navigation_Pressed" : family == "Action" ? "Action_Active" : family + "_Selected");
            state.disabledSprite = Sprite("Tile_Disabled");
            button.spriteState = state;
        }
        var feedback = button.GetComponent<YQBlueglassControlFeedback>();
        if (feedback == null) feedback = button.gameObject.AddComponent<YQBlueglassControlFeedback>();
        feedback.SetSelected(selected);
        feedback.RefreshHover();
    }

    public static void AttachCanvas(CanvasScaler scaler)
    {
        if (scaler != null && scaler.GetComponent<YQBlueglassCanvasSkin>() == null)
            scaler.gameObject.AddComponent<YQBlueglassCanvasSkin>();
    }

    public static Sprite ItemPortrait(InventoryItemRecord item)
    {
        if (item == null) return null;
        // note: Explicit presentation fallback by structured slot/type; generated prose never determines mechanics.
        switch ((item.equipSlot ?? string.Empty).ToLowerInvariant())
        {
            case "weapon": case "offhand": return Sprite("Portrait_Sword");
            case "head": return Sprite("Portrait_Head");
            case "chest": case "legs": case "gloves": case "cloak": return Sprite("Portrait_Coat");
            case "boots": return Sprite("Portrait_Boots");
            case "ring_left": case "ring_right": case "necklace": case "trinket": return Sprite("Portrait_Ring");
        }
        if (item.IsConsumable) return Sprite("Portrait_Tonic");
        if (string.Equals(item.itemType, "readable", System.StringComparison.OrdinalIgnoreCase)) return Sprite("Portrait_Map");
        return null;
    }

    public static void DrawGuiFrame(Rect target, string name)
    {
        if (Event.current.type != EventType.Repaint) return;
        // note: Small startup controls use resolution-independent rails, avoiding oversized atlas corners and per-repaint slicing arrays.
        bool selected = name.Contains("Selected") || name.Contains("Active");
        bool emphasis = selected || name.Contains("Hover") || name.Contains("Pressed");
        bool disabled = name.Contains("Disabled");
        Color previous = GUI.color;
        Color fill = emphasis ? new Color(.08f,.23f,.34f,.94f) : new Color(.018f,.05f,.085f,.82f);
        if (disabled) fill.a = .4f;
        GuiRect(target, fill, previous);
        Color line = emphasis ? new Color(.70f,.92f,1f,.95f) : new Color(.48f,.69f,.82f,.38f);
        if (disabled) line.a = .15f;
        GuiRect(new Rect(target.x,target.y,target.width,1),line,previous);
        GuiRect(new Rect(target.x,target.yMax-1,target.width,1),line,previous);
        GuiRect(new Rect(target.x,target.y,1,target.height),line,previous);
        GuiRect(new Rect(target.xMax-1,target.y,1,target.height),line,previous);
        if (selected) GuiRect(new Rect(target.x+1,target.y+6,3,Mathf.Max(0,target.height-12)),line,previous);
        GUI.color = previous;
    }

    private static void GuiRect(Rect rect, Color color, Color tint)
    {
        GUI.color = color * tint;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    public static bool GuiButton(Rect rect,string label)
    {
        // note: Keep native IMGUI hit/focus behavior while sharing the atlas with legacy startup recovery controls.
        if (_guiButton == null)
        {
            // note: A clean style avoids inheriting editor/skin scaled backgrounds over the authored frame.
            _guiButton=new GUIStyle(GUIStyle.none) {fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            _guiButton.normal.textColor=_guiButton.hover.textColor=_guiButton.active.textColor=_guiButton.focused.textColor=Color.white;
        }
        DrawGuiFrame(rect,rect.Contains(Event.current.mousePosition) ? "Navigation_Hover" : "Navigation_Idle");
        bool clicked=GUI.Button(rect,label,_guiButton);
        if (clicked) YQBlueglassFeedback.Request(YQBlueglassCue.Select,YQBlueglassFeedback.ControllerActive);
        return clicked;
    }
}
