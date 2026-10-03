using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// note: Motion changes only presentation transforms/opacity; cursor, pause and record ownership remain with the existing menu.
public sealed class YQBlueglassQuickMenuMotion : MonoBehaviour
{
    private sealed class Part
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public float delay;
        public bool root;
    }
    private readonly List<Part> _parts=new List<Part>();
    private readonly List<Part> _categories=new List<Part>();
    private RectTransform _entries;
    private float _height;
    private float _phase;
    private float _branch=1;
    private bool _opening;
    private Action _closed;
    private int _sourceRow;
    private int _depth;
    private readonly float[] _depthReveal=new float[3];
    public bool IsAnimating => (_opening ? _phase<1 || _branch<1 : _phase>0) ||
        _depthReveal[0]!=(_depth>=1 ? 1f : 0f) || _depthReveal[1]!=(_depth>=2 ? 1f : 0f) || _depthReveal[2]!=(_depth>=3 ? 1f : 0f);
    public float Reveal => _phase;
    public void SetDepth(int depth,bool immediate=false)
    {
        // note: Hover owns side-window visibility independently of the Alt root's open/close animation.
        _depth=Mathf.Clamp(depth,0,3);
        if(immediate || YQBlueglassFeedback.ReducedMotion)
            for(int i=0;i<3;i++) _depthReveal[i]=_depth>i ? 1 : 0;
        Apply();
    }
    public void PrepareLayout()
    {
        // note: Restore canonical positions before the owner's bounded refresh; animated offsets never become the next layout's starting coordinates.
        foreach(Part part in _parts) if(part.rect!=null) part.rect.anchoredPosition=part.position;
        foreach(Part part in _categories) if(part.rect!=null) part.rect.anchoredPosition=part.position;
        if(_entries!=null) _entries.sizeDelta=new Vector2(_entries.sizeDelta.x,_height);
    }
    public void CaptureLayout(RectTransform entries,int sourceRow,bool replayBranch)
    {
        _parts.Clear(); _categories.Clear(); _entries=entries; _height=entries.sizeDelta.y; _sourceRow=sourceRow;
        foreach(RectTransform child in transform)
        {
            bool root=child.name=="QuickPlayer" || child.name=="QuickItems" || child.name=="QuickJournal";
            float delay=root ? 0 : child.name=="QuickCategories" ? .15f : child.name=="QuickEntriesArea" ? .32f : .58f;
            var group=child.GetComponent<CanvasGroup>();
            if(group==null) group=child.gameObject.AddComponent<CanvasGroup>();
            _parts.Add(new Part {rect=child,group=group,position=child.anchoredPosition,root=root,delay=delay});
            if(child.name=="QuickCategories")
                foreach(RectTransform category in child)
                    if(category.GetComponent<Button>()!=null) _categories.Add(new Part {rect=category,position=category.anchoredPosition});
        }
        if(replayBranch) _branch=YQBlueglassFeedback.ReducedMotion ? 1 : 0;
        Apply();
    }
    public void Open()
    {
        _closed=null; _opening=true;
        // note: Reholding Alt reverses the current exit rather than snapping from a new zero pose.
        if(YQBlueglassFeedback.ReducedMotion) _phase=_branch=1;
        Apply();
    }
    public void Close(Action completed,bool immediate)
    {
        _opening=false; _closed=completed;
        if(immediate || YQBlueglassFeedback.ReducedMotion) _phase=0;
        Apply();
        CompleteClose();
    }
    private void Update()
    {
        if(!IsAnimating) return;
        // note: A streaming or focus hitch cannot consume the entire reveal in one rendered frame; gameplay input still releases immediately in the menu owner.
        float step=Mathf.Min(Time.unscaledDeltaTime,.05f);
        _phase=Mathf.MoveTowards(_phase,_opening ? 1 : 0,step/(_opening ? .36f : .16f));
        _branch=Mathf.MoveTowards(_branch,1,step/.28f);
        for(int i=0;i<3;i++) _depthReveal[i]=Mathf.MoveTowards(_depthReveal[i],_depth>i ? 1 : 0,step/.18f);
        Apply(); CompleteClose();
    }
    private static float Ease(float value) { value=Mathf.Clamp01(value); return 1-Mathf.Pow(1-value,3); }
    private void Apply()
    {
        float branch=Mathf.Min(_phase,_branch);
        foreach(Part part in _parts)
        {
            if(part.rect==null) continue;
            float t=Ease(((part.root ? _phase : branch)-part.delay)/(1-part.delay));
            int depth=part.root || part.rect.name=="InputHints" ? 0 : part.rect.name=="QuickCategories" ? 1 : part.rect.name=="QuickEntriesArea" ? 2 : 3;
            if(depth>0) t*=Ease(_depthReveal[depth-1]);
            Vector2 start=part.root ? new Vector2(-14,-20) : new Vector2(0,-20-_sourceRow*80);
            part.rect.anchoredPosition=Vector2.Lerp(start,part.position,t);
            // note: A branch travels out of its source box before its labels fade in, preventing stacked words during the shared-origin transition.
            part.group.alpha=part.root ? t : Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.8f)/.2f));
            // note: Exiting visuals never keep intercepting clicks after the cursor has returned to gameplay.
            part.group.blocksRaycasts=part.group.interactable=_opening && (depth==0 || _depth>=depth) && t>.85f;
        }
        // note: Secondary options spread from their first box rather than appearing as an already-expanded column.
        float categorySpread=Ease((branch-.22f)/.78f)*Ease(_depthReveal[0]);
        foreach(Part category in _categories)
            if(category.rect!=null) category.rect.anchoredPosition=Vector2.Lerp(Vector2.zero,category.position,categorySpread);
        if(_entries!=null)
        {
            float spread=Ease((branch-.38f)/.62f)*Ease(_depthReveal[1]);
            _entries.sizeDelta=new Vector2(_entries.sizeDelta.x,Mathf.Lerp(Mathf.Min(84,_height),_height,spread));
        }
    }
    private void CompleteClose()
    {
        if(_opening || _phase>0 || _closed==null) return;
        Action callback=_closed; _closed=null; callback();
    }
}
