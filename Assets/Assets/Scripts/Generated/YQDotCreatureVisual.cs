using UnityEngine;

// note: Observe the existing actor's resolved motion. This component never moves an actor, processes damage, or writes persistent state.
public sealed class YQDotCreatureVisual : MonoBehaviour
{
    private Animator[] _animators;
    private Vector3 _previous;
    private float _authoredSpeed;
    private YQDotMotionProfile _motionProfile;
    private int _gaitState;
    private float _gaitTime;
    private static readonly int Moving = Animator.StringToHash("DotMoving");
    private static readonly int Rate = Animator.StringToHash("DotRate");

    public static YQDotCreatureVisual Bind(GameObject root, string prefabPath)
    {
        var catalog = YQDotCreatureCatalog.Current;
        if (root == null || catalog == null || !catalog.TryGetPrefab(prefabPath, out var entry) || !string.IsNullOrEmpty(entry.moduleSlot)) return null;
        // note: Bind after the production actor's imported-behaviour sanitation, using all authored LOD rigs without adding a gameplay owner.
        var visual = root.GetComponent<YQDotCreatureVisual>() ?? root.AddComponent<YQDotCreatureVisual>();
        visual._animators = root.GetComponentsInChildren<Animator>(true);
        visual._authoredSpeed = Mathf.Max(.01f, entry.authoredWalkSpeed);
        // note: Unity inline serialization may materialize an empty profile for legacy entries; only validated delivered samples opt into phase-driven presentation.
        visual._motionProfile = entry.motionProfile != null && entry.motionProfile.IsValid ? entry.motionProfile : null;
        visual._gaitState = Animator.StringToHash("DotIdle"); visual._gaitTime = 0f;
        visual._previous = root.transform.position;
        return visual;
    }

    public float AuthoredWalkSpeed => _authoredSpeed;
    public YQDotMotionProfile MotionProfile => _motionProfile;
    public void SetAuthoredGait(YQDotGaitSampler sampler)
    {
        // note: The existing wander owner supplies the approved phase; this visual component still never changes actor position.
        _gaitState = Animator.StringToHash(sampler.StateName); _gaitTime = sampler.NormalizedTime;
    }
    private void OnEnable() => _previous = transform.position;
    private void LateUpdate()
    {
        if (_animators == null) return;
        Vector3 delta = transform.position - _previous; delta.y = 0f; _previous = transform.position;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        // note: Collision-resolved travel controls visual cadence; root motion stays disabled and teleports cannot accelerate animation indefinitely.
        foreach (var animator in _animators)
        {
            if (animator == null || animator.runtimeAnimatorController == null) continue;
            animator.applyRootMotion = false;
            if (_motionProfile != null)
            {
                animator.SetFloat(Rate, 1f);
                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _gaitState)
                { animator.Play(_gaitState, 0, _gaitTime); animator.Update(0f); }
                continue;
            }
            animator.SetBool(Moving, speed > .025f);
            animator.SetFloat(Rate, Mathf.Clamp(speed / _authoredSpeed, .1f, 3f));
        }
    }
}
