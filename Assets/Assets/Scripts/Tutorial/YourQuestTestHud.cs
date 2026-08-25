// Assets/Assets/Scripts/Tutorial/YourQuestTestHud.cs
using UnityEngine;

public sealed class YourQuestTestHud : MonoBehaviour
{
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;

    private void EnsureStyles()
    {
        if (boxStyle != null)
            return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.fontSize = 14;
        boxStyle.padding = new RectOffset(12, 12, 12, 12);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 16;
        titleStyle.fontStyle = FontStyle.Bold;

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 13;
        textStyle.wordWrap = true;
    }

    private void OnGUI()
    {
        EnsureStyles();

        var psm = PlayerStateManager.Instance;
        var wsm = WorldStateManager.Instance;
        var ctx = PlayerContext.Instance;
        var acc = EventAccumulator.Instance;
        var player = GameObject.FindGameObjectWithTag("Player");
        var combat = player != null ? player.GetComponent<YourQuestTestCombat>() : null;

        GUILayout.BeginArea(new Rect(12f, 12f, 520f, 340f), boxStyle);
        GUILayout.Label("YourQuest Test Scene", titleStyle);
        GUILayout.Label("WASD move | Mouse look | Shift sprint | Space jump | LMB attack | E talk | F shrine | T world think | Y progression think", textStyle);
        GUILayout.Space(8f);

        if (combat != null)
            GUILayout.Label($"HP: {combat.currentHealth}/{combat.maxHealth}", textStyle);

        if (psm != null && psm.state != null)
            GUILayout.Label($"Player Level: {psm.state.level} | XP: {psm.state.xp} | Region: {psm.state.currentRegionName} ({psm.state.currentRegionId})", textStyle);

        if (ctx != null)
            GUILayout.Label($"Context Region: {ctx.SemanticRegionName} ({ctx.SemanticRegionId})", textStyle);

        if (wsm != null && wsm.State != null)
        {
            GUILayout.Label($"World: {wsm.State.worldName}", textStyle);
            GUILayout.Label($"Tension: {wsm.State.tension:0.00}", textStyle);
            GUILayout.Label($"Last LLM Rationale: {wsm.State.lastLLMRationale}", textStyle);
            GUILayout.Label($"Last LLM Confidence: {wsm.State.lastLLMConfidence:0.00}", textStyle);
        }

        if (acc != null)
            GUILayout.Label($"Buffered Events: {acc.GetEvents().Count}", textStyle);

        GUILayout.EndArea();
    }
}
