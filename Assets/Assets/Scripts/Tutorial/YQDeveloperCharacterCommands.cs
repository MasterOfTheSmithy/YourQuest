#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using UnityEngine;

// note: All character writes stay on the existing state owner; the console only selects a typed operation.
public partial class PlayerStateManager
{
    public event Action<string> DevelopmentStateChanged;

    public bool DevelopmentSetStat(string name, float value, bool add, out string result)
    {
        YQDeveloperVariable variable = YQDeveloperVariable.FindStat(name);
        if (variable == null) { result = "Unknown variable. Use stat list."; return false; }
        float before = variable.Read(state);
        float after = add ? before + value : value;
        if (!variable.Validate(after, out result)) return false;
        if (name == "xp")
        {
            if (state.level > 40) { result = "XP edits above console safety level 40 are unsupported."; return false; }
            if (add && value > 0)
            {
                if (state.level == 40 && after >= PlayerState.GetXpRequiredForLevel(40))
                { result = "XP would exceed console safety level 40."; return false; }
                GrantXp((int)value);
            }
            else
            {
                if (after >= PlayerState.GetXpRequiredForLevel(state.level))
                { result = "XP is current-level progress; set below xpToNext, or use stat add xp for level-ups."; return false; }
                state.xp = (int)after;
            }
        }
        else if (name == "level")
        {
            if (state.xp >= PlayerState.GetXpRequiredForLevel((int)after))
            { result = "Existing XP exceeds the requested level's progress range. Lower XP first."; return false; }
            state.level = (int)after;
        }
        else variable.Write(state, after);
        DevelopmentNotify("stat " + name + ": " + before + " -> " + variable.Read(state) + " " + variable.Unit);
        result = "SESSION " + name + "=" + variable.Read(state) + " " + variable.Unit + "; level=" + state.level + ", xpToNext=" + state.xpToNext;
        if (GeneratedRpgContentService.Instance != null)
            result += "; derivedMaxHealth=" + GeneratedRpgContentService.Instance.GetDerivedMaxHealth(state);
        return true;
    }

    public bool DevelopmentSetSkill(SkillRecord template, int rank, bool revoke, out string result)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.skillId)) { result = "Unknown skill ID."; return false; }
        if (rank < 1 || rank > 100) { result = "Rank requires an integer 1..100 (console safety range)."; return false; }
        if (!string.IsNullOrWhiteSpace(template.parentSkillId) && state.FindSkillById(template.parentSkillId) == null)
        { result = "Missing parent skill " + template.parentSkillId + "; force cannot create a dangling parent reference."; return false; }
        SkillRecord record = state.FindSkillById(template.skillId);
        if (revoke && record == null) { result = "Target does not own that skill."; return false; }
        if (record == null)
        {
            record = Newtonsoft.Json.JsonConvert.DeserializeObject<SkillRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(template));
            // note: Preflight the existing similarity normalizer so a forced template cannot replace an accepted skill with a different identity.
            var preview = new PlayerState { skills = new List<SkillRecord>(state.skills) };
            preview.UpsertSkill(record);
            if (preview.skills.Count != state.skills.Count + 1)
            { result = "An accepted skill already matches this template. Use its actual accepted ID from skill list."; return false; }
            state.UpsertSkill(record);
            if (!ReferenceEquals(state.FindSkillById(record.skillId), record))
            { result = "Similarity normalization selected another skill; use its accepted ID."; return false; }
        }
        record.unlocked = !revoke; record.rank = rank;
        if (YQSpellCircleRules.IsSpell(record)) record.tier = YQSpellCircleRules.ClampCircle(record.tier);
        // note: Locked records retain identity/family references but cannot remain in any executable equipment slot.
        if (revoke)
        {
            var slots = new List<string>();
            foreach (var slot in state.equippedSkillBySlot)
                if (string.Equals(slot.Value, record.skillId, StringComparison.OrdinalIgnoreCase)) slots.Add(slot.Key);
            foreach (string slot in slots) state.equippedSkillBySlot.Remove(slot);
        }
        DevelopmentNotify((revoke ? "Revoked " : "Granted ") + record.skillId + " rank=" + rank);
        result = "SESSION " + (revoke ? "revoked " : "granted ") + record.name + " id=" + record.skillId + " rank=" + record.rank;
        return true;
    }

    public bool DevelopmentItem(InventoryItemRecord template, int count, bool remove, out string result)
    {
        if (template == null || count < 1 || count > 10000) { result = "Valid item ID and count 1..10000 required."; return false; }
        InventoryItemRecord current = state.FindInventoryItemById(template.itemId);
        if (remove)
        {
            if (current == null || current.quantity < count) { result = "Insufficient owned quantity."; return false; }
            if (current.quantity == count)
            {
                // note: Equipped items must be deliberately unequipped via the normal menu before removal; no dangling slot links.
                foreach (string equipped in state.equippedItemBySlot.Values)
                    if (string.Equals(equipped, current.itemId, StringComparison.OrdinalIgnoreCase))
                    { result = "Unequip this item in the normal equipment menu before removing the final copy."; return false; }
                state.inventoryItems.Remove(current);
            }
            else current.quantity -= count;
        }
        else
        {
            if (!template.stackable && count != 1) { result = "Non-stackable items accept count 1; each copy receives its own ID."; return false; }
            InventoryItemRecord copy = Newtonsoft.Json.JsonConvert.DeserializeObject<InventoryItemRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(template));
            copy.itemId = Guid.NewGuid().ToString("N"); copy.quantity = count;
            // note: Shared inventory rules compare all mechanics/bindings and cap stacks; the legacy template-only merger would bypass those invariants.
            if (copy.stackable && count > YQContainerInventory.MaxStack)
            { result = "Give at most " + YQContainerInventory.MaxStack + " per stack."; return false; }
            InventoryItemRecord compatible = state.inventoryItems.Find(owned => YQContainerInventory.CanStack(owned, copy) &&
                (long)owned.quantity + count <= YQContainerInventory.MaxStack);
            if (compatible != null) { copy.itemId = compatible.itemId; copy.quantity += compatible.quantity; }
            else if (state.inventoryCapacity > 0 && state.inventoryItems.Count >= state.inventoryCapacity)
            { result = "Player inventory capacity reached; force cannot bypass inventory invariants."; return false; }
            state.AddOrUpdateItem(copy, false);
        }
        DevelopmentNotify((remove ? "Removed " : "Gave ") + count + " x " + template.displayName);
        result = "SESSION " + (remove ? "removed " : "gave ") + count + " x " + template.displayName;
        return true;
    }

    public void DevelopmentNotify(string message)
    {
        state.Touch();
        GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
        DevelopmentStateChanged?.Invoke(message);
    }
}

public sealed class YQDeveloperVariable
{
    public readonly string Name, Unit;
    public readonly float Min, Max;
    public readonly bool Integer;
    public readonly Func<PlayerState, float> Read;
    public readonly Action<PlayerState, float> Write;
    public YQDeveloperVariable(string name, string unit, float min, float max, bool integer, Func<PlayerState, float> read, Action<PlayerState, float> write)
    { Name = name; Unit = unit; Min = min; Max = max; Integer = integer; Read = read; Write = write; }
    public bool Validate(float value, out string error)
    {
        bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && value >= Min && value <= Max && (!Integer || value == Mathf.Floor(value));
        error = valid ? "" : Name + " requires " + (Integer ? "an integer " : "a finite number ") + Min + ".." + Max + " " + Unit + " (console safety range).";
        return valid;
    }
    public static YQDeveloperVariable FindStat(string name) => Array.Find(Stats, v => v.Name == name);
    public static readonly YQDeveloperVariable[] Stats =
    {
        new YQDeveloperVariable("vitality", "points", 1, 10000, true, s=>s.stats.vitality, (s,v)=>s.stats.vitality=(int)v),
        new YQDeveloperVariable("strength", "points", 1, 10000, true, s=>s.stats.strength, (s,v)=>s.stats.strength=(int)v),
        new YQDeveloperVariable("dexterity", "points", 1, 10000, true, s=>s.stats.dexterity, (s,v)=>s.stats.dexterity=(int)v),
        new YQDeveloperVariable("intelligence", "points", 1, 10000, true, s=>s.stats.intelligence, (s,v)=>s.stats.intelligence=(int)v),
        new YQDeveloperVariable("maxHealth", "HP", 1, 100000, true, s=>s.stats.maxHealth, (s,v)=>s.stats.maxHealth=(int)v),
        new YQDeveloperVariable("maxStamina", "stamina points", 1, 100000, true, s=>s.stats.maxStamina, (s,v)=>s.stats.maxStamina=(int)v),
        new YQDeveloperVariable("maxMana", "mana points", 1, 100000, true, s=>s.stats.maxMana, (s,v)=>s.stats.maxMana=(int)v),
        new YQDeveloperVariable("attack", "power points", 0, 10000, true, s=>s.stats.attack, (s,v)=>s.stats.attack=(int)v),
        new YQDeveloperVariable("defense", "power points", 0, 10000, true, s=>s.stats.defense, (s,v)=>s.stats.defense=(int)v),
        new YQDeveloperVariable("critChance", "probability (0..1)", 0, 1, false, s=>s.stats.critChance, (s,v)=>s.stats.critChance=v),
        new YQDeveloperVariable("moveSpeed", "meters/second", .1f, 1000, false, s=>s.stats.moveSpeed, (s,v)=>s.stats.moveSpeed=v),
        new YQDeveloperVariable("level", "levels", 1, 40, true, s=>s.level, (s,v)=>s.level=(int)v),
        new YQDeveloperVariable("xp", "current-level XP", 0, 1000000, true, s=>s.xp, (s,v)=>s.xp=(int)v),
        new YQDeveloperVariable("currency", "gold", 0, 1000000, true, s=>s.currency, (s,v)=>s.currency=(int)v),
        new YQDeveloperVariable("rewardBudget", "reward budget units", 0, 100000, false, s=>s.rewardBudget, (s,v)=>s.rewardBudget=v)
    };
}
#endif
