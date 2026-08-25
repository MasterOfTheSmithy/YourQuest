using UnityEngine;

[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class YQStillnessProgressionTracker : MonoBehaviour
{
    private const string StillSecondsCounter = "idle:still_seconds";
    private const float SampleIntervalSeconds = 5f;
    private const float StillDistanceThreshold = 0.12f;

    private static readonly float[] Thresholds = { 60f, 300f, 900f, 1800f, 3600f, 7200f };
    private static readonly string[] Titles =
    {
        "Quiet Foot",
        "AFK Witness",
        "Master of Waiting",
        "Like a Stone",
        "Unmovable",
        "God of Stillness"
    };

    private static readonly string[] Descriptions =
    {
        "The system noticed that the player can stop without becoming absent.",
        "A goofy but valid title for proving that doing nothing can still be a choice.",
        "A waiting title earned when patience becomes a repeated player stimulus.",
        "The player stayed so still that the world began treating stillness as a trait.",
        "A higher stillness title for refusing to be moved by ordinary pressure.",
        "A grandiose stillness title seeded by absurd patience and repeated refusal to move."
    };

    private Vector3 _lastPosition;
    private bool _hasPosition;
    private float _nextSampleTime;
    private float _nextPlayerResolveTime;
    private Transform _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQStillnessProgressionTracker>() != null)
            return;

        GameObject go = new GameObject("00__YQ_StillnessProgressionTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<YQStillnessProgressionTracker>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;
    }

    private void Update()
    {
        if (RuntimeModalUiBlocker.IsBlocked || Time.unscaledTime < _nextSampleTime)
            return;

        float elapsed = Mathf.Max(0.1f, Time.unscaledTime - (_nextSampleTime - SampleIntervalSeconds));
        _nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;

        Transform player = ResolvePlayer();
        if (player == null)
        {
            _hasPosition = false;
            return;
        }

        Vector3 position = player.position;
        if (!_hasPosition)
        {
            _lastPosition = position;
            _hasPosition = true;
            return;
        }

        float moved = Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(_lastPosition.x, 0f, _lastPosition.z));
        _lastPosition = position;
        if (moved > StillDistanceThreshold)
            return;

        PlayerStateManager manager = PlayerStateManager.Instance;
        PlayerState state = manager != null ? manager.state : null;
        if (state == null)
            return;

        state.EnsureCollections();
        state.IncCounter(StillSecondsCounter, elapsed);
        state.behaviorCounters.TryGetValue(StillSecondsCounter, out float total);
        bool changed = false;
        for (int i = 0; i < Thresholds.Length && i < Titles.Length; i++)
        {
            string key = "idle:stillness_title:" + i;
            if (total < Thresholds[i] || state.behaviorCounters.ContainsKey(key))
                continue;

            state.AwardTitle(Titles[i], i < Descriptions.Length ? Descriptions[i] : string.Empty);
            state.behaviorCounters[key] = 1f;
            state.AddLedgerLine("The system generated a stillness title after " + Mathf.RoundToInt(total) + " seconds of deliberate waiting.");
            changed = true;
        }

        if (changed && manager.autosave)
            manager.Save();
    }

    private Transform ResolvePlayer()
    {
        if (_player != null)
            return _player;
        if (Time.unscaledTime < _nextPlayerResolveTime)
            return null;

        _nextPlayerResolveTime = Time.unscaledTime + 2f;
        if (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative)
        {
            _player = YQInvestorPlayerMotor.ActiveMotor.transform;
            return _player;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player != null ? player.transform : null;
        return _player;
    }
}
