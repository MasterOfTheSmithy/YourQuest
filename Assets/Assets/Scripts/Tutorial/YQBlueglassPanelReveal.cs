using System.Collections;
using UnityEngine;

// note: A short unscaled fade settles inspection panels without moving text, hit targets, or gameplay transforms.
[RequireComponent(typeof(CanvasGroup))]
public sealed class YQBlueglassPanelReveal : MonoBehaviour
{
    private CanvasGroup _group;
    private Coroutine _reveal;
    private void Awake() => _group=GetComponent<CanvasGroup>();
    private void OnEnable()
    {
        if(YQBlueglassFeedback.ReducedMotion) { _group.alpha=1; return; }
        _reveal=StartCoroutine(Reveal());
    }
    private IEnumerator Reveal()
    {
        float elapsed=0;
        while(elapsed<.14f)
        {
            _group.alpha=Mathf.SmoothStep(0,1,elapsed/.14f);
            yield return null;
            elapsed+=Time.unscaledDeltaTime;
        }
        _group.alpha=1; _reveal=null;
    }
    private void OnDisable()
    {
        if(_reveal!=null) StopCoroutine(_reveal);
        _reveal=null;
        if(_group!=null) _group.alpha=1;
    }
}
