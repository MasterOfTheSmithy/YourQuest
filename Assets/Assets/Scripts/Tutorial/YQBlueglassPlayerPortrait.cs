using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQBlueglassPlayerPortrait : MonoBehaviour
{
    private sealed class Part
    {
        public Renderer source;
        public Renderer view;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    }
    // note: Only transforms, approved meshes/materials, and a presentation Animator are copied; gameplay scripts and colliders never enter this rig.
    private static readonly Vector3 PreviewOrigin=new Vector3(0f,-10000f,0f);
    private readonly Dictionary<Transform,Transform> _nodes=new Dictionary<Transform,Transform>();
    private readonly List<Part> _parts=new List<Part>();
    private GameObject _previewRoot;
    private Transform _rig;
    private Transform _visualRoot;
    private YQInvestorPlayerMotor _motor;
    private Light _light;
    private Camera _camera;
    private RenderTexture _texture;
    private RawImage _image;
    private float _nextParts;
    private Animator _animator;
    private AnimationClip _idleClip;
    private AnimationClipPlayable _idle;
    private PlayableGraph _graph;
    private double _idleTime;
    public TMP_Text ViewModeText { get; set; }
    public int VisibleParts => _parts.Count;
    public Camera PresentationCamera => _camera;
    public double IdlePlaybackTime => _idleTime;
    public string IdleAnimationName => _idleClip!=null ? _idleClip.name : string.Empty;
    public int PoseSignature
    {
        get
        {
            // note: The focused witness samples actual skeletal motion, rather than treating a running clock as proof of animation.
            int hash=17;
            foreach(Transform node in _nodes.Values)
                if(node!=null) { Quaternion q=node.localRotation; unchecked { hash=hash*31+Mathf.RoundToInt(q.x*10000); hash=hash*31+Mathf.RoundToInt(q.y*10000); hash=hash*31+Mathf.RoundToInt(q.z*10000); } }
            return hash;
        }
    }
    private void Awake()
    {
        _image=GetComponent<RawImage>(); _image.raycastTarget=false;
        _texture=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32) { name="YourQuest animated player-only portrait" };
        _texture.Create(); _image.texture=_texture;
        _previewRoot=new GameObject("YourQuest portrait geometry"); _previewRoot.transform.position=PreviewOrigin;
        _camera=new GameObject("Equipment presentation camera",typeof(Camera)).GetComponent<Camera>();
        _camera.transform.SetParent(_previewRoot.transform,false);
        _camera.targetTexture=_texture; _camera.fieldOfView=36; _camera.nearClipPlane=.1f; _camera.farClipPlane=12;
        _camera.clearFlags=CameraClearFlags.SolidColor; _camera.backgroundColor=Color.clear;
        _camera.cullingMask=1<<31; _camera.allowHDR=false; _camera.useOcclusionCulling=false;
        var data=_camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false; data.volumeLayerMask=0;
        _light=new GameObject("Portrait key light",typeof(Light)).GetComponent<Light>();
        _light.transform.SetParent(_previewRoot.transform,false);
        _light.type=LightType.Point; _light.range=10; _light.cullingMask=1<<31; _light.intensity=4; _light.shadows=LightShadows.None;
        Vector3 target=PreviewOrigin+Vector3.up*1.05f;
        _camera.transform.position=target+Vector3.forward*3.6f+Vector3.right*1.25f+Vector3.up*.35f;
        _camera.transform.LookAt(target);
        _light.transform.position=PreviewOrigin+Vector3.up*2.6f+Vector3.forward*2.25f;
    }
    private void OnEnable() { if(_previewRoot!=null) _previewRoot.SetActive(true); _nextParts=0; }
    private void LateUpdate()
    {
        _motor=YQInvestorPlayerMotor.ActiveMotor;
        _camera.enabled=_image.enabled=_motor!=null;
        if(_motor==null) return;
        if(ViewModeText!=null) ViewModeText.text=_motor.firstPerson ? "VIEW  /  FIRST PERSON" : "VIEW  /  THIRD PERSON";
        if(Time.unscaledTime>=_nextParts) { _nextParts=Time.unscaledTime+1; ResolveParts(); }
        if(!_graph.IsValid() || _idleClip==null) return;
        // note: Manual unscaled playback keeps the menu idle alive at timescale zero without advancing the authoritative player's animation or combat.
        _idleTime=(_idleTime+Time.unscaledDeltaTime)%Mathf.Max(.01f,_idleClip.length);
        _idle.SetTime(_idleTime);
        _graph.Evaluate(0);
    }
    private Transform CopyNode(Transform source)
    {
        if(source==null) return null;
        // note: Bone bindings cannot pull an unrelated world hierarchy into this presentation-only rig.
        if(source!=_visualRoot && !source.IsChildOf(_visualRoot)) return null;
        if(_nodes.TryGetValue(source,out Transform found)) return found;
        Transform parent=source==_visualRoot ? _previewRoot.transform : CopyNode(source.parent);
        var node=new GameObject(source.name).transform; node.gameObject.layer=31;
        node.SetParent(parent,false);
        node.localPosition=source==_visualRoot ? Vector3.zero : source.localPosition;
        node.localRotation=source==_visualRoot ? Quaternion.identity : source.localRotation;
        node.localScale=source.localScale;
        _nodes.Add(source,node);
        if(source==_visualRoot) _rig=node;
        return node;
    }
    private void ResolveParts()
    {
        var visual=_motor.GetComponent<YQPlayerEquipmentVisual>();
        Transform sourceRoot=visual!=null ? visual.CharacterPresentationRoot : null;
        if(sourceRoot!=_visualRoot) { ClearRig(); _visualRoot=sourceRoot; }
        if(_visualRoot==null) return;
        foreach(Transform source in _visualRoot.GetComponentsInChildren<Transform>(true)) CopyNode(source);
        for(int i=_parts.Count-1;i>=0;i--)
            if(_parts[i].source==null) { Destroy(_parts[i].view); _parts.RemoveAt(i); }
        foreach(Renderer source in _visualRoot.GetComponentsInChildren<Renderer>(true))
        {
            if(!(source is SkinnedMeshRenderer) && !(source is MeshRenderer)) continue;
            bool found=false; foreach(Part part in _parts) if(part.source==source) { found=true; break; }
            if(found) continue;
            Renderer view;
            if(source is SkinnedMeshRenderer skin)
            {
                var copy=CopyNode(source.transform).gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh=skin.sharedMesh; copy.rootBone=CopyNode(skin.rootBone);
                var bones=skin.bones; var mapped=new Transform[bones.Length];
                for(int i=0;i<bones.Length;i++) mapped[i]=CopyNode(bones[i]);
                copy.bones=mapped; copy.localBounds=skin.localBounds; copy.updateWhenOffscreen=true;
                if(skin.sharedMesh!=null) for(int i=0;i<skin.sharedMesh.blendShapeCount;i++) copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
                view=copy;
            }
            else
            {
                var node=CopyNode(source.transform).gameObject;
                var filter=node.AddComponent<MeshFilter>(); filter.sharedMesh=source.GetComponent<MeshFilter>()?.sharedMesh;
                view=node.AddComponent<MeshRenderer>();
            }
            view.shadowCastingMode=ShadowCastingMode.Off; view.receiveShadows=false;
            _parts.Add(new Part {source=source,view=view});
        }
        // note: Equipment/material arrays and runtime tints refresh at the bounded rescan cadence; shared assets remain untouched.
        foreach(Part part in _parts)
            if(part.source!=null)
            {
                part.view.enabled=IsPresented(part.source);
                part.view.sharedMaterials=part.source.sharedMaterials;
                part.source.GetPropertyBlock(part.properties);
                part.view.SetPropertyBlock(part.properties);
            }
        Animator sourceAnimator=visual.CharacterAnimator;
        if(_graph.IsValid() || sourceAnimator==null || sourceAnimator.runtimeAnimatorController==null) return;
        foreach(AnimationClip clip in sourceAnimator.runtimeAnimatorController.animationClips)
            if(clip!=null && clip.name.IndexOf("idle",System.StringComparison.OrdinalIgnoreCase)>=0) { _idleClip=clip; break; }
        if(_idleClip==null) return;
        _animator=CopyNode(sourceAnimator.transform).gameObject.AddComponent<Animator>();
        _animator.avatar=sourceAnimator.avatar; _animator.applyRootMotion=false; _animator.fireEvents=false;
        _animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        _graph=PlayableGraph.Create("YourQuest menu idle"); _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        _idle=AnimationClipPlayable.Create(_graph,_idleClip);
        var output=AnimationPlayableOutput.Create(_graph,"Player presentation",_animator); output.SetSourcePlayable(_idle);
        _graph.Play();
    }
    private bool IsPresented(Renderer source)
    {
        if(!source.enabled || _visualRoot==null) return false;
        // note: Ignore only the perspective-hidden rig root; inactive outfit variants and replaced equipment remain hidden.
        for(Transform node=source.transform;node!=null && node!=_visualRoot;node=node.parent)
            if(!node.gameObject.activeSelf) return false;
        return true;
    }
    private void ClearRig()
    {
        if(_graph.IsValid()) _graph.Destroy();
        if(_rig!=null) Destroy(_rig.gameObject);
        _nodes.Clear(); _parts.Clear(); _idleClip=null; _idleTime=0; _animator=null; _rig=null;
    }
    private void OnDisable() { if(_previewRoot!=null) _previewRoot.SetActive(false); }
    private void OnDestroy()
    {
        ClearRig();
        if(_camera!=null) _camera.targetTexture=null;
        if(_previewRoot!=null) Destroy(_previewRoot);
        if(_texture!=null) { _texture.Release(); Destroy(_texture); }
    }
}
