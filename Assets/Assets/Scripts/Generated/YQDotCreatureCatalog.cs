using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQDotCreatureEntry
{
    public string assetId, kind, species, category, bodyForm, compatibilityId, moduleSlot, prefabPath;
    public string[] sourcePaths = Array.Empty<string>();
    public string[] sourceHashes = Array.Empty<string>();
    public string[] sourceRendererNames = Array.Empty<string>();
    public float authoredWalkSpeed;
    public bool requiresSlotReplacement;
    public YQDotMotionProfile motionProfile;
}

// note: This catalog binds approved presentation to accepted semantic records; it owns no player, world, inventory or creature state.
[CreateAssetMenu(menuName = "YourQuest/DOT Creature Catalog")]
public sealed class YQDotCreatureCatalog : ScriptableObject
{
    public const string ResourcePath = "Player/YQDotCreatureCatalog";
    public List<YQDotCreatureEntry> entries = new List<YQDotCreatureEntry>();
    private static YQDotCreatureCatalog _current;
    private static bool _loaded;
    public static YQDotCreatureCatalog Current
    {
        get { if (!_loaded) { _current = Resources.Load<YQDotCreatureCatalog>(ResourcePath); _loaded = true; } return _current; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { _current = null; _loaded = false; }

    public bool TryGetPrefab(string path, out YQDotCreatureEntry entry)
    {
        foreach (var candidate in entries)
            if (candidate != null && string.Equals(path, candidate.prefabPath, StringComparison.OrdinalIgnoreCase))
            { entry = candidate; return true; }
        entry = null; return false;
    }

    public static bool TryResolve(YQRuntimeWorldAssetRegistry registry, string semantics, string kind, string seed,
        out YQRuntimeWorldAssetEntry result, out string category)
    {
        result = null; category = string.Empty;
        var catalog = Current;
        if (catalog == null || registry == null) return false;
        // note: Whole semantic tokens select only a declared species, leaving legacy family pools and their deterministic choices unchanged.
        string requested = FindSpecies(semantics);
        if (requested == null) return false;
        var candidates = new List<YQDotCreatureEntry>();
        foreach (var entry in catalog.entries)
            if (entry != null && entry.kind == kind && entry.species == requested && string.IsNullOrEmpty(entry.moduleSlot)) candidates.Add(entry);
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => StringComparer.Ordinal.Compare(a.assetId, b.assetId));
        uint hash = 2166136261u;
        unchecked { foreach (char c in seed ?? string.Empty) { hash ^= c; hash *= 16777619u; } }
        var selected = candidates[(int)(hash % (uint)candidates.Count)];
        GameObject prefab = registry.ResolvePrefab(selected.prefabPath);
        if (prefab == null) return false;
        result = new YQRuntimeWorldAssetEntry { assetPath = selected.prefabPath, prefab = prefab };
        category = selected.category; return true;
    }

    public static string FindSpecies(string semantics)
    {
        string normalized = (semantics ?? string.Empty).ToLowerInvariant();
        string[] tokens = normalized.Split(new[] { ' ', '_', '-', '/', '\\', ',', '.', ':', ';', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
        {
            switch (token)
            {
                case "elf": case "elves": case "elven": return "elf";
                case "lizardman": case "lizardmen": return "lizardmen";
                case "orc": case "orcs": case "orcish": return "orc";
                case "satyr": case "satyrs": return "satyr";
                case "avian": case "avians": return "avian";
                // note: Newly delivered bases enter the existing semantic binder without changing prior species pools.
                case "dwarf": case "dwarves": case "dwarven": return "dwarf";
                case "kitsune": return "kitsune";
                case "bramblekin": return "bramblekin";
                case "cairnback": case "thornweaver": case "sporewarden": case "maw": return token;
                // note: Bind the accepted generic frontier families to the closest approved imported creature so they do not degrade to capsule stand-ins.
                case "sewer": case "mutant": case "mutants": case "burrower": case "burrowers": return "maw";
                case "beast": case "beasts": return "cairnback";
                // note: An explicit DOT qualifier selects the new mimic shell without changing established generic mimic bindings.
                case "mimic": if (Array.IndexOf(tokens, "dot") >= 0) return "mimic"; break;
                case "rabbit": case "rabbits": return "rabbit";
                case "cat": case "cats": return "cat";
                case "horse": case "horses": return "horse";
                case "cow": case "cows": return "cow";
                case "deer": case "stag": case "stags": return "deer";
            }
        }
        return null;
    }

    public static string BuildBindingPrompt()
    {
        var catalog = Current;
        if (catalog == null) return string.Empty;
        var species = new SortedSet<string>(StringComparer.Ordinal);
        var races = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in catalog.entries)
        {
            if (entry != null && entry.kind == "monster") species.Add(entry.species == "mimic" ? "DOT mimic" : entry.species);
            if (entry != null && entry.kind == "race") races.Add(entry.species);
        }
        // note: Expose only installed semantic capabilities. The model may generate identities/lore but cannot emit Unity paths or invent executable asset bindings.
        return "ADDITIONAL_CREATURE_VISUALS: Optional hostile families " + string.Join(", ", species) +
            ". Generated NPC appearance may explicitly request " + string.Join(", ", races) + ". These are visual bindings; generate original identities, lore and supported mechanics. Wildlife assets are passive and must not be requested as hostile sites.";
    }

    public bool TryValidateModules(string assemblyPath, IReadOnlyList<string> moduleIds, out string reason)
    {
        // note: Exact body/rest interfaces and unique slots are mandatory; matching bone names alone cannot authorize cross-body attachment.
        if (!TryGetPrefab(assemblyPath, out var assembly) || !string.IsNullOrEmpty(assembly.moduleSlot) || string.IsNullOrEmpty(assembly.compatibilityId))
        { reason = "Assembly has no declared modular interface."; return false; }
        if (moduleIds == null || moduleIds.Count == 0) { reason = "No modules supplied."; return false; }
        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in moduleIds)
        {
            YQDotCreatureEntry module = entries.Find(e => e != null && e.assetId == id);
            if (module == null || string.IsNullOrEmpty(module.moduleSlot) || module.compatibilityId != assembly.compatibilityId || !slots.Add(module.moduleSlot))
            { reason = "Unknown module, incompatible body/rest interface or duplicate slot: " + id; return false; }
        }
        reason = string.Empty; return true;
    }

    public bool TryAttachModules(GameObject actor, string assemblyPath, IReadOnlyList<string> moduleIds, out GameObject attachment, out string reason)
    {
        attachment = null;
        if (actor == null || !TryValidateModules(assemblyPath, moduleIds, out reason)) { reason = "Actor or compatible module recipe is missing."; return false; }
        if (actor.GetComponentInChildren<YQDotModuleAttachment>(true) != null) { reason = "Remove the current modular attachment before replacing its recipe."; return false; }
        GameObject basePrefab = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(assemblyPath);
        if (basePrefab == null) { reason = "Assembly reference is missing."; return false; }
        var modules = new List<GameObject>();
        foreach (string id in moduleIds)
        {
            var entry = entries.Find(e => e.assetId == id);
            // note: Batched orc bodies already contain these anatomical/equipment slots. An additive attachment would duplicate skin or accessories, so require verified replacement data first.
            if (entry.requiresSlotReplacement) { reason = "This module requires authored slot replacement on the batched body: " + id; return false; }
            var prefab = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath);
            if (prefab == null) { reason = "Module reference is missing: " + id; return false; }
            modules.Add(prefab);
        }
        // note: Validate rest matrices on immutable source prefabs, then remap every module skin to the matching live LOD skeleton. Animated poses are never treated as rest data.
        var created = new List<GameObject>();
        try
        {
            LODGroup group = actor.GetComponent<LODGroup>();
            LOD[] lods = group != null ? group.GetLODs() : Array.Empty<LOD>();
            if (lods.Length == 0) throw new InvalidOperationException("Assembly LOD skeletons are missing.");
            for (int level = 0; level < lods.Length; level++)
            {
                Transform live = actor.transform.Find("LOD" + level);
                Transform original = basePrefab.transform.Find("LOD" + level);
                if (live == null || original == null) throw new InvalidOperationException("Matching LOD body is missing.");
                var restBones = UniqueTransforms(original);
                var liveBones = UniqueTransforms(live);
                var renderers = new List<Renderer>(lods[level].renderers);
                foreach (GameObject module in modules)
                {
                    foreach (var skin in module.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        foreach (Transform bone in skin.bones)
                        {
                            if (bone == null || !restBones.TryGetValue(bone.name, out var target) || !liveBones.ContainsKey(bone.name)) throw new InvalidOperationException("Module bone is missing or ambiguous.");
                            Matrix4x4 sourceRest = module.transform.worldToLocalMatrix * bone.localToWorldMatrix;
                            Matrix4x4 targetRest = original.worldToLocalMatrix * target.localToWorldMatrix;
                            for (int index = 0; index < 16; index++)
                                if (Mathf.Abs(sourceRest[index] - targetRest[index]) > .001f) throw new InvalidOperationException("Module rest frame does not match this LOD.");
                        }
                    var part = Instantiate(module, live, false); created.Add(part);
                    foreach (Animator animator in part.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var skin in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Transform[] bones = skin.bones;
                        for (int index = 0; index < bones.Length; index++) bones[index] = liveBones[bones[index].name];
                        skin.bones = bones;
                        if (skin.rootBone != null) skin.rootBone = liveBones[skin.rootBone.name];
                    }
                    renderers.AddRange(part.GetComponentsInChildren<Renderer>(true));
                }
                lods[level].renderers = renderers.ToArray();
            }
            // note: Publish LOD membership only after every source/rest validation and binding succeeds. The returned owner cleans up just these presentation parts.
            attachment = new GameObject("DOT modular attachment"); attachment.transform.SetParent(actor.transform, false);
            var owner = attachment.AddComponent<YQDotModuleAttachment>(); owner.Initialize(created.ToArray(), group, group.GetLODs());
            group.SetLODs(lods); group.RecalculateBounds(); reason = string.Empty; return true;
        }
        catch (Exception exception)
        {
            foreach (GameObject part in created) { part.SetActive(false); if (Application.isPlaying) Destroy(part); else DestroyImmediate(part); }
            reason = exception.Message; return false;
        }
    }

    private static Dictionary<string, Transform> UniqueTransforms(Transform root)
    {
        var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        { if (result.ContainsKey(transform.name)) ambiguous.Add(transform.name); else result.Add(transform.name, transform); }
        foreach (string name in ambiguous) result.Remove(name);
        return result;
    }
}
