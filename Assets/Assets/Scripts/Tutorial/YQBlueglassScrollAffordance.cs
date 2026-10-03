using UnityEngine;
using UnityEngine.UI;

// note: Shared scroll chrome uses the existing ScrollRect; it never duplicates content or changes selection.
[DisallowMultipleComponent]
public sealed class YQBlueglassScrollAffordance : MonoBehaviour
{
    private ScrollRect _scroll;
    private Image _above;
    private Image _below;
    private float _nextCue;
    private Vector2 _lastSizes;

    public static void Attach(ScrollRect scroll)
    {
        if(scroll == null || scroll.viewport == null || scroll.content == null || !scroll.vertical || scroll.horizontal) return;
        if(scroll.GetComponent<YQBlueglassScrollAffordance>() != null) return;
        scroll.gameObject.AddComponent<YQBlueglassScrollAffordance>().Build(scroll);
    }

    private static Image Graphic(Transform parent,string name,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();
        image.color=color;
        image.raycastTarget=false;
        return image;
    }

    private void Build(ScrollRect scroll)
    {
        _scroll=scroll;
        // note: Reserve a permanent gutter so appearance of overflow never reflows the text under the pointer.
        if(scroll.verticalScrollbar==null)
        {
            Vector2 inset=scroll.viewport.offsetMax;
            scroll.viewport.offsetMax=new Vector2(inset.x-20,inset.y);
            Image track=Graphic(transform,"__YQ_ScrollRail",new Color(.30f,.48f,.60f,.20f));
            RectTransform rect=track.rectTransform;
            rect.anchorMin=new Vector2(1,0); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(1,.5f);
            rect.offsetMin=new Vector2(-20,10); rect.offsetMax=new Vector2(-4,-10);
            track.raycastTarget=true;
            var scrollbar=track.gameObject.AddComponent<Scrollbar>();
            Image handle=Graphic(track.transform,"__YQ_ScrollHandle",new Color(.65f,.83f,.94f,.85f));
            handle.rectTransform.offsetMin=new Vector2(5,0); handle.rectTransform.offsetMax=new Vector2(-5,0);
            handle.raycastTarget=true;
            scrollbar.handleRect=handle.rectTransform;
            scrollbar.targetGraphic=handle;
            scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=scrollbar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            scrollbar.onValueChanged.AddListener(ScrollFeedback);
        }
        _above=Graphic(scroll.viewport,"__YQ_MoreAbove",new Color(.59f,.85f,1,.7f));
        _below=Graphic(scroll.viewport,"__YQ_MoreBelow",new Color(.59f,.85f,1,.7f));
        foreach(Image edge in new[]{_above,_below})
        {
            var layout=edge.gameObject.AddComponent<LayoutElement>(); layout.ignoreLayout=true;
            bool top=edge==_above;
            edge.rectTransform.anchorMin=new Vector2(.35f,top ? 1 : 0);
            edge.rectTransform.anchorMax=new Vector2(.65f,top ? 1 : 0);
            edge.rectTransform.pivot=new Vector2(.5f,top ? 1 : 0);
            edge.rectTransform.sizeDelta=new Vector2(0,2);
        }
        scroll.onValueChanged.AddListener(Refresh);
        Refresh(Vector2.zero);
    }

    private void ScrollFeedback(float value)
    {
        if(Time.unscaledTime<_nextCue || _scroll==null || !isActiveAndEnabled) return;
        _nextCue=Time.unscaledTime+.09f;
        YQBlueglassFeedback.Request(YQBlueglassCue.Scroll,YQBlueglassFeedback.ControllerActive);
    }
    private void Refresh(Vector2 value)
    {
        if(_scroll==null || _above==null) return;
        bool overflow=_scroll.content.rect.height>_scroll.viewport.rect.height+1;
        _above.enabled=overflow && _scroll.verticalNormalizedPosition<.998f;
        _below.enabled=overflow && _scroll.verticalNormalizedPosition>.002f;
    }
    private void CanvasReady()
    {
        // note: Layout can change without scroll input; only reconsider cues when the two relevant heights change.
        if(_scroll==null) return;
        Vector2 sizes=new Vector2(_scroll.content.rect.height,_scroll.viewport.rect.height);
        if(sizes==_lastSizes) return;
        _lastSizes=sizes; Refresh(Vector2.zero);
    }
    private void OnEnable() => Canvas.willRenderCanvases+=CanvasReady;
    private void OnDisable() => Canvas.willRenderCanvases-=CanvasReady;
    private void OnDestroy()
    {
        if(_scroll==null) return;
        _scroll.onValueChanged.RemoveListener(Refresh);
        if(_scroll.verticalScrollbar!=null) _scroll.verticalScrollbar.onValueChanged.RemoveListener(ScrollFeedback);
    }
    public static void Reveal(Transform target)
    {
        var scroll=target.GetComponentInParent<ScrollRect>();
        if(scroll==null || scroll.content==null || scroll.viewport==null || !target.IsChildOf(scroll.content)) return;
        // note: Navigation focus moves only enough to reveal its row, preserving surrounding context.
        Canvas.ForceUpdateCanvases();
        Bounds bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);
        Rect view=scroll.viewport.rect;
        float offset=bounds.max.y>view.yMax ? view.yMax-bounds.max.y : bounds.min.y<view.yMin ? view.yMin-bounds.min.y : 0;
        if(Mathf.Abs(offset)<.5f) return;
        scroll.StopMovement();
        scroll.content.anchoredPosition+=new Vector2(0,offset);
    }
}
