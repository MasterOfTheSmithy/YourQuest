using System;
using System.Reflection;
using UnityEngine;

// note: Disposable synthetic cells verify storage ownership and collider lifecycle without awarding items or writing player saves.
public static class YQCellLootBindingTestsV2
{
    public static string TestBinding()
    {
        GameObject root = Fixture(out Transform chest, out YQCellLootBindingV2 binding);
        try
        {
            BoxCollider box = chest.GetComponent<BoxCollider>();
            Vector3 size = box.size, center = box.center;
            if (!Bind(root, binding, "site-a", out string failure)) return failure;
            var loot = chest.GetComponent<YQLockpickableLoot>();
            if (loot == null || loot.persistentLootId != YQCellLootBindingsV2.BuildLootId("site-a", "cell", "storage"))
                return "Reviewed storage did not use its instance-scoped saved identity.";
            // note: Invoke the real activation callback to catch the historical generic chest collider expansion.
            typeof(YQLockpickableLoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(loot, null);
            if (box.size != size || box.center != center || chest.GetComponents<Collider>().Length != 1)
                return "Reviewed storage activation changed its authored collision.";
            var opened = typeof(YQLockpickableLoot).GetField("_opened", BindingFlags.Instance | BindingFlags.NonPublic);
            opened.SetValue(loot, true);
            if (!Bind(root, binding, "site-a", out failure) || !(bool)opened.GetValue(loot) ||
                chest.GetComponents<YQLockpickableLoot>().Length != 1)
                return "Repeated binding reset consumed storage or duplicated its owner.";
            if (Bind(root, binding, "site-b", out _)) return "Different site identity overwrote an existing storage owner.";
            return string.Empty;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string TestRejections()
    {
        GameObject root = Fixture(out Transform chest, out YQCellLootBindingV2 binding);
        try
        {
            binding.reviewState = YQSemanticSiteReviewState.Pending;
            if (Bind(root, binding, "site-a", out _)) return "Draft storage was installed.";
            binding.reviewState = YQSemanticSiteReviewState.Approved;
            binding.sourceSignature = "stale";
            if (Bind(root, binding, "site-a", out _)) return "Stale storage geometry was installed.";
            binding.sourceSignature = "fixture";
            var box = chest.GetComponent<BoxCollider>();
            Vector3 size = box.size;
            box.size *= 2;
            if (Bind(root, binding, "site-a", out _)) return "Changed chest collision passed review.";
            box.size = size;
            var extra = chest.gameObject.AddComponent<SphereCollider>();
            if (Bind(root, binding, "site-a", out _)) return "Unreviewed additional chest collision passed.";
            UnityEngine.Object.DestroyImmediate(extra);
            binding.accessSocket.clearanceSize = Vector3.zero;
            if (Bind(root, binding, "site-a", out _)) return "Unmeasured interaction clearance passed.";
            binding.accessSocket.clearanceSize = new Vector3(0.6f, 1.8f, 0.6f);
            root.transform.Find("Access").localPosition += Vector3.right;
            if (Bind(root, binding, "site-a", out _)) return "Moved storage access passed.";
            root.transform.Find("Access").localPosition = binding.accessSocket.localPosition;
            root.SetActive(true);
            if (Bind(root, binding, "site-a", out _)) return "Storage installed after activation.";
            root.SetActive(false);
            if (chest.GetComponent<YQLockpickableLoot>() != null) return "Rejected storage left a partial provider.";
            string first = YQCellLootBindingsV2.BuildLootId("a:b", "c", "slot");
            if (first == YQCellLootBindingsV2.BuildLootId("a", "b:c", "slot") ||
                first == YQCellLootBindingsV2.BuildLootId("a:b", "c", "other"))
                return "Storage ID components collided.";
            return string.Empty;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string TestFunctionEvidence()
    {
        GameObject root = Fixture(out _, out YQCellLootBindingV2 loot);
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        try
        {
            var contract = new YQReviewedCellFunctionContractV2
            {
                cellId = "cell", sourceSignature = "fixture", reviewState = YQSemanticSiteReviewState.Approved,
                curation = new YQAssetCurationContractV2 { contractVersion = 2, primaryFunction = YQAssetFunctionV2.Storage }
            };
            contract.curation.affordances.Add(YQAssetAffordanceV2.Storage);
            contract.curation.sockets.Add(JsonUtility.FromJson<YQAssetSocketRecordV2>(JsonUtility.ToJson(loot.accessSocket)));
            var zone = new YQReviewedSemanticZoneRecord { stableId = "cell", prefab = root };
            zone.cellContractsV2.Add(contract);
            manifest.ConfigureCandidate("fixture", "fixture", "fixture", YQSemanticExtractionTopology.Unknown, null, 1, new[] { zone });
            manifest.MarkReleaseEligible();
            Func<bool> accepts = () => YQSiteFunctionContractsV2.TryValidate(manifest, new[] { "cell" },
                new[] { YQAssetFunctionV2.Storage }, YQWorldStructureUsagePolicy.ExteriorShellsOnly, out _);
            if (accepts()) return "Decorative storage sockets passed without a provider.";
            contract.lootBindings.Add(loot);
            if (!accepts()) return "Matching reviewed storage provider was rejected.";
            loot.reviewState = YQSemanticSiteReviewState.Pending;
            if (accepts()) return "Unapproved provider supplied an accepted storage function.";
            loot.reviewState = YQSemanticSiteReviewState.Approved;
            loot.accessSocket.localPosition += Vector3.forward;
            if (accepts()) return "Provider used a different access point from the functional contract.";
            return string.Empty;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(manifest); }
    }

    private static bool Bind(GameObject root, YQCellLootBindingV2 binding, string site, out string failure) =>
        YQCellLootBindingsV2.TryBind(root.transform, binding, "fixture", site, "cell", "Generated Location", "region", 1, out failure);

    private static GameObject Fixture(out Transform chest, out YQCellLootBindingV2 binding)
    {
        GameObject root = new GameObject("LootBindingFixture");
        root.SetActive(false);
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Chest";
        item.transform.SetParent(root.transform, false);
        chest = item.transform;
        BoxCollider collider = item.GetComponent<BoxCollider>();
        collider.center = new Vector3(0, 0.25f, 0);
        collider.size = new Vector3(0.43f, 0.51f, 0.72f);
        Transform access = new GameObject("Access").transform;
        access.SetParent(root.transform, false);
        access.localPosition = Vector3.right;
        binding = new YQCellLootBindingV2
        {
            bindingId = "storage", targetPath = "Chest", sourceSignature = "fixture", sourcePrefabGuid = "fixture-guid",
            reviewState = YQSemanticSiteReviewState.Approved, colliderCenter = collider.center, colliderSize = collider.size,
            accessSocket = new YQAssetSocketRecordV2
            {
                socketId = "access", transformPath = "Access", kind = YQAssetSocketKindV2.Interaction,
                localPosition = access.localPosition, clearanceSize = new Vector3(0.6f, 1.8f, 0.6f)
            }
        };
        return root;
    }
}
