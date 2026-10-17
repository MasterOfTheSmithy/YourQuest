using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQStagedColliderBoundsVerification
{
    private const string CellPath = "Assets/Assets/GeneratedAssets/WorldAssemblies/StreamingSites/witch_house/Cells/yq_cell_witch_house_p1_p1_p00.prefab";
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("YourQuest/Verification/Verify Hidden Witch House Colliders")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { Debug.LogError("[YQStagedColliderBoundsVerification] Requires idle Edit Mode."); return; }
        Scene preview = default;
        try
        {
            // note: Exercise the production bounds helper and sanitizer only on disposable preview objects. No source prefab, open scene, world document or profile is edited.
            MethodInfo boundsMethod = typeof(YQCompiledWorldSiteInstance).GetMethod("TryGetSanitationColliderBounds", PrivateStatic);
            MethodInfo sanitationMethod = typeof(YQCompiledWorldSiteInstance).GetMethod("SanitizeStreamedCellRoutine", PrivateStatic);
            if (boundsMethod == null || sanitationMethod == null)
                throw new InvalidOperationException("Production sanitation methods are unavailable.");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);
            if (source == null) throw new InvalidOperationException("Missing source cell: " + CellPath);
            preview = EditorSceneManager.NewPreviewScene();
            int nativeProbes = VerifyNativeShapes(preview, boundsMethod);
            nativeProbes += VerifyReflectedMeshes(preview, boundsMethod, source);
            GameObject cell = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            cell.hideFlags = HideFlags.HideAndDontSave;
            cell.transform.SetPositionAndRotation(new Vector3(0f, 56.06f, 30f), Quaternion.identity);
            cell.transform.localScale = Vector3.one;
            cell.SetActive(false);
            int checkedColliders = 0, planarBoxes = 0;
            var failures = new List<string>();
            foreach (Collider collider in cell.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger || collider is TerrainCollider || collider is CharacterController) continue;
                checkedColliders++;
                if (collider is BoxCollider box && (box.size.x == 0f || box.size.y == 0f || box.size.z == 0f)) planarBoxes++;
                if (!ReadBounds(boundsMethod, collider, out _, out string failure))
                    failures.Add(AnimationUtility.CalculateTransformPath(collider.transform, cell.transform) + " [" + collider.GetType().Name + "]: " + failure);
            }
            if (failures.Count != 0)
                throw new InvalidOperationException("Cell bounds failures=" + failures.Count + "\n" + string.Join("\n", failures));

            int repaired = 0, probesDisabled = 0;
            Action<int, int> completed = (colliders, probes) => { repaired = colliders; probesDisabled = probes; };
            IEnumerator routine = (IEnumerator)sanitationMethod.Invoke(null, new object[] { cell, completed });
            // note: This routine yields only frame boundaries. Draining it in the preview verifies its complete classification pass, not gameplay performance or streaming readiness.
            int steps = 0;
            try
            {
                while (routine.MoveNext())
                {
                    if (routine.Current != null || ++steps > 10000)
                        throw new InvalidOperationException("Sanitation yielded an unsupported operation or exceeded its bound.");
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
            Debug.Log("[YQStagedColliderBoundsVerification] PASS (detached Edit Mode only)\n" +
                "Cell: " + CellPath + "\nChecked enabled non-trigger colliders: " + checkedColliders +
                "\nPlanar boxes: " + planarBoxes + "\nNative shape comparisons: " + nativeProbes +
                "\nSanitation repairs/disables: " + repaired + "/" + probesDisabled +
                "\nPlacement context: seed 33b9e87d, origin (0, 56.06, 30); no generation, profile or gameplay run.");
        }
        catch (Exception exception)
        {
            Exception cause = exception is TargetInvocationException wrapper && wrapper.InnerException != null ? wrapper.InnerException : exception;
            Debug.LogError("[YQStagedColliderBoundsVerification] FAIL (detached Edit Mode only)\n" + cause);
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static bool ReadBounds(MethodInfo method, Collider collider, out Bounds bounds, out string failure)
    {
        object[] arguments = { collider, default(Bounds), null };
        bool valid = (bool)method.Invoke(null, arguments);
        bounds = (Bounds)arguments[1];
        failure = (string)arguments[2];
        return valid;
    }

    private static int VerifyNativeShapes(Scene preview, MethodInfo method)
    {
        int count = 0;
        for (int shape = 0; shape < 7; shape++)
        {
            GameObject probe = new GameObject("StagedBoundsProbe_" + shape);
            probe.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(probe, preview);
            probe.transform.SetPositionAndRotation(new Vector3(3f, 7f, 11f), Quaternion.Euler(17f, 31f, 43f));
            probe.transform.localScale = new Vector3(.8f, 1.3f, 2f);
            Collider collider;
            if (shape <= 1)
            {
                BoxCollider box = probe.AddComponent<BoxCollider>();
                box.center = new Vector3(.2f, -.3f, .4f);
                box.size = shape == 0 ? new Vector3(1f, 2f, 3f) : new Vector3(1f, 1f, 0f);
                collider = box;
            }
            else if (shape <= 5)
            {
                CapsuleCollider capsule = probe.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(.2f, -.3f, .4f);
                capsule.direction = (shape - 2) % 3;
                capsule.radius = shape == 5 ? 1f : .4f;
                capsule.height = shape == 5 ? .4f : 3.5f;
                collider = capsule;
            }
            else
            {
                SphereCollider sphere = probe.AddComponent<SphereCollider>();
                sphere.center = new Vector3(.2f, -.3f, .4f);
                sphere.radius = .7f;
                collider = sphere;
            }
            Physics.SyncTransforms();
            Bounds native = collider.bounds;
            if (native.size.sqrMagnitude <= .000001f)
                throw new InvalidOperationException("Preview native bounds unavailable for probe " + shape);
            probe.SetActive(false);
            Physics.SyncTransforms();
            if (!ReadBounds(method, collider, out Bounds hidden, out string failure))
                throw new InvalidOperationException("Probe " + shape + ": " + failure);
            // note: Native comparison is independent of the authored calculation and tolerates only small physics padding and floating-point differences.
            const float tolerance = .02f;
            if ((native.center - hidden.center).sqrMagnitude > tolerance * tolerance ||
                Mathf.Abs(native.size.x - hidden.size.x) > tolerance ||
                Mathf.Abs(native.size.y - hidden.size.y) > tolerance ||
                Mathf.Abs(native.size.z - hidden.size.z) > tolerance)
                throw new InvalidOperationException("Probe " + shape + " native/hidden mismatch: " + native + " / " + hidden);
            count++;
        }
        return count;
    }

    private static int VerifyReflectedMeshes(Scene preview, MethodInfo method, GameObject source)
    {
        int count = 0;
        foreach (string sourceName in new[] { "Cylinder", "Cylinder_1" })
        {
            // note: Compare the actual reflected source meshes with their native collider bounds; a transformed mesh AABB may conservatively exceed the tight native bounds.
            Transform authored = source.transform.Find(sourceName);
            MeshCollider original = authored != null ? authored.GetComponent<MeshCollider>() : null;
            if (original == null || original.sharedMesh == null)
                throw new InvalidOperationException("Missing reflected mesh fixture: " + sourceName);
            GameObject probe = new GameObject("ReflectedBoundsProbe_" + sourceName);
            probe.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(probe, preview);
            probe.transform.SetPositionAndRotation(new Vector3(3f, 7f, 11f), authored.localRotation);
            probe.transform.localScale = authored.localScale;
            MeshCollider collider = probe.AddComponent<MeshCollider>();
            collider.convex = original.convex;
            collider.cookingOptions = original.cookingOptions;
            collider.sharedMesh = original.sharedMesh;
            Physics.SyncTransforms();
            Bounds native = collider.bounds;
            if (native.size.sqrMagnitude <= .000001f)
                throw new InvalidOperationException("Preview native mesh bounds unavailable: " + sourceName);
            probe.SetActive(false);
            Physics.SyncTransforms();
            if (!ReadBounds(method, collider, out Bounds hidden, out string failure))
                throw new InvalidOperationException(sourceName + ": " + failure);
            hidden.Expand(.04f);
            if (!hidden.Contains(native.min) || !hidden.Contains(native.max))
                throw new InvalidOperationException(sourceName + " hidden mesh bounds do not contain native bounds: " + native + " / " + hidden);
            count++;
        }
        return count;
    }
}
