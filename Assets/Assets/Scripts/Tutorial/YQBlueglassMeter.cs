using UnityEngine;
using UnityEngine.UI;

// note: This native tapered meter presents authoritative quantities without changing resources or progression.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class YQBlueglassMeter : MaskableGraphic
{
    protected override void Awake()
    {
        base.Awake();
        // note: Use the current UI mesh path and the same front-face winding as Unity's native graphics.
        useLegacyMeshGeneration=false;
    }
    public float Ratio { get; private set; }
    private float _trail;
    private float _lastSample;
    private bool _initialized;
    public void SetValue(float current, float maximum)
    {
        float next=maximum>0f && !float.IsNaN(current) && !float.IsInfinity(current) ? Mathf.Clamp01(current/maximum) : 0f;
        float previousTrail=_trail;
        float previousRatio=Ratio;
        _trail=!_initialized || YQBlueglassFeedback.ReducedMotion ? next : Mathf.MoveTowards(_trail,next,Mathf.Max(0f,Time.unscaledTime-_lastSample)*.65f);
        _initialized=true;
        _lastSample=Time.unscaledTime;
        Ratio=next;
        // note: Stable resources do not rebuild four HUD meshes every poll; only a changed fill or moving damage trail needs geometry work.
        if (previousRatio!=Ratio || previousTrail!=_trail) SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r=GetPixelAdjustedRect();
        // note: A pearl hairline encloses the dark rail; the cyan-to-pearl fill reads against both sky and terrain.
        Draw(vh,r,1f,new Color(.78f,.91f,1f,.82f),new Color(.94f,.98f,1f,.9f));
        float inset=r.height<8f ? 1f : 2f;
        Rect inner=new Rect(r.xMin+inset,r.yMin+inset,Mathf.Max(0,r.width-inset*2),Mathf.Max(0,r.height-inset*2));
        Draw(vh,inner,1f,new Color(.015f,.045f,.085f,.88f),new Color(.06f,.12f,.20f,.88f));
        if (_trail>Ratio) Draw(vh,inner,_trail,new Color(.65f,.86f,1f,.48f),new Color(.88f,.95f,1f,.6f));
        Draw(vh,inner,Ratio,color,Color.Lerp(color,new Color(.97f,.99f,1f,color.a),.84f));
    }
    private static void Draw(VertexHelper vh,Rect r,float ratio,Color left,Color right)
    {
        // note: Clip the slanted track at the true fill coordinate, including zero and very small quantities.
        if (ratio<=0f || r.width<=0f || r.height<=0f) return;
        float tip=r.xMin+r.width*ratio;
        float bevel=Mathf.Min(r.height*.5f,(tip-r.xMin)*.5f);
        int start=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),left,Vector2.zero);
        vh.AddVert(new Vector3(tip-bevel,r.yMin),right,Vector2.zero);
        vh.AddVert(new Vector3(tip,r.yMax),right,Vector2.zero);
        vh.AddVert(new Vector3(r.xMin+bevel,r.yMax),left,Vector2.zero);
        vh.AddTriangle(start,start+2,start+1);
        vh.AddTriangle(start,start+3,start+2);
    }
}
