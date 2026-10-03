using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQBlueglassControlFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IMoveHandler, IScrollHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Selectable _control;
    private Image _pulse;
    private Image _marker;
    private Coroutine _animation;
    private Vector3 _baseScale;
    private bool _inside;
    private bool _focused;
    private bool _selected;
    private float _nextDragCue;
    public YQBlueglassSurface Surface { get; set; }
    public bool Selected => _selected;
    public bool Hovered => _inside;
    public System.Action HoverEntered;
    public System.Action HoverExited;
    public string RecordKey { get; set; }
    private ScrollRect _dragOwner;
    private bool CanInteract => _control == null || _control.IsInteractable();
#if UNITY_EDITOR
    // note: Optional editor observer records actual pointer dispatch; it never invokes gameplay listeners or alters input.
    public static System.Action<YQBlueglassControlFeedback, string, PointerEventData> PointerObserved;
    private void Observe(string phase, PointerEventData data) => PointerObserved?.Invoke(this, phase, data);
#else
    private void Observe(string phase, PointerEventData data) { }
#endif

    private void Awake()
    {
        _control = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }
    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_marker == null && selected && Surface==null)
        {
            // note: Persistent application selection has a separate notch; hover/navigation focus cannot erase it.
            var go = new GameObject("SelectionNotch", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _marker = go.GetComponent<Image>();
            _marker.raycastTarget = false;
            _marker.color = YQUITheme.Pearl;
            _marker.rectTransform.anchorMin = _marker.rectTransform.anchorMax = new Vector2(0f, .5f);
            _marker.rectTransform.sizeDelta = new Vector2(7f, 7f);
            _marker.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            _marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        }
        if (_marker != null) _marker.enabled = selected && Surface==null;
        RefreshHover();
    }
    public void RefreshHover()
    {
        if(Surface!=null) { Surface.SetState(_selected,_inside || _focused,CanInteract); return; }
        // note: A refreshed record keeps its highlight while the pointer remains inside; native state alone misses style reassignment.
        if (_inside && TryGetComponent(out Button button) && button.targetGraphic is Image graphic && button.IsInteractable())
            graphic.overrideSprite=button.spriteState.highlightedSprite;
    }
    public void Pulse(YQBlueglassCue cue, bool controller = false)
    {
        if (!isActiveAndEnabled) return;
        YQBlueglassFeedback.Request(cue, controller);
        RefreshHover();
        if (_pulse == null && Surface==null)
        {
            // note: A non-raycast overlay provides feedback for buttons, fields, toggles, sliders and scroll surfaces alike.
            var go = new GameObject("InteractionResponse", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            _pulse = go.GetComponent<Image>();
            _pulse.raycastTarget = false;
            _pulse.sprite = YQBlueglassStyle.Sprite("Tile_Hover");
            _pulse.type = Image.Type.Sliced;
            _pulse.pixelsPerUnitMultiplier = 3f;
            _pulse.rectTransform.anchorMin = Vector2.zero;
            _pulse.rectTransform.anchorMax = Vector2.one;
            _pulse.rectTransform.offsetMin = _pulse.rectTransform.offsetMax = Vector2.zero;
        }
        if (_animation != null) StopCoroutine(_animation);
        _animation = StartCoroutine(Animate(cue));
    }
    private IEnumerator Animate(YQBlueglassCue cue)
    {
        float duration = cue == YQBlueglassCue.Confirm || cue == YQBlueglassCue.Reject ? .18f : .10f;
        Color color = cue == YQBlueglassCue.Reject ? new Color(1f,.65f,.24f) : Color.white;
        float end = Time.unscaledTime + duration;
        while (Time.unscaledTime < end)
        {
            float t = Mathf.Clamp01((end-Time.unscaledTime)/duration);
            // note: Animate luminance, not the control transform: glyphs and pointer hit areas stay stationary.
            float strength=cue==YQBlueglassCue.Press || cue==YQBlueglassCue.Confirm ? .34f : .18f;
            color.a = t * strength;
            if(Surface!=null) Surface.Flash(t*strength,cue==YQBlueglassCue.Reject);
            else _pulse.color = color;
            yield return null;
        }
        color.a=_inside ? .14f : 0f;
        if(Surface!=null) Surface.Flash(0,false);
        else _pulse.color=color;
        RefreshHover();
        transform.localScale = _baseScale;
        _animation = null;
    }
    public void OnPointerEnter(PointerEventData e) { Observe("enter",e); if (!_inside) { _inside = true; Pulse(YQBlueglassCue.Hover); HoverEntered?.Invoke(); } }
    public void OnPointerExit(PointerEventData e) { _inside = false; Pulse(YQBlueglassCue.Exit); HoverExited?.Invoke(); }
    public void OnPointerDown(PointerEventData e) { Observe("down",e); Pulse(CanInteract ? YQBlueglassCue.Press : YQBlueglassCue.Reject); }
    public void OnPointerUp(PointerEventData e) { Observe("up",e); Pulse(YQBlueglassCue.Exit); }
    public void OnPointerClick(PointerEventData e) { Observe("click",e); Pulse(CanInteract ? YQBlueglassCue.Select : YQBlueglassCue.Reject); }
    public void OnSelect(BaseEventData e)
    {
        // note: Keyboard/controller navigation receives the same persistent focus and readable inspector as a pointer.
        _focused=YQBlueglassFeedback.ControllerActive;
        if(_focused) { YQBlueglassScrollAffordance.Reveal(transform); HoverEntered?.Invoke(); }
        Pulse(YQBlueglassCue.Hover,YQBlueglassFeedback.ControllerActive);
    }
    public void OnDeselect(BaseEventData e)
    {
        _focused=false;
        if(!_inside) HoverExited?.Invoke();
        Pulse(YQBlueglassCue.Exit,YQBlueglassFeedback.ControllerActive);
    }
    public void OnSubmit(BaseEventData e) => Pulse(CanInteract ? YQBlueglassCue.Select : YQBlueglassCue.Reject, YQBlueglassFeedback.ControllerActive);
    public void OnMove(AxisEventData e) => Pulse(YQBlueglassCue.Hover, YQBlueglassFeedback.ControllerActive);
    // note: Feedback handlers on a row must forward scrolling to its actual scroll owner instead of swallowing native bubbling.
    public void OnScroll(PointerEventData e) { Pulse(YQBlueglassCue.Scroll); ParentScroll()?.OnScroll(e); }
    private ScrollRect ParentScroll() => GetComponent<ScrollRect>() == null ? GetComponentInParent<ScrollRect>() : null;
    public void OnBeginDrag(PointerEventData e) { _dragOwner=ParentScroll(); _dragOwner?.OnInitializePotentialDrag(e); _dragOwner?.OnBeginDrag(e); Pulse(YQBlueglassCue.Press); }
    public void OnDrag(PointerEventData e)
    {
        _dragOwner?.OnDrag(e);
        if (Time.unscaledTime < _nextDragCue) return;
        _nextDragCue=Time.unscaledTime+.08f;
        Pulse(YQBlueglassCue.Scroll);
    }
    public void OnEndDrag(PointerEventData e) { _dragOwner?.OnEndDrag(e); _dragOwner=null; Pulse(YQBlueglassCue.Select); }
    private void OnEnable()
    {
        // note: Native value events cover typed text, slider adjustments, toggles and dropdown selection without polling.
        if (TryGetComponent(out TMP_InputField field)) field.onValueChanged.AddListener(TextChanged);
        if (TryGetComponent(out TMP_Dropdown dropdown)) dropdown.onValueChanged.AddListener(ChoiceChanged);
        if (TryGetComponent(out Toggle toggle)) toggle.onValueChanged.AddListener(ToggleChanged);
        if (TryGetComponent(out Slider slider)) slider.onValueChanged.AddListener(NumberChanged);
    }
    private void TextChanged(string value) => Pulse(YQBlueglassCue.Select);
    private void ChoiceChanged(int value) => Pulse(YQBlueglassCue.Select,YQBlueglassFeedback.ControllerActive);
    private void ToggleChanged(bool value) => Pulse(YQBlueglassCue.Select,YQBlueglassFeedback.ControllerActive);
    private void NumberChanged(float value) => Pulse(YQBlueglassCue.Select,YQBlueglassFeedback.ControllerActive);
    private void OnDisable()
    {
        if (TryGetComponent(out TMP_InputField field)) field.onValueChanged.RemoveListener(TextChanged);
        if (TryGetComponent(out TMP_Dropdown dropdown)) dropdown.onValueChanged.RemoveListener(ChoiceChanged);
        if (TryGetComponent(out Toggle toggle)) toggle.onValueChanged.RemoveListener(ToggleChanged);
        if (TryGetComponent(out Slider slider)) slider.onValueChanged.RemoveListener(NumberChanged);
        // note: Rebuilt rows cannot strand a scale/animation or retain stale hover state.
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        _inside = false;
        _focused = false;
        _dragOwner = null;
        transform.localScale = _baseScale;
        if (_pulse != null) _pulse.color = Color.clear;
        if(Surface!=null) { Surface.Flash(0,false); Surface.SetState(_selected,false); }
    }
}
