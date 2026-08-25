using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQAnimationEventAudioReceiver : MonoBehaviour
{
    [Range(0f, 1f)]
    public float volume = 0.08f;

    /*
     * Minimum interval for this exact receiver.
     */
    public float minimumInterval = 0.20f;

    /*
     * Multiple Animator children belonging to one character can contain
     * identical imported animation events.
     *
     * All receivers belonging to one actor therefore share this cooldown.
     */
    private const float OwnerMinimumInterval =
        0.20f;

    /*
     * Different NPCs may all fire a footstep event on the same frame.
     *
     * This prevents an entire settlement/crowd from dumping simultaneous
     * locomotion one-shots into the mixer.
     */
    private const float NonPlayerGlobalMinimumInterval =
        0.12f;

    private static readonly Dictionary<int, float>
        NextAllowedByOwner =
            new Dictionary<int, float>();

    private static float
        _nextAllowedNonPlayerTime;

    private static AudioClip
        _playerFallbackFootstepClip;

    private AudioSource _source;

    private float
        _nextAllowedReceiverTime;

    public void PlayAudio()
    {
        PlayClip(
            null);
    }

    public void PlayAudio(
        string _)
    {
        PlayClip(
            null);
    }

    public void PlayAudio(
        UnityEngine.Object audioObject)
    {
        PlayClip(
            audioObject as AudioClip);
    }

    public void PlayAudio(
        AnimationEvent animationEvent)
    {
        AudioClip clip =
            animationEvent != null
                ? animationEvent
                    .objectReferenceParameter
                    as AudioClip
                : null;

        PlayClip(
            clip);
    }

    public void Footstep()
    {
        PlayClip(
            null);
    }

    public void PlayFootstep()
    {
        PlayClip(
            null);
    }

    private void PlayClip(
        AudioClip clip)
    {
        bool isPlayer =
            TryResolvePlayerOwner(
                out int ownerId);

        if (clip == null)
        {
            if (!isPlayer ||
                !CanCurrentPlayerPlayLocomotionClip())
            {
                return;
            }

            // note: Player-only fallback restores authored footstep events that do not carry an AudioClip, without reviving imported NPC wet-step spam.
            clip =
                GetPlayerFallbackFootstepClip();
        }

        bool locomotionClip =
            IsLocomotionClip(
                clip);

        /*
         * HARD INITIAL-GENERATION AUDIO FIREWALL
         *
         * No actor in the world is allowed to emit locomotion audio while
         * initial world generation still owns gameplay.
         *
         * This executes before:
         *
         * - AudioSource creation
         * - receiver cooldowns
         * - actor cooldowns
         * - global NPC cooldowns
         * - PlayOneShot()
         *
         * Therefore imported idle/walk animation events cannot create
         * WetFootsteps behind the loading screen.
         */
        if (locomotionClip &&
            YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked)
        {
            return;
        }

        /*
         * Player footsteps have an authoritative physical validity rule.
         *
         * CanPlayFootstepAudio already requires:
         *
         * - initial generation unlocked
         * - grounded player
         * - sufficient actual planar velocity
         *
         * Therefore an imported walk animation cannot produce footsteps
         * merely because its AnimationEvent fired.
         */
        if (locomotionClip &&
    isPlayer)
        {
            /*
             * Imported walking animations are allowed to fire their
             * AnimationEvents even when the physical player is stationary.
             *
             * Require actual world-space player movement before accepting
             * a footstep.
             */
            if (!CanCurrentPlayerPlayLocomotionClip())
            {
                return;
            }
        }

        if (!isPlayer)
        {
            ownerId =
                ResolveNonPlayerOwnerId();
        }

        float now =
            Time.time;

        /*
         * First layer:
         * this individual receiver cannot repeatedly fire.
         */
        if (now <
            _nextAllowedReceiverTime)
        {
            return;
        }

        /*
         * Second layer:
         * all animation-event receivers belonging to one actor share
         * the same cooldown.
         *
         * This collapses duplicate events from body, wardrobe, hair,
         * equipment and other Animator children.
         */
        if (NextAllowedByOwner.TryGetValue(
                ownerId,
                out float ownerNextAllowed) &&
            now <
            ownerNextAllowed)
        {
            return;
        }

        /*
         * Third layer:
         * locomotion events from different NPCs cannot all fire together.
         *
         * IMPORTANT:
         * This global NPC throttle applies only to locomotion clips.
         *
         * Attack, hurt, death and other legitimate one-shot audio should
         * not be discarded just because another NPC made a sound.
         */
        if (locomotionClip &&
            !isPlayer &&
            now <
            _nextAllowedNonPlayerTime)
        {
            return;
        }

        EnsureAudioSource(
            locomotionClip);

        if (_source == null)
            return;

        float receiverInterval =
            Mathf.Max(
                0.08f,
                minimumInterval);

        _nextAllowedReceiverTime =
            now +
            receiverInterval;

        NextAllowedByOwner[ownerId] =
            now +
            Mathf.Max(
                OwnerMinimumInterval,
                receiverInterval);

        if (locomotionClip &&
            !isPlayer)
        {
            _nextAllowedNonPlayerTime =
                now +
                NonPlayerGlobalMinimumInterval;
        }

        _source.pitch =
            Random.Range(
                0.96f,
                1.04f);

        float playbackVolume =
            isPlayer
                ? Mathf.Clamp01(
                    volume)
                : Mathf.Clamp01(
                    volume * 0.65f);

        _source.PlayOneShot(
            clip,
            playbackVolume);
    }

    private void EnsureAudioSource(
        bool locomotionClip)
    {
        if (_source == null)
        {
            _source =
                GetComponent<AudioSource>();

            if (_source == null)
            {
                _source =
                    gameObject.AddComponent<
                        AudioSource>();
            }
        }

        /*
         * Always enforce runtime spatial settings.
         *
         * Do not rely on whatever settings an imported prefab happened
         * to ship with.
         */
        _source.playOnAwake =
            false;

        _source.loop =
            false;

        _source.spatialBlend =
            1f;

        _source.dopplerLevel =
            0f;

        _source.minDistance =
            1f;

        _source.maxDistance =
            locomotionClip
                ? 10f
                : 18f;

        _source.rolloffMode =
            AudioRolloffMode.Linear;
    }

    private static AudioClip GetPlayerFallbackFootstepClip()
    {
        if (_playerFallbackFootstepClip != null)
            return _playerFallbackFootstepClip;

        const int sampleRate =
            22050;

        const float duration =
            0.075f;

        int sampleCount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    sampleRate *
                    duration));

        float[] samples =
            new float[sampleCount];

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                i /
                (float)sampleRate;

            float envelope =
                Mathf.Exp(
                    -t *
                    46f);

            // note: A tiny generated thump is enough feedback until curated player footstep clips are assigned.
            samples[i] =
                Mathf.Sin(
                    2f *
                    Mathf.PI *
                    92f *
                    t) *
                envelope *
                0.22f;
        }

        _playerFallbackFootstepClip =
            AudioClip.Create(
                "YQ_Player_Fallback_Footstep",
                sampleCount,
                1,
                sampleRate,
                false);

        _playerFallbackFootstepClip.SetData(
            samples,
            0);

        return _playerFallbackFootstepClip;
    }

    private bool TryResolvePlayerOwner(
        out int ownerId)
    {
        PlayerController controller =
            GetComponentInParent<
                PlayerController>();

        if (controller != null)
        {
            ownerId =
                controller.gameObject
                    .GetInstanceID();

            return true;
        }

        YQInvestorPlayerMotor player =
            GetComponentInParent<
                YQInvestorPlayerMotor>();

        if (player != null)
        {
            ownerId =
                player.gameObject
                    .GetInstanceID();

            return true;
        }

        ownerId =
            0;

        return false;
    }

    private bool CanCurrentPlayerPlayLocomotionClip()
    {
        // note: Footstep events are invalid while any modal/loading lock owns gameplay input.
        if (RuntimeModalUiBlocker.IsBlocked)
            return false;

        PlayerController controller =
            GetComponentInParent<
                PlayerController>();

        if (controller != null)
        {
            // note: The current authoritative player owns the strict grounded/moving footstep rule.
            return
                controller.CanPlayFootstepAudio;
        }

        YQInvestorPlayerMotor player =
            GetComponentInParent<
                YQInvestorPlayerMotor>();

        if (player == null)
        {
            return false;
        }

        Rigidbody playerBody =
            player.GetComponent<Rigidbody>();

        if (playerBody == null)
            return false;

        Vector3 planarVelocity =
            playerBody.linearVelocity;

        planarVelocity.y =
            0f;

        return
            planarVelocity.sqrMagnitude >=
            0.01f;
    }

    private int ResolveNonPlayerOwnerId()
    {
        YQInvestorEnemy enemy =
            GetComponentInParent<
                YQInvestorEnemy>();

        if (enemy != null)
        {
            return
                enemy.gameObject
                    .GetInstanceID();
        }

        Transform root =
            transform.root;

        if (root != null)
        {
            return
                root.gameObject
                    .GetInstanceID();
        }

        return
            gameObject.GetInstanceID();
    }

    private static bool IsLocomotionClip(
        AudioClip clip)
    {
        if (clip == null)
            return false;

        string name =
            clip.name ??
            string.Empty;

        name =
            name
                .Trim()
                .Replace(
                    '-',
                    '_')
                .Replace(
                    ' ',
                    '_')
                .ToLowerInvariant();

        /*
         * Explicit WetFootsteps matching matters because that is the
         * imported clip currently flooding the Audio Profiler.
         */
        return
            name.Contains(
                "footstep") ||
            name.Contains(
                "foot_step") ||
            name.Contains(
                "footsteps") ||
            name.Contains(
                "foot_steps") ||
            name.Contains(
                "wetfoot") ||
            name.Contains(
                "wet_foot") ||
            name.Contains(
                "walking") ||
            name.Contains(
                "walk_loop") ||
            name.Contains(
                "walkloop") ||
            name.Contains(
                "running") ||
            name.Contains(
                "run_loop") ||
            name.Contains(
                "runloop");
    }
}
