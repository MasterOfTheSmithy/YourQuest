using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: These detached contract checks never load profiles, publish saves or mutate the production player.
public static class YQSpellCircleVerification
{
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("YourQuest/Spells/Verify Circle Contracts %#F9")]
    public static void VerifyContracts()
    {
        Checks.Clear();
        GameObject caster = null;
        string status = "FAIL";
        string failure = null;
        var samples = new List<object>();
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Fresh Unity compilation followed by Edit Mode contract checks");
            // note: Legacy JSON is still authoritative; reading a circle does not replace or rewrite its saved tier.
            SkillRecord legacy = JsonConvert.DeserializeObject<SkillRecord>("{\"skillId\":\"accepted-spell\",\"type\":\"spell\",\"tier\":4,\"rank\":2}");
            Require(YQSpellCircleRules.IsSpell(legacy) && YQSpellCircleRules.GetCircle(legacy) == 4, "Legacy typed spell resolves Circle 4");
            Require(YQSpellCircleRules.GetResourceCost(legacy) == 15f, "Legacy mana cost remains 15");
            JObject roundTrip = JObject.Parse(JsonConvert.SerializeObject(legacy));
            Require((int)roundTrip["tier"] == 4 && roundTrip["circle"] == null && legacy.skillId == "accepted-spell",
                "Existing save shape and accepted ID remain unchanged");
            legacy.tier = 20;
            Require(YQSpellCircleRules.GetCircle(legacy) == 7 && legacy.tier == 20, "Circle view clamps without mutating historical content");
            Require(YQSpellCircleRules.ClampCircle(0) == 1 && YQSpellCircleRules.ClampCircle(99) == 7, "Circle baseline bounds 1-7");

            float duration = YQSpellCircleRules.CalculateCastSeconds(1, 15f, 16);
            Require(YQSpellCircleRules.CalculateCastSeconds(2, 15f, 16) > duration, "Circle independently increases cast time");
            Require(YQSpellCircleRules.CalculateCastSeconds(1, 30f, 16) > duration, "Mana independently increases cast time");
            Require(YQSpellCircleRules.CalculateCastSeconds(1, 15f, 32) > duration, "Delivered strength independently increases cast time");
            Require(!float.IsNaN(YQSpellCircleRules.CalculateCastSeconds(1, float.NaN, 16)), "Malformed mana remains finite");
            SkillRecord free = new SkillRecord { isSpell = true, resourceType = "free", resourceCost = 90 };
            Require(YQSpellCircleRules.GetResourceCost(free) == 0f, "Explicit free spell preserves its resource contract");
            SkillRecord stamina = new SkillRecord { isSpell = true, resourceType = "stamina", resourceCost = 25 };
            Require(Mathf.Approximately(YQSpellCircleRules.GetCastSeconds(stamina),
                YQSpellCircleRules.CalculateCastSeconds(1, 0f, YQSpellCircleRules.GetPower(stamina))), "Stamina cost is not misrepresented as mana");

            PlayerState detached = new PlayerState();
            detached.EnsureCollections();
            PendingProgressionOfferRecord spellOffer = detached.QueueOrRefreshOffer(new PendingProgressionOfferRecord
            {
                offerKind = "spell", name = "Contract spell", skillType = "control", proposedTier = 99
            });
            Require(detached.AcceptOffer(spellOffer.offerId, out string spellMessage), "Detached spell acceptance succeeds");
            SkillRecord accepted = detached.FindSkillByName("Contract spell");
            Require(accepted != null && accepted.isSpell && accepted.tier == 7 && spellMessage.Contains("Circle 7"),
                "Accepted spell offer cannot exceed Circle 7");
            Require(detached.equippedSkillBySlot["spell"] == accepted.skillId, "Typed spell offer uses the spell equipment slot");
            PendingProgressionOfferRecord skillOffer = detached.QueueOrRefreshOffer(new PendingProgressionOfferRecord
            {
                offerKind = "skill", name = "Contract melee discipline", skillType = "combat", proposedTier = 12
            });
            Require(detached.AcceptOffer(skillOffer.offerId, out _), "Detached skill acceptance succeeds");
            Require(detached.FindSkillByName("Contract melee discipline").tier == 12, "Ordinary skill tiers are unchanged");

            caster = new GameObject("Spell circle contract fixture") { hideFlags = HideFlags.HideAndDontSave };
            float previousTime = 0f;
            for (int circle = 1; circle <= 7; circle++)
            {
                SkillRecord spell = new SkillRecord { isSpell = true, tier = circle, rank = 1, resourceType = "mana", resourceCost = 15, targetingMode = "pulse" };
                float castTime = YQSpellCircleRules.GetCastSeconds(spell);
                Require(castTime > previousTime, "Cast time increases at Circle " + circle);
                previousTime = castTime;
                YQSpellCircleVfx vfx = YQGeneratedRuntimeVfx.SpawnSpellCircles(caster.transform, "arcane", circle);
                try
                {
                    Require(vfx.CircleCount == circle && vfx.GetComponentsInChildren<LineRenderer>().Length == circle,
                        "Exactly " + circle + " casting rings");
                    vfx.SetProgress(0f);
                    float startAlpha = vfx.GetComponentsInChildren<LineRenderer>()[0].startColor.a;
                    vfx.SetProgress(0.5f);
                    vfx.SetProgress(1f);
                    Require(vfx.GetComponentsInChildren<LineRenderer>()[0].startColor.a > startAlpha,
                        "Rings charge with cast progress at Circle " + circle);
                    samples.Add(new { circle, manaCost = 15, strength = YQSpellCircleRules.GetPower(spell), castSeconds = castTime, rings = vfx.CircleCount });
                }
                finally { if (vfx != null) UnityEngine.Object.DestroyImmediate(vfx.gameObject); }
            }
            status = "PASS";
        }
        catch (Exception error)
        {
            failure = error.ToString();
            Debug.LogError("[YourQuest Spell Circles] " + failure);
        }
        finally
        {
            if (caster != null) UnityEngine.Object.DestroyImmediate(caster);
            // note: Hash the actual loaded assembly so this receipt cannot be mistaken for evidence from an older patch.
            string receipt = Path.GetFullPath("Docs/Spell_Circles_Contract_Receipt_2026-10-01.json");
            File.WriteAllText(receipt, JsonConvert.SerializeObject(new
            {
                status, utc = DateTime.UtcNow.ToString("O"), mode = "Unity Edit Mode detached contract checks",
                unity = Application.unityVersion, assemblyHash = Hash(typeof(SkillRecord).Assembly.Location),
                rulesHash = Hash("Assets/Assets/Scripts/Data/State/YQSpellCircleRules.cs"),
                combatHash = Hash("Assets/Assets/Scripts/Tutorial/YQInvestorCombat.cs"),
                checks = Checks, samples, failure,
                runtime = "NOT TESTED: input wind-up, release, cancellation and visible first/third-person casting require PlaySafe"
            }, Formatting.Indented));
            Debug.Log("[YourQuest Spell Circles] Contract checks " + status + ": " + receipt);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Checks.Add(label);
    }

    private static string Hash(string path)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
