// Assets/Assets/Scripts/Tutorial/YourQuestTestShrine.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YourQuestTestShrine : MonoBehaviour
{
    public int healAmount = 15;
    public float useCooldown = 1.5f;
    private float nextUseTime;

    public bool TryUse(GameObject user)
    {
        if (Time.time < nextUseTime)
            return false;

        nextUseTime = Time.time + useCooldown;

        var combat = user != null ? user.GetComponent<YourQuestTestCombat>() : null;
        if (combat != null)
            combat.Heal(healAmount);

        var recorder = user != null ? user.GetComponent<ActionRecorder>() : null;
        if (recorder != null)
            recorder.RecordInteract(gameObject);

        var wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.State != null)
        {
            string factionId = "none";
            var info = GetComponent<EntityInfo>();
            if (info != null && !string.IsNullOrWhiteSpace(info.factionId))
                factionId = info.factionId;

            wsm.State.ApplyFactionDelta(factionId, "add", 0.05f, "The shrine recognized the player.");
            wsm.State.ApplyFlagDelta("shrine_resonance", "add", 1f, "A shrine was activated in the test scene.");
            wsm.Save();
        }

        return true;
    }
}
