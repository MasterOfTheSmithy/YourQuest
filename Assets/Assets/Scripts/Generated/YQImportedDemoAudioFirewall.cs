using System;
using UnityEngine;

public static class YQImportedDemoAudioFirewall
{
    private const float TemporaryOneShotSweepIntervalSeconds =
        2.50f;

    private static float _nextTemporaryOneShotSweepTime;

    public static void SanitizeGeneratedPrefabAudio(
        GameObject root,
        string owner)
    {
        if (root == null)
            return;

        // note: Generated-world props must be visual/physical assets only; imported demo audio is not gameplay authority.
        StopAndRemoveHierarchyAudioSources(
            root);

        // note: These third-party demo scripts can emit animation/startup one-shots before YourQuest systems take over.
        RemoveImportedDemoAudioBehaviours(
            root,
            removeAnimationEventReceiver: true);
    }

    public static void RemoveImportedDemoAudioBehaviours(
        GameObject root,
        bool removeAnimationEventReceiver)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            MonoBehaviour behaviour =
                behaviours[i];

            if (behaviour == null)
                continue;

            Type type =
                behaviour.GetType();

            string typeName =
                type != null
                    ? type.Name
                    : string.Empty;

            bool remove =
                IsImportedDemoAudioBehaviourName(
                    typeName) ||
                (removeAnimationEventReceiver &&
                 behaviour is YQAnimationEventAudioReceiver);

            if (!remove)
                continue;

            // note: Disable first so the component cannot keep reacting while Unity defers object destruction.
            behaviour.enabled =
                false;

            UnityEngine.Object.Destroy(
                behaviour);
        }
    }

    public static void SweepTemporaryLocomotionOneShots()
    {
        if (Time.unscaledTime <
            _nextTemporaryOneShotSweepTime)
        {
            return;
        }

        _nextTemporaryOneShotSweepTime =
            Time.unscaledTime +
            TemporaryOneShotSweepIntervalSeconds;

        AudioSource[] sources =
            UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        // note: Imported audio behaviours are removed at spawn; this infrequent global sweep is only a safety net for loose third-party PlayClipAtPoint shells.

        for (int i = 0;
             i < sources.Length;
             i++)
        {
            AudioSource source =
                sources[i];

            if (source == null)
                continue;

            if (!IsUnauthorizedTemporaryAudioSource(
                    source))
            {
                continue;
            }

            // note: PlayClipAtPoint creates loose "One shot audio" objects outside the player tree, so stop them explicitly.
            source.Stop();

            if (IsTemporaryOneShotShell(
                    source))
            {
                UnityEngine.Object.Destroy(
                    source.gameObject);
            }
        }
    }

    public static bool IsImportedDemoAmbientOrVoxAudioSource(
        AudioSource source)
    {
        if (source == null)
            return false;

        string sourceName =
            NormalizeAudioName(
                source.name);

        string objectName =
            source.gameObject != null
                ? NormalizeAudioName(
                    source.gameObject.name)
                : string.Empty;

        string clipName =
            source.clip != null
                ? NormalizeAudioName(
                    source.clip.name)
                : string.Empty;

        // note: Catch imported creature prefab idle/vox loops that can auto-fire as global breathing/growling demo audio.
        return
            IsImportedDemoAmbientOrVoxName(
                sourceName) ||
            IsImportedDemoAmbientOrVoxName(
                objectName) ||
            IsImportedDemoAmbientOrVoxName(
                clipName);
    }

    private static void StopAndRemoveHierarchyAudioSources(
        GameObject root)
    {
        AudioSource[] sources =
            root.GetComponentsInChildren<AudioSource>(
                true);

        for (int i = 0;
             i < sources.Length;
             i++)
        {
            AudioSource source =
                sources[i];

            if (source == null)
                continue;

            // note: Stop, disarm, then remove so Awake/Start audio cannot leak after instantiation.
            source.Stop();
            source.playOnAwake =
                false;
            source.loop =
                false;
            source.enabled =
                false;

            UnityEngine.Object.Destroy(
                source);
        }
    }

    private static bool IsImportedDemoAudioBehaviourName(
        string typeName)
    {
        if (string.IsNullOrWhiteSpace(
                typeName))
        {
            return false;
        }

        return
            string.Equals(
                typeName,
                "SFB_AudioManager",
                StringComparison.Ordinal) ||
            string.Equals(
                typeName,
                "PlayAudioClipOnAwake",
                StringComparison.Ordinal) ||
            string.Equals(
                typeName,
                "LPDemoHumanoid",
                StringComparison.Ordinal);
    }

    private static bool IsLocomotionAudioSource(
        AudioSource source)
    {
        if (source == null)
            return false;

        string sourceName =
            NormalizeAudioName(
                source.name);

        string objectName =
            source.gameObject != null
                ? NormalizeAudioName(
                    source.gameObject.name)
                : string.Empty;

        string clipName =
            source.clip != null
                ? NormalizeAudioName(
                    source.clip.name)
                : string.Empty;

        return
            IsLocomotionAudioName(
                sourceName) ||
            IsLocomotionAudioName(
                objectName) ||
            IsLocomotionAudioName(
                clipName);
    }

    private static bool IsTemporaryOneShotShell(
        AudioSource source)
    {
        if (source == null ||
            source.gameObject == null)
        {
            return false;
        }

        string objectName =
            NormalizeAudioName(
                source.gameObject.name);

        if (objectName.Contains(
                "one_shot_audio"))
        {
            return true;
        }

        Component[] components =
            source.gameObject.GetComponents<Component>();

        return
            source.transform.parent == null &&
            components.Length <=
                2;
    }

    private static bool IsUnauthorizedTemporaryAudioSource(
        AudioSource source)
    {
        if (source == null)
            return false;

        // note: Loose temporary shells are never authored YourQuest combat audio, so demo locomotion/vox may be killed aggressively.
        return
            IsLocomotionAudioSource(
                source) ||
            (IsTemporaryOneShotShell(
                 source) &&
             IsImportedDemoAmbientOrVoxAudioSource(
                 source));
    }

    private static bool IsLocomotionAudioName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        return
            value.Contains(
                "footstep") ||
            value.Contains(
                "foot_step") ||
            value.Contains(
                "footsteps") ||
            value.Contains(
                "foot_steps") ||
            value.Contains(
                "wetfoot") ||
            value.Contains(
                "wet_foot") ||
            value.Contains(
                "walking") ||
            value.Contains(
                "walk_loop") ||
            value.Contains(
                "walkloop") ||
            value.Contains(
                "running") ||
            value.Contains(
                "run_loop") ||
            value.Contains(
                "runloop");
    }

    private static bool IsImportedDemoAmbientOrVoxName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        // note: These names match bundled creature idle/vox demo clips, not authored YourQuest ability/combat SFX.
        return
            value.Contains(
                "mimic_vox") ||
            value.Contains(
                "idle_grunt") ||
            value.Contains(
                "idle_grunts") ||
            value.Contains(
                "idle_break") ||
            value.Contains(
                "idlebreak") ||
            value.Contains(
                "idle_loop") ||
            value.Contains(
                "idleloop") ||
            value.Contains(
                "idle_exhale") ||
            value.Contains(
                "movement_idle") ||
            value.Contains(
                "vox_roar") ||
            value.Contains(
                "roar_") ||
            value.Contains(
                "_roar") ||
            value.Contains(
                "grunt_") ||
            value.Contains(
                "_grunt") ||
            value.Contains(
                "vox_hissing") ||
            value.Contains(
                "vox_breathing") ||
            value.Contains(
                "vox_grunting") ||
            value.Contains(
                "breathing") ||
            value.Contains(
                "grunting") ||
            value.Contains(
                "tongue_flap") ||
            value.Contains(
                "hissing");
    }

    private static string NormalizeAudioName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return
            value
                .Trim()
                .Replace(
                    '-',
                    '_')
                .Replace(
                    ' ',
                    '_')
                .ToLowerInvariant();
    }
}
