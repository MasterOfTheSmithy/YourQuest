// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypeShrine.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class YQPrototypeShrine : MonoBehaviour
{
    public int healAmount = 30;
    public float manaRestore = 20f;
    public float staminaRestore = 30f;
    public float interactDistance = 3f;
    public string shrineLabel = "Shrine";

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        float dist = Vector3.Distance(player.transform.position, transform.position);
        if (dist > interactDistance)
            return;

        var vitals = player.GetComponent<YQPrototypePlayerVitals>();
        if (vitals != null)
        {
            vitals.Heal(healAmount);
            vitals.currentMana = Mathf.Min(vitals.maxMana, vitals.currentMana + manaRestore);
            vitals.currentStamina = Mathf.Min(vitals.maxStamina, vitals.currentStamina + staminaRestore);
            vitals.SetRespawnPoint(transform.position + Vector3.up * 0.25f);
        }

        if (ActionRecorder.Instance != null)
            ActionRecorder.Instance.RecordInteract(gameObject);

        if (WorldStateManager.Instance != null)
        {
            string line = $"Shrine used: {shrineLabel}";
            WorldStateManager.Instance.AddCanonLine(line);
            WorldStateManager.Instance.Save();
        }

        if (PlayerStateManager.Instance != null)
        {
            PlayerState state = PlayerStateManager.Instance.state;
            if (state != null)
            {
                state.AddLedgerLine($"Used shrine '{shrineLabel}' to recover.");
                state.IncCounter("interact:shrine", 1f);
                state.IncCounter($"shrine:{SanitizeKey(shrineLabel)}", 1f);

                if (PlayerStateManager.Instance.autosave)
                    PlayerStateManager.Instance.Save();
            }
        }
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
