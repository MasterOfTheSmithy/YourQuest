using System;
using System.Collections.Generic;
using UnityEngine;

// note: Transient approved-prefab evidence only; this receipt neither changes visibility nor owns actor/equipment state.
[DisallowMultipleComponent]
public sealed class YQGeneratedActorVisualIntent : MonoBehaviour
{
    private readonly Dictionary<Renderer, Mesh> _optionalMeshes = new Dictionary<Renderer, Mesh>();
    private readonly HashSet<Transform> _authoredInactive = new HashSet<Transform>();
    private readonly Dictionary<Transform, Transform> _authoredParents = new Dictionary<Transform, Transform>();
    private EntityInfo _actor;
    private string _entityId;
    private string _approvedPrefabName;
    private string _firstCaptureRejection;
    private int _captureRejectedBranches;
    private int _captureInactiveNodes;
    private int _captureInactiveRenderers;
    private int _captureRendererCountMismatches;
    private int _captureMeshMismatches;
    private string _firstInactiveRendererFailure;

    internal static void Capture(GameObject instance, GameObject approvedPrefab)
    {
        // note: Read authored activeSelf from the approved source, never from an inactive staging parent or a runtime visibility gate.
        if (instance == null || approvedPrefab == null || instance.GetComponent<YQGeneratedActorVisualIntent>() != null)
            return;
        var receipt = instance.AddComponent<YQGeneratedActorVisualIntent>();
        receipt._approvedPrefabName = BoundedName(approvedPrefab.name);
        receipt.CaptureHierarchy(approvedPrefab.transform, instance.transform, false, true);
    }

    internal static void BindActor(EntityInfo actor)
    {
        if (actor == null || string.IsNullOrWhiteSpace(actor.entityId)) return;
        var receipt = actor.GetComponent<YQGeneratedActorVisualIntent>();
        if (receipt == null || receipt._actor != null) return;
        receipt._actor = actor;
        receipt._entityId = actor.entityId;
    }

    internal static bool IsAuthoredInactiveRenderer(Renderer renderer)
    {
        if (renderer == null || renderer.gameObject.activeInHierarchy) return false;
        var receipt = renderer.GetComponentInParent<YQGeneratedActorVisualIntent>(true);
        return receipt != null && receipt.HasCurrentInactiveIntent(renderer);
    }

    private void CaptureHierarchy(Transform source, Transform instance, bool inactivePath, bool root)
    {
        // note: Instantiation callbacks may alter a hierarchy. Ambiguous correspondence stays required, rather than guessing by a mesh name.
        if (source == null || instance == null || (!root && source.name != instance.name) || source.childCount != instance.childCount)
        {
            // note: Record the first exact capture mismatch for a failed native readiness witness; correspondence and acceptance remain fail-closed.
            _captureRejectedBranches++;
            if (_firstCaptureRejection == null)
                _firstCaptureRejection = "source=" + BoundedName(source != null ? source.name : "<missing>") +
                    ",instance=" + BoundedName(instance != null ? instance.name : "<missing>") +
                    ",sourceChildren=" + (source != null ? source.childCount : -1) +
                    ",instanceChildren=" + (instance != null ? instance.childCount : -1) + ",root=" + root;
            return;
        }
        bool authoredInactive = !root && !source.gameObject.activeSelf;
        inactivePath |= authoredInactive;
        if (authoredInactive)
        {
            _authoredInactive.Add(instance);
            _captureInactiveNodes++;
        }
        if (inactivePath)
        {
            Renderer[] originals = source.GetComponents<Renderer>();
            Renderer[] copies = instance.GetComponents<Renderer>();
            // note: Keep bounded evidence of uncaptured authored-inactive renderers; this does not relax the mesh/type/count identity checks below.
            _captureInactiveRenderers += originals.Length;
            if (originals.Length != copies.Length)
            {
                _captureRendererCountMismatches++;
                if (_firstInactiveRendererFailure == null)
                    _firstInactiveRendererFailure = "rendererCount:source=" + originals.Length + ",instance=" + copies.Length +
                        ",node=" + BoundedName(source.name);
            }
            if (originals.Length == copies.Length)
                for (int index = 0; index < originals.Length; index++)
                    if (HasSameAuthoredMesh(originals[index], copies[index]))
                    {
                        _optionalMeshes[copies[index]] = GetAuthoredMesh(copies[index]);
                        for (Transform current = instance; current != null && current != transform; current = current.parent)
                            _authoredParents[current] = current.parent;
                    }
                    else
                    {
                        _captureMeshMismatches++;
                        if (_firstInactiveRendererFailure == null)
                            _firstInactiveRendererFailure = "rendererPair:source={" + DescribeRendererMesh(originals[index]) +
                                "},instance={" + DescribeRendererMesh(copies[index]) + "}";
                    }
        }
        for (int index = 0; index < source.childCount; index++)
            CaptureHierarchy(source.GetChild(index), instance.GetChild(index), inactivePath, false);
    }

    private static bool HasSameAuthoredMesh(Renderer source, Renderer instance)
    {
        // note: Only the corresponding approved rigid/skinned mesh qualifies; effects or replacement geometry receive no exemption.
        return source != null && instance != null && source.GetType() == instance.GetType() &&
            GetAuthoredMesh(source) != null && GetAuthoredMesh(source) == GetAuthoredMesh(instance);
    }

    private static Mesh GetAuthoredMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        return renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
    }

    private bool HasCurrentInactiveIntent(Renderer renderer)
    {
        // note: An inactive/missing actor, changed identity, reparented mesh, or newly hidden required ancestor remains a readiness failure.
        if (_actor == null || _actor.gameObject != gameObject || !gameObject.activeInHierarchy ||
            !string.Equals(_actor.entityId, _entityId, StringComparison.Ordinal) ||
            !_optionalMeshes.TryGetValue(renderer, out Mesh authoredMesh) || authoredMesh == null || GetAuthoredMesh(renderer) != authoredMesh)
            return false;
        bool hiddenAsAuthored = false;
        Transform current = renderer.transform;
        while (current != null && current != transform)
        {
            if (!_authoredParents.TryGetValue(current, out Transform expectedParent) || current.parent != expectedParent)
                return false;
            if (!current.gameObject.activeSelf)
            {
                if (!_authoredInactive.Contains(current)) return false;
                hiddenAsAuthored = true;
            }
            current = current.parent;
        }
        return current == transform && hiddenAsAuthored;
    }

    internal static string DescribeRendererIntent(Renderer renderer)
    {
        // note: Diagnostics only. Do not infer optional equipment from names or repair activation when a receipt is missing or its exact ownership has changed.
        if (renderer == null) return "rendererMissing";
        var receipt = renderer.GetComponentInParent<YQGeneratedActorVisualIntent>(true);
        var nearestActor = renderer.GetComponentInParent<EntityInfo>(true);
        if (receipt == null)
            return "receipt=missing,nearestActor=" + BoundedName(nearestActor != null ? nearestActor.name : "<missing>") +
                ",entity=" + BoundedName(nearestActor != null ? nearestActor.entityId : null);
        string reason = receipt.DescribeInactiveIntentFailure(renderer);
        return "receipt=" + BoundedName(receipt.name) + ",prefab=" + receipt._approvedPrefabName +
            ",reason=" + reason + ",bound=" + (receipt._actor != null) +
            ",entity=" + BoundedName(receipt._actor != null ? receipt._actor.entityId : null) +
            ",capturedEntity=" + BoundedName(receipt._entityId) + ",optionalCount=" + receipt._optionalMeshes.Count +
            ",captureRejected=" + receipt._captureRejectedBranches + ",firstCaptureRejection={" + receipt._firstCaptureRejection + "}" +
            ",sourceInactiveNodes=" + receipt._captureInactiveNodes + ",sourceInactiveRenderers=" + receipt._captureInactiveRenderers +
            ",rendererCountMismatches=" + receipt._captureRendererCountMismatches + ",meshMismatches=" + receipt._captureMeshMismatches +
            ",firstInactiveRendererFailure={" + receipt._firstInactiveRendererFailure + "}";
    }

    private string DescribeInactiveIntentFailure(Renderer renderer)
    {
        if (_actor == null) return "actorUnboundOrDestroyed";
        if (_actor.gameObject != gameObject) return "actorRootMismatch";
        if (!gameObject.activeInHierarchy) return "actorInactive";
        if (!string.Equals(_actor.entityId, _entityId, StringComparison.Ordinal)) return "actorIdentityChanged";
        if (!_optionalMeshes.TryGetValue(renderer, out Mesh authoredMesh)) return "rendererNotCaptured";
        if (authoredMesh == null) return "authoredMeshMissing";
        if (GetAuthoredMesh(renderer) != authoredMesh) return "rendererMeshChanged";
        bool hiddenAsAuthored = false;
        Transform current = renderer.transform;
        int inspected = 0;
        while (current != null && current != transform && inspected++ < 128)
        {
            if (!_authoredParents.TryGetValue(current, out Transform expectedParent)) return "parentNotCaptured:" + BoundedName(current.name);
            if (current.parent != expectedParent) return "parentChanged:" + BoundedName(current.name);
            if (!current.gameObject.activeSelf)
            {
                if (!_authoredInactive.Contains(current)) return "requiredAncestorInactive:" + BoundedName(current.name);
                hiddenAsAuthored = true;
            }
            current = current.parent;
        }
        if (current != transform) return "actorAncestorMissingOrDiagnosticDepthBound";
        return hiddenAsAuthored ? "authoredInactiveAccepted" : "noAuthoredInactiveAncestor";
    }

    private static string DescribeRendererMesh(Renderer renderer)
    {
        if (renderer == null) return "missingRenderer";
        Mesh mesh = GetAuthoredMesh(renderer);
        return "name=" + BoundedName(renderer.name) + ",type=" + renderer.GetType().Name +
            ",activeSelf=" + renderer.gameObject.activeSelf + ",rendererId=" + renderer.GetInstanceID() +
            ",meshName=" + (mesh != null ? BoundedName(mesh.name) : "<missing>") +
            ",meshId=" + (mesh != null ? mesh.GetInstanceID() : 0);
    }

    private static string BoundedName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";
        return value.Length <= 96 ? value : value.Substring(0, 96);
    }

    private void OnDestroy()
    {
        _actor = null;
        _entityId = null;
        _approvedPrefabName = null;
        _firstCaptureRejection = null;
        _captureRejectedBranches = 0;
        _captureInactiveNodes = 0;
        _captureInactiveRenderers = 0;
        _captureRendererCountMismatches = 0;
        _captureMeshMismatches = 0;
        _firstInactiveRendererFailure = null;
        _optionalMeshes.Clear();
        _authoredInactive.Clear();
        _authoredParents.Clear();
    }
}
