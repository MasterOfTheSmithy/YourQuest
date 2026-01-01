using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds pending upgrade offers and provides a simple accept/decline flow.
/// Swap OnGUI out later for a proper UI panel.
/// </summary>
public class UpgradeOfferManager : MonoBehaviour
{
    public static UpgradeOfferManager Instance { get; private set; }

    [Header("References")]
    public PlayerProfile playerProfile;

    private readonly Queue<UpgradeOffer> offers = new();
    private UpgradeOffer? activeOffer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (activeOffer == null && offers.Count > 0)
            activeOffer = offers.Dequeue();
    }

    /// <summary>
    /// Called when an upgrade (Tier 2+) is committed.
    /// Creates an offer instead of auto replacing.
    /// </summary>
    public void OfferReplacementIfRelevant(SkillData newSkill, SkillData parentSkill)
    {
        if (newSkill == null || parentSkill == null) return;
        if (playerProfile == null)
        {
            Debug.LogWarning("[UpgradeOffer] No PlayerProfile assigned.");
            return;
        }

        var equippedId = playerProfile.GetEquippedSkillId(newSkill.type);

        bool parentEquipped = !string.IsNullOrWhiteSpace(equippedId) && equippedId == parentSkill.skillId;
        bool slotOccupied = !string.IsNullOrWhiteSpace(equippedId);

        if (!slotOccupied)
        {
            playerProfile.EquipSkill(newSkill);
            PlayerStateManager.Instance?.EquipSkill(newSkill);
            return;
        }

        offers.Enqueue(new UpgradeOffer
        {
            newSkill = newSkill,
            parentSkill = parentSkill,
            currentlyEquippedSkillId = equippedId,
            reason = parentEquipped
                ? "Upgraded equipped skill"
                : "Higher-tier option for this slot"
        });
    }

    private void Accept()
    {
        if (activeOffer == null) return;
        var offer = activeOffer.Value;

        playerProfile.ReplaceEquippedSkill(offer.newSkill);
        PlayerStateManager.Instance?.EquipSkill(offer.newSkill);

        activeOffer = null;
    }

    private void Decline()
    {
        activeOffer = null;
    }

    private void OnGUI()
    {
        if (activeOffer == null) return;
        var offer = activeOffer.Value;

        GUILayout.BeginArea(new Rect(20, 20, 520, 160), GUI.skin.box);
        GUILayout.Label("<b>Upgrade Available</b>", new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.Label(
            $"New: {offer.newSkill.skillName} (Tier {offer.newSkill.tier})\n" +
            $"Upgrades: {offer.parentSkill.skillName} (Tier {offer.parentSkill.tier})\n" +
            $"Slot: {offer.newSkill.type}\n" +
            $"Reason: {offer.reason}"
        );

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Replace Equipped")) Accept();
        if (GUILayout.Button("Keep Current")) Decline();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    public struct UpgradeOffer
    {
        public SkillData newSkill;
        public SkillData parentSkill;
        public string currentlyEquippedSkillId;
        public string reason;
    }
}
