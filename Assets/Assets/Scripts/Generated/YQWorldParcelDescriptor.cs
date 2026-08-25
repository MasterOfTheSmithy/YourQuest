using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQWorldParcelDescriptor : MonoBehaviour
{
    [SerializeField]
    private YQParcelFunction parcelFunction =
        YQParcelFunction.Unknown;

    [SerializeField]
    private float frontageWidth;

    [SerializeField]
    private float parcelDepth;

    [SerializeField]
    private float frontSetback;

    [SerializeField]
    private float sideSetback;

    [SerializeField]
    private float rearSetback;

    [SerializeField]
    private float maximumSlopeDegrees = 12f;

    [SerializeField]
    private int residentCapacity;

    [SerializeField]
    private bool requiresServiceAccess;

    [SerializeField]
    private string frontageSocketPath = string.Empty;

    [SerializeField]
    private string entranceTargetPath = string.Empty;

    [SerializeField]
    private string serviceSocketPath = string.Empty;

    [SerializeField]
    private List<string> sourceAssemblyIds =
        new List<string>();

    public YQParcelFunction ParcelFunction => parcelFunction;
    public float FrontageWidth => frontageWidth;
    public float ParcelDepth => parcelDepth;
    public float FrontSetback => frontSetback;
    public float SideSetback => sideSetback;
    public float RearSetback => rearSetback;
    public float MaximumSlopeDegrees => maximumSlopeDegrees;
    public int ResidentCapacity => residentCapacity;
    public bool RequiresServiceAccess => requiresServiceAccess;
    public string FrontageSocketPath => frontageSocketPath;
    public string EntranceTargetPath => entranceTargetPath;
    public string ServiceSocketPath => serviceSocketPath;
    public IReadOnlyList<string> SourceAssemblyIds => sourceAssemblyIds;

    public void Configure(
        YQParcelFunction newParcelFunction,
        float newFrontageWidth,
        float newParcelDepth,
        float newFrontSetback,
        float newSideSetback,
        float newRearSetback,
        float newMaximumSlopeDegrees,
        int newResidentCapacity,
        bool newRequiresServiceAccess,
        string newFrontageSocketPath,
        string newEntranceTargetPath,
        string newServiceSocketPath,
        IEnumerable<string> newSourceAssemblyIds)
    {
        // note: Parcel contracts are deterministic spatial authority; generated narrative may request a function but cannot bypass these measured limits.
        parcelFunction = newParcelFunction;
        frontageWidth = Mathf.Max(1f, newFrontageWidth);
        parcelDepth = Mathf.Max(1f, newParcelDepth);
        frontSetback = Mathf.Max(0f, newFrontSetback);
        sideSetback = Mathf.Max(0f, newSideSetback);
        rearSetback = Mathf.Max(0f, newRearSetback);
        maximumSlopeDegrees = Mathf.Clamp(newMaximumSlopeDegrees, 0f, 45f);
        residentCapacity = Mathf.Max(0, newResidentCapacity);
        requiresServiceAccess = newRequiresServiceAccess;
        frontageSocketPath = newFrontageSocketPath ?? string.Empty;
        entranceTargetPath = newEntranceTargetPath ?? string.Empty;
        serviceSocketPath = newServiceSocketPath ?? string.Empty;
        sourceAssemblyIds = newSourceAssemblyIds != null
            ? new List<string>(newSourceAssemblyIds)
            : new List<string>();
    }

    private void OnDrawGizmos()
    {
        if (frontageWidth <= 0f || parcelDepth <= 0f)
            return;

        // note: The editor wire volume makes overlap, frontage, and setback mistakes visible during human review without adding runtime geometry.
        Matrix4x4 priorMatrix = Gizmos.matrix;
        Color priorColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.15f, 0.85f, 0.65f, 0.8f);
        Gizmos.DrawWireCube(
            new Vector3(0f, 0.1f, parcelDepth * 0.5f),
            new Vector3(frontageWidth, 0.2f, parcelDepth));
        Gizmos.matrix = priorMatrix;
        Gizmos.color = priorColor;
    }
}
