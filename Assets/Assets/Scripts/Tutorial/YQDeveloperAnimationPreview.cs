#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQDeveloperAnimationPreview : MonoBehaviour
{
    private Animator _animator;
    private RuntimeAnimatorController _controller;
    private AnimatorStateInfo[] _states;
    private readonly Dictionary<int, float> _floats = new Dictionary<int, float>();
    private readonly Dictionary<int, int> _ints = new Dictionary<int, int>();
    private readonly Dictionary<int, bool> _bools = new Dictionary<int, bool>();
    private readonly List<Behaviour> _drivers = new List<Behaviour>();
    private bool _rootMotion;
    private float _speed;
    public bool IsPreviewing => _animator != null;

    public void Begin(Animator animator)
    {
        // note: Snapshot presentation state only. Suspend existing drivers rather than mutating their private gameplay flags.
        _animator = animator; _controller = animator.runtimeAnimatorController; _rootMotion = animator.applyRootMotion; _speed = animator.speed;
        _states = new AnimatorStateInfo[animator.layerCount];
        for (int i = 0; i < _states.Length; i++) _states[i] = animator.GetCurrentAnimatorStateInfo(i);
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float) _floats[p.nameHash] = animator.GetFloat(p.nameHash);
            if (p.type == AnimatorControllerParameterType.Int) _ints[p.nameHash] = animator.GetInteger(p.nameHash);
            if (p.type == AnimatorControllerParameterType.Bool) _bools[p.nameHash] = animator.GetBool(p.nameHash);
            if (p.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(p.nameHash);
        }
        foreach (var driver in GetComponentsInChildren<Behaviour>(true))
            if (driver.enabled && (driver is YQPlayerEquipmentVisual || driver is YQGeneratedNpcWander || driver is YQInvestorEnemy || driver is YQDotCreatureVisual))
            { _drivers.Add(driver); driver.enabled = false; }
        animator.applyRootMotion = false; animator.speed = 1;
    }
    public void Stop()
    {
        // note: A streamed/rebound controller must never be replaced with an obsolete captured asset.
        if (_animator != null && _animator.runtimeAnimatorController == _controller)
        {
            foreach (var p in _animator.parameters) if (p.type == AnimatorControllerParameterType.Trigger) _animator.ResetTrigger(p.nameHash);
            foreach (var p in _floats) _animator.SetFloat(p.Key, p.Value);
            foreach (var p in _ints) _animator.SetInteger(p.Key, p.Value);
            foreach (var p in _bools) _animator.SetBool(p.Key, p.Value);
            for (int i = 0; i < _states.Length && i < _animator.layerCount; i++) if (_animator.HasState(i, _states[i].fullPathHash)) _animator.Play(_states[i].fullPathHash, i, _states[i].normalizedTime);
            _animator.applyRootMotion = _rootMotion; _animator.speed = _speed;
        }
        foreach (var driver in _drivers) if (driver != null) driver.enabled = true;
        _drivers.Clear(); _floats.Clear(); _ints.Clear(); _bools.Clear(); _animator = null;
    }
    private void OnDisable() => Stop();
}
#endif
