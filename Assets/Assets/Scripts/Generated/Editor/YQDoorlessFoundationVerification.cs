#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class YQDoorlessFoundationVerification
{
    [MenuItem("YourQuest/World Generation/Verify Doorless Foundation Grounding")]
    public static void Run() { RunContracts(); }

    public static bool RunContracts()
    {
        // note: Isolated temporary terrain and transforms exercise the real runtime solver without loading a scene or persistent profile.
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 10f, 32f) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = .2f;
        data.SetHeights(0, 0, heights);
        var terrainObject = Terrain.CreateTerrainGameObject(data);
        var cell = new GameObject("DoorlessFoundationFixture");
        try
        {
            cell.transform.position = new Vector3(16f, 0f, 16f);
            var support = new GameObject("ActualSupport");
            support.transform.SetParent(cell.transform, false);
            support.AddComponent<BoxCollider>();
            var contact = new YQFoundationTerrainContact { supportPath = "ActualSupport", localBottom = Vector3.zero,
                minimumEmbedDepth = 0f, maximumEmbedDepth = .15f };
            var contract = new YQReviewedCellFunctionContractV2 { sourceSignature = "fixture-source", reviewState = YQSemanticSiteReviewState.Approved };
            contract.independentAssembly = new YQIndependentAssemblyContract { sourceSignature = "fixture-source",
                reviewState = YQSemanticSiteReviewState.Approved, completeStructuralDependencies = true, foundationVerified = true,
                terrainContactVersion = 1, terrainContacts = new List<YQFoundationTerrainContact> { contact } };
            var solve = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveReviewedLandingDelta", BindingFlags.NonPublic | BindingFlags.Static);
            if (solve == null) throw new InvalidOperationException("Runtime grounding solver missing.");
            bool Resolve(out float delta)
            {
                object[] args = { cell.transform, terrainObject.GetComponent<Terrain>(), contract, 0f };
                bool result = (bool)solve.Invoke(null, args);
                delta = (float)args[3]; return result;
            }
            void Check(bool valid, string name) { if (!valid) throw new InvalidOperationException(name); }
            Check(Resolve(out float correction) && Mathf.Abs(correction - 2f) < .001f, "Approved doorless assembly grounds to measured support.");
            Check(YQFoundationTerrainContacts.TryValidate(cell.transform, terrainObject.GetComponent<Terrain>(), contract, correction, out _), "Final shared validator accepts proposed translation.");
            contract.independentAssembly.sourceSignature = "stale";
            Check(!Resolve(out _), "Stale foundation source rejects.");
            contract.independentAssembly.sourceSignature = contract.sourceSignature;
            contact.supportPath = "MissingSupport";
            Check(!Resolve(out _), "Missing actual support rejects.");
            contact.supportPath = "ActualSupport";
            contact.localBottom = new Vector3(100f, 0f, 0f);
            Check(!Resolve(out _), "Unsupported terrain rejects.");
            contact.localBottom = Vector3.zero;
            contract.independentAssembly.terrainContacts.Add(new YQFoundationTerrainContact { supportPath = "ActualSupport",
                localBottom = Vector3.up, minimumEmbedDepth = 0f, maximumEmbedDepth = .15f });
            Check(!Resolve(out _), "Incompatible measured contacts reject without renderer fallback.");
            contract.independentAssembly.terrainContacts.RemoveAt(1);
            cell.transform.position += Vector3.down * 200f;
            Check(!Resolve(out _), "Correction beyond existing bound rejects.");
            Debug.Log("[YQDoorlessFoundationVerification] PASS: 7 isolated runtime solver checks; visible shrine publication unverified.");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Docs/Doorless_Foundation_Contract_Receipt.json"),
                "{\"status\":\"PASS\",\"checks\":7,\"evidence\":\"isolated Editor runtime solver fixtures\",\"visiblePublicationVerified\":false,\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\"}");
            return true;
        }
        finally
        {
            // note: Fixture objects are temporary; the active world and saved documents remain untouched.
            UnityEngine.Object.DestroyImmediate(cell);
            UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
#endif
