using System.Collections.Generic;
using UnityEngine;

public enum YQDistrictFunction
{
    Unknown = 0,
    Residential = 1,
    MixedUse = 2,
    Service = 3,
    Defensive = 4
}

[DisallowMultipleComponent]
public sealed class YQWorldDistrictDescriptor : MonoBehaviour
{
    [SerializeField]
    private YQDistrictFunction districtFunction =
        YQDistrictFunction.Unknown;

    [SerializeField]
    private int sourceInstanceCount;

    [SerializeField]
    private int authoredBuildingCount;

    [SerializeField]
    private int authoredDressingCount;

    [SerializeField]
    private Vector3 authoredSourceOrigin;

    [SerializeField]
    private Vector3 localBoundsCenter;

    [SerializeField]
    private Vector3 localBoundsSize;

    [SerializeField]
    private List<string> connectionSocketPaths =
        new List<string>();

    public YQDistrictFunction DistrictFunction => districtFunction;
    public int SourceInstanceCount => sourceInstanceCount;
    public int AuthoredBuildingCount => authoredBuildingCount;
    public int AuthoredDressingCount => authoredDressingCount;
    public Vector3 AuthoredSourceOrigin => authoredSourceOrigin;
    public Vector3 LocalBoundsCenter => localBoundsCenter;
    public Vector3 LocalBoundsSize => localBoundsSize;
    public IReadOnlyList<string> ConnectionSocketPaths => connectionSocketPaths;

    public void Configure(
        YQDistrictFunction newDistrictFunction,
        int newSourceInstanceCount,
        int newAuthoredBuildingCount,
        int newAuthoredDressingCount,
        Vector3 newAuthoredSourceOrigin,
        Vector3 newLocalBoundsCenter,
        Vector3 newLocalBoundsSize,
        IEnumerable<string> newConnectionSocketPaths)
    {
        // note: District metadata records deterministic spatial facts; generated narrative intent may select a district but cannot rewrite its authored geometry.
        districtFunction = newDistrictFunction;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
        authoredBuildingCount = Mathf.Max(0, newAuthoredBuildingCount);
        authoredDressingCount = Mathf.Max(0, newAuthoredDressingCount);
        authoredSourceOrigin = newAuthoredSourceOrigin;
        localBoundsCenter = newLocalBoundsCenter;
        localBoundsSize = newLocalBoundsSize;
        connectionSocketPaths = newConnectionSocketPaths != null
            ? new List<string>(newConnectionSocketPaths)
            : new List<string>();
    }

    private void OnDrawGizmosSelected()
    {
        // note: The cyan footprint makes district overlap and connector alignment visible during authored review.
        Gizmos.color = new Color(0.1f, 0.9f, 0.9f, 0.7f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localBoundsCenter, localBoundsSize);
        Gizmos.matrix = previousMatrix;
    }
}
