// Assets/Assets/Scripts/Tutorial/YourQuestTutorialShrine.cs
using System;
using UnityEngine;

public class YourQuestTutorialShrine : MonoBehaviour
{
    public void Interact(GameObject interactor)
    {
        var combat = interactor != null ? interactor.GetComponent<YourQuestTutorialCombat>() : null;
        if (combat != null)
            typeof(YourQuestTutorialCombat).GetField("_currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(combat, combat.maxHealth);

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.AddLedgerLine("The player invoked a shrine and stabilized themselves.");
            PlayerStateManager.Instance.state.IncCounter("interact:shrine", 1f);
            PlayerStateManager.Instance.Save();
        }

        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.State.ApplyFactionDelta("the_archives", "add", 0.05f, "The shrines respond favorably to repeated use.");
            WorldStateManager.Instance.State.AppendCanon("A shrine answered the player inside the tutorial lands.");
            WorldStateManager.Instance.Save();
        }
    }
}
