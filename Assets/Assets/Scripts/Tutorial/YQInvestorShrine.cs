// Assets/Assets/Scripts/Tutorial/YQInvestorShrine.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorShrine : MonoBehaviour
{
    public int healAmount = 28;

    public void Interact(GameObject interactor)
    {
        if (interactor == null)
            return;

        YQInvestorVitals vitals = interactor.GetComponent<YQInvestorVitals>();
        if (vitals != null)
        {
            vitals.Heal(healAmount);
            vitals.RestoreMana(20f);
            vitals.RestoreStamina(35f);
        }

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.AddLedgerLine("The player restored themselves at a shrine.");
            PlayerStateManager.Instance.state.IncCounter("interact:shrine", 1f);
            if (PlayerStateManager.Instance.autosave)
                PlayerStateManager.Instance.Save();
        }

        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.AddCanonLine("A shrine answered the player in the hub.");
            WorldStateManager.Instance.State.lastLLMRationale = "Shrine use calmed local instability.";
            WorldStateManager.Instance.State.tension = Mathf.Clamp01(WorldStateManager.Instance.State.tension - 0.04f);
            WorldStateManager.Instance.Save();
        }

        YQInvestorDirector director = FindFirstObjectByType<YQInvestorDirector>();
        if (director != null)
            director.NotifyShrineUsed(this);
    }
}
