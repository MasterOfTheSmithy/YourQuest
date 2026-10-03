using UnityEngine;
using UnityEngine.UI;

// note: Resolution-independent chrome is presentation only; existing native controls retain all input and gameplay ownership.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class YQBlueglassSurface : MaskableGraphic
{
    public enum Form { Panel, Navigation, Tile, Socket, Rule, Tracker }
    private Form _form;
    private bool _selected;
    private bool _interactable=true;
    public bool Hovered { get; private set; }
    private float _flash;
    public float FlashAmount => _flash;
    private bool _reject;
    public static YQBlueglassSurface Attach(Image owner, Form form)
    {
        Transform found=owner.transform.Find("NativeChrome");
        var surface=found!=null ? found.GetComponent<YQBlueglassSurface>() : null;
        if(surface==null)
        {
            var go=new GameObject("NativeChrome",typeof(RectTransform),typeof(LayoutElement),typeof(YQBlueglassSurface));
            go.transform.SetParent(owner.transform,false);
            go.transform.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout=true;
            surface=go.GetComponent<YQBlueglassSurface>();
            surface.rectTransform.anchorMin=Vector2.zero;
            surface.rectTransform.anchorMax=Vector2.one;
            surface.rectTransform.offsetMin=surface.rectTransform.offsetMax=Vector2.zero;
            surface.raycastTarget=false;
        }
        owner.color=Color.clear;
        // note: Buttons raycast the visible mesh, avoiding dependence on an entirely transparent substrate surviving canvas culling.
        owner.canvasRenderer.cullTransparentMesh=false;
        if(owner.TryGetComponent(out Button button))
        {
            owner.raycastTarget=false;
            surface.raycastTarget=true;
            button.targetGraphic=surface;
        }
        var outline=owner.GetComponent<Outline>();
        if(outline!=null) outline.enabled=false;
        if(surface._form!=form) { surface._form=form; surface.SetVerticesDirty(); }
        return surface;
    }
    protected override void Awake() { base.Awake(); useLegacyMeshGeneration=false; }
    public void SetState(bool selected,bool hovered,bool interactable=true)
    {
        hovered=hovered && interactable;
        if(_selected==selected && Hovered==hovered && _interactable==interactable) return;
        _selected=selected; Hovered=hovered; _interactable=interactable; SetVerticesDirty();
    }
    public void Flash(float amount,bool reject)
    {
        _flash=amount; _reject=reject; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r=GetPixelAdjustedRect();
        if(r.width<=0f || r.height<=0f) return;
        Color accent=_reject ? new Color(1f,.65f,.24f) : new Color(.54f,.86f,1f);
        bool emphasis=_selected || Hovered;
        if(_form==Form.Rule)
        {
            Quad(vh,new Rect(r.xMin,r.yMin,r.width,1f),new Color(.65f,.85f,1f,.25f));
            return;
        }
        // note: One small chamfer and a single hairline replace repeated ornamental borders; selection has a separate persistent rail.
        float bevel=_form==Form.Socket ? 8f : 6f;
        float opacity=_form==Form.Navigation ? (emphasis ? .94f : .76f) : .92f;
        Color top=Color.Lerp(new Color(.025f,.065f,.115f,opacity),new Color(.07f,.20f,.31f,opacity),emphasis ? .65f : .15f);
        Color bottom=new Color(.015f,.035f,.065f,opacity);
        if(_flash>0f) top=Color.Lerp(top,new Color(accent.r,accent.g,accent.b,opacity),_flash);
        Color border=emphasis ? new Color(.66f,.91f,1f,.95f) : new Color(.54f,.72f,.86f,.32f);
        // note: Unavailable actions retain their reason/feedback but remain visibly subdued before an attempted click.
        if(!_interactable) { top.a*=.45f; bottom.a*=.45f; border.a*=.45f; }
        Polygon(vh,r,bevel,border,border);
        Rect inner=new Rect(r.xMin+1,r.yMin+1,Mathf.Max(0,r.width-2),Mathf.Max(0,r.height-2));
        Polygon(vh,inner,Mathf.Max(0,bevel-1),bottom,top);
        if(_selected || _form==Form.Tracker)
            Quad(vh,new Rect(r.xMin+1,r.yMin+7,3,Mathf.Max(0,r.height-14)),accent);
        if(_flash>0f)
            Quad(vh,new Rect(r.xMin+bevel,r.yMax-2,Mathf.Max(0,r.width-bevel*2),1),new Color(accent.r,accent.g,accent.b,_flash));
    }
    private static void Polygon(VertexHelper vh,Rect r,float cut,Color bottom,Color top)
    {
        if(r.width<=0 || r.height<=0) return;
        cut=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.25f);
        int s=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),bottom,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax-cut,r.yMin),bottom,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMin+cut),bottom,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),top,Vector2.zero);
        vh.AddVert(new Vector3(r.xMin+cut,r.yMax),top,Vector2.zero);
        vh.AddVert(new Vector3(r.xMin,r.yMax-cut),top,Vector2.zero);
        for(int i=1;i<5;i++) vh.AddTriangle(s,s+i+1,s+i);
    }
    private static void Quad(VertexHelper vh,Rect r,Color color)
    {
        int s=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMin),color,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),color,Vector2.zero);
        vh.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero);
        vh.AddTriangle(s,s+2,s+1); vh.AddTriangle(s,s+3,s+2);
    }
}
