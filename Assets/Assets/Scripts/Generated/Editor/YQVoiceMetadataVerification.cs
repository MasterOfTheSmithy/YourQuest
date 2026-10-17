#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: An explicit editor diagnostic exercises native metadata hierarchies in temporary unsaved objects, then destroys only those objects.
internal static class YQVoiceMetadataVerification
{
    [MenuItem("YourQuest/Playable Evaluation/Verify Voice Metadata Hierarchies")]
    private static void VerifyVoiceMetadataHierarchies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Hierarchy checks require idle Edit Mode.");
        var roots = new List<GameObject>();
        var groups = new List<string>();
        try
        {
            var root = TemporaryRoot(roots);
            var child = new GameObject("InactiveVoiceChild") { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            var authoredChild = child.AddComponent<YQNpcVoice>();
            authoredChild.voice = YQNpcVoice.Preset.Anna;
            authoredChild.hearingRadius = 11.25f;
            authoredChild.enabled = false;
            Require(YQNpcVoice.Resolve(root) == authoredChild, "Inactive authored child was not resolved.");
            groups.Add("NATIVE-INACTIVE-CHILD");
            var authoredRoot = root.AddComponent<YQNpcVoice>();
            authoredRoot.voice = YQNpcVoice.Preset.Marius;
            authoredRoot.hearingRadius = 2.5f;
            Require(YQNpcVoice.Resolve(root) == authoredRoot, "Root metadata did not take precedence.");
            groups.Add("NATIVE-ROOT-PRECEDENCE");
            var sanitation = typeof(YQGeneratedWorldPopulation).GetMethod("PrepareResidentPhysics", BindingFlags.Static | BindingFlags.NonPublic);
            if (sanitation == null) throw new InvalidOperationException("Production sanitation entry point was not found.");
            var arguments = sanitation.GetParameters();
            if (arguments.Length != 1 || arguments[0].ParameterType != typeof(GameObject))
                throw new InvalidOperationException("Sanitation signature changed; diagnostic must be reviewed.");
            // note: The exact existing sanitation owner is called on test-owned objects only, to verify authored preset/radius/enabled preservation.
            sanitation.Invoke(null, new object[] { root });
            Require(authoredRoot.enabled && authoredRoot.voice == YQNpcVoice.Preset.Marius && authoredRoot.hearingRadius == 2.5f,
                "Root authored values changed during sanitation.");
            Require(!authoredChild.enabled && authoredChild.voice == YQNpcVoice.Preset.Anna && authoredChild.hearingRadius == 11.25f,
                "Child authored values changed during sanitation.");
            Require(root.GetComponentsInChildren<YQNpcVoice>(true).Length == 2, "Sanitation changed voice component count.");
            groups.Add("NATIVE-AUTHORED-SANITATION");
            var empty = TemporaryRoot(roots);
            Require(YQNpcVoice.Resolve(empty) == null, "Missing metadata did not remain missing.");
            Require(YQNpcVoice.Resolve(null) == null, "Null root was not safe.");
            groups.Add("NATIVE-MISSING-NULL");
            WriteReceipt(new { utc = DateTime.UtcNow.ToString("O"), passed = groups.Count, groups,
                evidence = "NATIVE_UNITY_TEMPORARY_METADATA_HIERARCHIES; no production body, reload, routing or audible-fit acceptance" });
        }
        catch (Exception error)
        {
            WriteReceipt(new { utc = DateTime.UtcNow.ToString("O"), failed = true, error = error.ToString(), groups });
            throw;
        }
        finally
        {
            foreach (var root in roots) if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject TemporaryRoot(List<GameObject> roots)
    {
        var root = new GameObject("YQVoiceMetadataDisposable") { hideFlags = HideFlags.HideAndDontSave };
        roots.Add(root);
        return root;
    }

    private static void Require(bool value, string failure)
    {
        if (!value) throw new InvalidOperationException(failure);
    }

    private static void WriteReceipt(object value)
    {
        string directory = Path.GetFullPath("outputs/PlayableEvaluation_20261009/voice-native");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "metadata-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".json");
        using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(value, Formatting.Indented));
        Debug.Log("[YQPlayableEvaluation] Native voice metadata receipt: " + destination);
    }
}
#endif
