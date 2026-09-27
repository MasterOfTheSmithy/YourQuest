using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQAssetRoleV2
{
    Unknown = 0,
    Surface = 1,
    Route = 2,
    GroundCover = 3,
    Understory = 4,
    Canopy = 5,
    NaturalFeature = 6,
    StructuralModule = 7,
    CompleteStructure = 8,
    Portal = 9,
    Dressing = 10,
    Container = 11,
    LightSource = 12,
    Landmark = 13,
    EncounterAssembly = 14,
    SiteAssembly = 15
}

public enum YQAssetFunctionV2
{
    None = 0,
    Habitation = 1,
    Commerce = 2,
    Manufacturing = 3,
    Security = 4,
    Civic = 5,
    CulturalFocus = 6,
    Circulation = 7,
    Infrastructure = 8,
    Service = 9,
    Storage = 10,
    Reward = 11,
    Encounter = 12,
    Transition = 13,
    Ecology = 14
}

public enum YQAssetEnvironmentV2
{
    Unspecified = 0,
    Exterior = 1,
    Interior = 2,
    Subterranean = 3,
    Aquatic = 4,
    Transitional = 5
}

public enum YQAssetAffordanceV2
{
    None = 0,
    TraversableSurface = 1,
    Openable = 2,
    Lockable = 3,
    Storage = 4,
    RewardAnchor = 5,
    Entrance = 6,
    Exit = 7,
    Seating = 8,
    Sleeping = 9,
    Workstation = 10,
    LightEmitter = 11,
    Cover = 12,
    Concealment = 13,
    Climbable = 14,
    SpawnAnchor = 15,
    QuestAnchor = 16,
    Destructible = 17
}

public enum YQAssetSupportModeV2
{
    Unspecified = 0,
    Ground = 1,
    Foundation = 2,
    Floor = 3,
    Wall = 4,
    Ceiling = 5,
    WaterSurface = 6,
    Suspended = 7,
    SocketOnly = 8,
    TerrainEmbedded = 9,
    FloatingAuthored = 10
}

public enum YQAssetSocketKindV2
{
    Unknown = 0,
    Connection = 1,
    Entrance = 2,
    Exit = 3,
    Portal = 4,
    Road = 5,
    Furnishing = 6,
    Dressing = 7,
    Interaction = 8,
    Spawn = 9,
    Support = 10,
    Concealment = 11,
    Cover = 12,
    StairTop = 13,
    StairBottom = 14
}

public enum YQTechnologyBandV2
{
    Unknown = 0,
    NotApplicable = 1,
    Manual = 2,
    Mechanical = 3,
    Electrified = 4,
    Networked = 5,
    Advanced = 6
}

public enum YQConstructionFamilyV2
{
    Unknown = 0,
    NotApplicable = 1,
    Earthen = 2,
    Timber = 3,
    Masonry = 4,
    Metal = 5,
    Concrete = 6,
    Glass = 7,
    Fabric = 8,
    Polymer = 9,
    Organic = 10,
    Composite = 11
}

public enum YQConditionBandV2
{
    Unknown = 0,
    New = 1,
    Maintained = 2,
    Weathered = 3,
    Damaged = 4,
    Ruined = 5,
    Overgrown = 6
}

public enum YQEcologyLayerV2
{
    None = 0,
    GroundCover = 1,
    Understory = 2,
    Canopy = 3,
    Wetland = 4,
    Cultivated = 5,
    Deadfall = 6,
    SpecialFlora = 7
}

public enum YQAssetConstraintFailureV2
{
    None = 0,
    MissingKit = 1,
    MissingAsset = 2,
    KitNotReleaseEligible = 3,
    AssetNotReleaseEligible = 4,
    IntakeDispositionNotCandidate = 5,
    MissingKitStyleContract = 6,
    MissingAssetCurationContract = 7,
    MissingSpatialReview = 8,
    InvalidBounds = 9,
    InvalidClearance = 10,
    InvalidFootprint = 11,
    InvalidCanonicalScale = 12,
    MissingFamily = 13,
    MissingRole = 14,
    MissingFunction = 15,
    MissingEnvironment = 16,
    MissingAffordance = 17,
    MissingSocket = 18,
    DuplicateSocketId = 19,
    InvalidSocket = 20,
    MissingSupportContract = 21,
    InvalidSupportPolygon = 22,
    InvalidEmbedRange = 23,
    SlopeExceeded = 24,
    IncompatibleTechnology = 25,
    IncompatibleConstruction = 26,
    ForbiddenKit = 27,
    FootprintTooLarge = 28,
    InvalidMaterial = 29,
    MissingCollider = 30,
    MissingNavigationProfile = 31,
    RepeatedVariant = 32,
    MissingEcologyProfile = 33,
    UnsupportedFloatingPlacement = 34,
    AssetKitMismatch = 35,
    InvalidStyleAxes = 36,
    InvalidAffordanceContract = 37,
    CompositionScaleMismatch = 38
}

[Serializable]
public sealed class YQAssetSocketRecordV2
{
    public string socketId = string.Empty;
    public YQAssetSocketKindV2 kind = YQAssetSocketKindV2.Unknown;
    public string transformPath = string.Empty;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 clearanceSize;
    public string compatibilityKey = string.Empty;
}

[Serializable]
public sealed class YQAssetEcologyContractV2
{
    public YQEcologyLayerV2 layer = YQEcologyLayerV2.None;
    public string speciesFamilyId = string.Empty;
    public float minimumMoisture;
    public float maximumMoisture = 1f;
    public float minimumNormalizedElevation;
    public float maximumNormalizedElevation = 1f;
    public float minimumSpacing = 1f;
    public int cohortMinimum = 1;
    public int cohortMaximum = 1;
    public float disturbanceTolerance = 0.5f;
}

[Serializable]
public sealed class YQAssetCurationContractV2
{
    public const int SupportedContractVersion = 2;

    // note: Version zero means the record is legacy evidence only; automated filename inference is never promoted into V2 authority.
    public int contractVersion;
    public YQAssetRoleV2 primaryRole = YQAssetRoleV2.Unknown;
    public YQAssetFunctionV2 primaryFunction = YQAssetFunctionV2.None;
    public List<YQAssetRoleV2> secondaryRoles =
        new List<YQAssetRoleV2>();
    public List<YQAssetFunctionV2> secondaryFunctions =
        new List<YQAssetFunctionV2>();
    public List<YQAssetEnvironmentV2> environments =
        new List<YQAssetEnvironmentV2>();
    public List<YQAssetAffordanceV2> affordances =
        new List<YQAssetAffordanceV2>();
    public string familyId = string.Empty;
    public string variantGroupId = string.Empty;
    public YQAssetSupportModeV2 supportMode =
        YQAssetSupportModeV2.Unspecified;
    public Vector3 canonicalScale = Vector3.one;
    public bool preserveAuthoredScale = true;
    public float supportLocalY;
    public List<Vector2> supportPolygon = new List<Vector2>();
    public float minimumEmbedDepth;
    public float maximumEmbedDepth;
    public float maximumSupportRelief;
    public int maxUsesPerSite;
    public float minimumRepeatDistance;
    public List<YQAssetSocketRecordV2> sockets =
        new List<YQAssetSocketRecordV2>();
    public YQAssetEcologyContractV2 ecology;

    public void EnsureCollections()
    {
        secondaryRoles ??= new List<YQAssetRoleV2>();
        secondaryFunctions ??= new List<YQAssetFunctionV2>();
        environments ??= new List<YQAssetEnvironmentV2>();
        affordances ??= new List<YQAssetAffordanceV2>();
        supportPolygon ??= new List<Vector2>();
        sockets ??= new List<YQAssetSocketRecordV2>();
    }

    public bool HasRole(YQAssetRoleV2 role)
    {
        return role == YQAssetRoleV2.Unknown ||
               primaryRole == role ||
               secondaryRoles.Contains(role);
    }

    public bool HasFunction(YQAssetFunctionV2 function)
    {
        return function == YQAssetFunctionV2.None ||
               primaryFunction == function ||
               secondaryFunctions.Contains(function);
    }

    public bool HasAffordance(YQAssetAffordanceV2 affordance)
    {
        return affordance == YQAssetAffordanceV2.None ||
               affordances.Contains(affordance);
    }

    public bool HasSocket(YQAssetSocketKindV2 kind)
    {
        if (kind == YQAssetSocketKindV2.Unknown)
            return true;

        // note: Socket queries use reviewed typed records rather than transform-name substring searches.
        for (int index = 0; index < sockets.Count; index++)
        {
            if (sockets[index] != null && sockets[index].kind == kind)
                return true;
        }

        return false;
    }
}

[Serializable]
public sealed class YQKitStyleContractV2
{
    public const int SupportedContractVersion = 2;

    // note: Orthogonal style axes allow one functional site plan to bind to manual, industrial, networked, or advanced visual families.
    public int contractVersion;
    public List<YQTechnologyBandV2> technologyBands =
        new List<YQTechnologyBandV2>();
    public List<YQConstructionFamilyV2> constructionFamilies =
        new List<YQConstructionFamilyV2>();
    public List<YQAssetEnvironmentV2> environments =
        new List<YQAssetEnvironmentV2>();
    public List<YQConditionBandV2> conditions =
        new List<YQConditionBandV2>();
    public List<string> climateTags = new List<string>();
    public List<string> shapeLanguageTags = new List<string>();

    public void EnsureCollections()
    {
        technologyBands ??= new List<YQTechnologyBandV2>();
        constructionFamilies ??= new List<YQConstructionFamilyV2>();
        environments ??= new List<YQAssetEnvironmentV2>();
        conditions ??= new List<YQConditionBandV2>();
        climateTags ??= new List<string>();
        shapeLanguageTags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQAssetPlacementContextV2
{
    public string worldSeed = string.Empty;
    public string ownerId = string.Empty;
    public string slotId = string.Empty;
    public string primaryKitId = string.Empty;
    public string preferredKitId = string.Empty;
    public YQAssetRoleV2 requiredRole = YQAssetRoleV2.Unknown;
    public YQAssetFunctionV2 requiredFunction = YQAssetFunctionV2.None;
    public YQAssetEnvironmentV2 requiredEnvironment =
        YQAssetEnvironmentV2.Unspecified;
    public YQTechnologyBandV2 technologyBand = YQTechnologyBandV2.Unknown;
    public List<YQConstructionFamilyV2> allowedConstructionFamilies =
        new List<YQConstructionFamilyV2>();
    public YQAssetSupportModeV2 requiredSupportMode =
        YQAssetSupportModeV2.Unspecified;
    public float terrainSlopeDegrees;
    public Vector2 maximumFootprint;
    public bool requireTerrainSupport;
    public bool requireEntrance;
    public bool requireCollider = true;
    public bool requireNavigation = true;
    public bool requireValidMaterials = true;
    public YQEcologyLayerV2 requiredEcologyLayer = YQEcologyLayerV2.None;
    public float moisture = 0.5f;
    public float normalizedElevation = 0.5f;
    public List<YQAssetAffordanceV2> requiredAffordances =
        new List<YQAssetAffordanceV2>();
    public List<YQAssetSocketKindV2> requiredSockets =
        new List<YQAssetSocketKindV2>();
    public List<string> allowedKitIds = new List<string>();
    public List<string> forbiddenKitIds = new List<string>();
    public List<string> excludedVariantGroupIds = new List<string>();

    public void EnsureCollections()
    {
        allowedConstructionFamilies ??=
            new List<YQConstructionFamilyV2>();
        requiredAffordances ??= new List<YQAssetAffordanceV2>();
        requiredSockets ??= new List<YQAssetSocketKindV2>();
        allowedKitIds ??= new List<string>();
        forbiddenKitIds ??= new List<string>();
        excludedVariantGroupIds ??= new List<string>();
    }
}

public sealed class YQAssetConstraintEvaluationV2
{
    public readonly List<YQAssetConstraintFailureV2> Failures =
        new List<YQAssetConstraintFailureV2>();

    public bool Accepted => Failures.Count == 0;

    public void Add(YQAssetConstraintFailureV2 failure)
    {
        if (failure != YQAssetConstraintFailureV2.None &&
            !Failures.Contains(failure))
        {
            Failures.Add(failure);
        }
    }
}

public sealed class YQAssetSelectionCandidateV2
{
    public YQAssetKitManifest kit;
    public YQSpatialAssetRecord asset;
}

public static class YQAssetConstraintEvaluatorV2
{
    public static YQAssetConstraintEvaluationV2 Evaluate(
        YQAssetKitManifest kit,
        YQSpatialAssetRecord asset,
        YQAssetPlacementContextV2 context)
    {
        YQAssetConstraintEvaluationV2 result =
            new YQAssetConstraintEvaluationV2();

        if (kit == null)
        {
            result.Add(YQAssetConstraintFailureV2.MissingKit);
            return result;
        }

        if (asset == null)
        {
            result.Add(YQAssetConstraintFailureV2.MissingAsset);
            return result;
        }

        kit.EnsureCollections();
        asset.EnsureCollections();
        context ??= new YQAssetPlacementContextV2();
        context.EnsureCollections();

        // note: Release and authored-review gates run before any semantic scoring, so raw discovered prefabs can never leak into V2.
        if (!kit.releaseEligible)
            result.Add(YQAssetConstraintFailureV2.KitNotReleaseEligible);
        if (!asset.releaseEligible)
            result.Add(YQAssetConstraintFailureV2.AssetNotReleaseEligible);
        if (asset.disposition != YQAssetIntakeDisposition.Candidate)
            result.Add(YQAssetConstraintFailureV2.IntakeDispositionNotCandidate);
        if (!asset.spatialMetadataAuthored)
            result.Add(YQAssetConstraintFailureV2.MissingSpatialReview);

        YQKitStyleContractV2 style = kit.styleV2;
        YQAssetCurationContractV2 curation = asset.curationV2;

        if (style == null ||
            style.contractVersion !=
            YQKitStyleContractV2.SupportedContractVersion)
        {
            result.Add(YQAssetConstraintFailureV2.MissingKitStyleContract);
        }
        else
        {
            style.EnsureCollections();

            // note: Reviewed V2 kits need explicit visual axes; an empty style contract would recreate filename-driven genre guessing.
            if (style.technologyBands.Count == 0 ||
                style.constructionFamilies.Count == 0)
            {
                result.Add(YQAssetConstraintFailureV2.InvalidStyleAxes);
            }
        }

        if (curation == null ||
            curation.contractVersion !=
            YQAssetCurationContractV2.SupportedContractVersion)
        {
            result.Add(YQAssetConstraintFailureV2.MissingAssetCurationContract);
            return result;
        }

        curation.EnsureCollections();

        if (!string.Equals(
                asset.kitId,
                kit.kitId,
                StringComparison.OrdinalIgnoreCase))
        {
            result.Add(YQAssetConstraintFailureV2.AssetKitMismatch);
        }

        ValidateMeasuredGeometry(asset, curation, result);
        ValidateSupportAndSockets(asset, curation, result);
        ValidateRuntimeSafety(asset, context, result);
        ValidateContext(kit, asset, style, curation, context, result);

        return result;
    }

    private static void ValidateMeasuredGeometry(
        YQSpatialAssetRecord asset,
        YQAssetCurationContractV2 curation,
        YQAssetConstraintEvaluationV2 result)
    {
        // note: V2 preserves reviewed scale and rejects incomplete geometry rather than resizing a house to fit an arbitrary lot.
        if (!IsPositiveFinite(asset.localBoundsSize))
            result.Add(YQAssetConstraintFailureV2.InvalidBounds);
        if (!IsPositiveFinite(asset.clearanceSize))
            result.Add(YQAssetConstraintFailureV2.InvalidClearance);
        if (!IsPositiveFinite(asset.footprintX) ||
            !IsPositiveFinite(asset.footprintZ))
        {
            result.Add(YQAssetConstraintFailureV2.InvalidFootprint);
        }
        if (!IsPositiveFinite(curation.canonicalScale))
            result.Add(YQAssetConstraintFailureV2.InvalidCanonicalScale);
        if (string.IsNullOrWhiteSpace(curation.familyId))
            result.Add(YQAssetConstraintFailureV2.MissingFamily);
        if (curation.primaryRole == YQAssetRoleV2.Unknown)
            result.Add(YQAssetConstraintFailureV2.MissingRole);
        if (curation.primaryFunction == YQAssetFunctionV2.None)
            result.Add(YQAssetConstraintFailureV2.MissingFunction);
    }

    private static void ValidateSupportAndSockets(
        YQSpatialAssetRecord asset,
        YQAssetCurationContractV2 curation,
        YQAssetConstraintEvaluationV2 result)
    {
        if (curation.minimumEmbedDepth > curation.maximumEmbedDepth ||
            !IsFinite(curation.minimumEmbedDepth) ||
            !IsFinite(curation.maximumEmbedDepth) ||
            !IsFinite(curation.maximumSupportRelief) ||
            curation.maximumSupportRelief < 0f)
        {
            result.Add(YQAssetConstraintFailureV2.InvalidEmbedRange);
        }

        if (RequiresSupportPolygon(curation.supportMode))
        {
            if (!HasFinitePolygon(curation.supportPolygon) ||
                curation.supportPolygon.Count < 3 ||
                Mathf.Abs(PolygonArea(curation.supportPolygon)) < 0.01f)
            {
                result.Add(YQAssetConstraintFailureV2.InvalidSupportPolygon);
            }

            if (string.IsNullOrWhiteSpace(asset.foundationProfile))
                result.Add(YQAssetConstraintFailureV2.MissingSupportContract);
        }
        else if (curation.supportMode == YQAssetSupportModeV2.Unspecified)
        {
            result.Add(YQAssetConstraintFailureV2.MissingSupportContract);
        }

        HashSet<string> socketIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // note: Typed sockets make entrances, rooms, roads, loot, and support explicit compiler contracts.
        for (int index = 0; index < curation.sockets.Count; index++)
        {
            YQAssetSocketRecordV2 socket = curation.sockets[index];

            if (socket == null ||
                string.IsNullOrWhiteSpace(socket.socketId) ||
                socket.kind == YQAssetSocketKindV2.Unknown ||
                !IsFinite(socket.localPosition) ||
                !IsFinite(socket.localRotation) ||
                (RequiresSocketClearance(socket.kind) &&
                 !IsPositiveFinite(socket.clearanceSize)))
            {
                result.Add(YQAssetConstraintFailureV2.InvalidSocket);
                continue;
            }

            if (!socketIds.Add(socket.socketId))
                result.Add(YQAssetConstraintFailureV2.DuplicateSocketId);
        }

        bool structure =
            curation.primaryRole == YQAssetRoleV2.CompleteStructure ||
            curation.primaryRole == YQAssetRoleV2.SiteAssembly ||
            curation.primaryRole == YQAssetRoleV2.StructuralModule;

        if (structure && !asset.frontDirectionAuthored)
            result.Add(YQAssetConstraintFailureV2.MissingSpatialReview);
        if (structure && !curation.preserveAuthoredScale)
            result.Add(YQAssetConstraintFailureV2.InvalidCanonicalScale);

        if (curation.primaryRole == YQAssetRoleV2.CompleteStructure &&
            asset.compositionScale != YQSpatialCompositionScale.CompleteBuilding)
        {
            result.Add(YQAssetConstraintFailureV2.CompositionScaleMismatch);
        }

        bool portalAffordance =
            curation.HasAffordance(YQAssetAffordanceV2.Openable) ||
            curation.HasAffordance(YQAssetAffordanceV2.Lockable);
        bool portalRole = curation.HasRole(YQAssetRoleV2.Portal);
        bool portalSocket =
            curation.HasSocket(YQAssetSocketKindV2.Portal) ||
            curation.HasSocket(YQAssetSocketKindV2.Interaction);

        if (portalAffordance && (!portalRole || !portalSocket))
            result.Add(YQAssetConstraintFailureV2.InvalidAffordanceContract);

        bool containerAffordance =
            curation.HasAffordance(YQAssetAffordanceV2.Storage) ||
            curation.HasAffordance(YQAssetAffordanceV2.RewardAnchor);

        if (containerAffordance &&
            (!curation.HasRole(YQAssetRoleV2.Container) ||
             !curation.HasSocket(YQAssetSocketKindV2.Interaction)))
        {
            result.Add(YQAssetConstraintFailureV2.InvalidAffordanceContract);
        }

        if (RequiresAttachmentSocket(curation.supportMode) &&
            !curation.HasSocket(YQAssetSocketKindV2.Support) &&
            !curation.HasSocket(YQAssetSocketKindV2.Connection))
        {
            result.Add(YQAssetConstraintFailureV2.MissingSocket);
        }
    }

    private static void ValidateRuntimeSafety(
        YQSpatialAssetRecord asset,
        YQAssetPlacementContextV2 context,
        YQAssetConstraintEvaluationV2 result)
    {
        if (context.requireValidMaterials &&
            (!asset.hasRenderer ||
             asset.rendererCount <= 0 ||
             asset.materialSlotCount <= 0 ||
             asset.invalidMaterialSlotCount > 0 ||
             asset.materialReviewSlotCount > 0))
        {
            result.Add(YQAssetConstraintFailureV2.InvalidMaterial);
        }

        if (context.requireCollider && !asset.hasCollider)
            result.Add(YQAssetConstraintFailureV2.MissingCollider);

        if (context.requireNavigation &&
            (string.IsNullOrWhiteSpace(asset.navigationProfile) ||
             string.Equals(
                 asset.navigationProfile,
                 "unassigned",
                 StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(YQAssetConstraintFailureV2.MissingNavigationProfile);
        }
    }

    private static void ValidateContext(
        YQAssetKitManifest kit,
        YQSpatialAssetRecord asset,
        YQKitStyleContractV2 style,
        YQAssetCurationContractV2 curation,
        YQAssetPlacementContextV2 context,
        YQAssetConstraintEvaluationV2 result)
    {
        if (!curation.HasRole(context.requiredRole))
            result.Add(YQAssetConstraintFailureV2.MissingRole);
        if (!curation.HasFunction(context.requiredFunction))
            result.Add(YQAssetConstraintFailureV2.MissingFunction);

        if (context.requiredEnvironment != YQAssetEnvironmentV2.Unspecified &&
            !curation.environments.Contains(context.requiredEnvironment))
        {
            result.Add(YQAssetConstraintFailureV2.MissingEnvironment);
        }

        for (int index = 0;
             index < context.requiredAffordances.Count;
             index++)
        {
            if (!curation.HasAffordance(context.requiredAffordances[index]))
                result.Add(YQAssetConstraintFailureV2.MissingAffordance);
        }

        for (int index = 0; index < context.requiredSockets.Count; index++)
        {
            if (!curation.HasSocket(context.requiredSockets[index]))
                result.Add(YQAssetConstraintFailureV2.MissingSocket);
        }

        if (context.requireEntrance &&
            !curation.HasSocket(YQAssetSocketKindV2.Entrance))
        {
            result.Add(YQAssetConstraintFailureV2.MissingSocket);
        }

        if (context.requiredSupportMode != YQAssetSupportModeV2.Unspecified &&
            curation.supportMode != context.requiredSupportMode)
        {
            result.Add(YQAssetConstraintFailureV2.MissingSupportContract);
        }

        if (context.requireTerrainSupport)
        {
            if (!RequiresSupportPolygon(curation.supportMode))
                result.Add(YQAssetConstraintFailureV2.UnsupportedFloatingPlacement);
            else if (curation.supportPolygon.Count < 3)
                result.Add(YQAssetConstraintFailureV2.InvalidSupportPolygon);
        }

        float allowedSlope = Mathf.Max(0f, asset.allowedSlopeDegrees);
        if (context.terrainSlopeDegrees > allowedSlope + 0.001f)
            result.Add(YQAssetConstraintFailureV2.SlopeExceeded);

        if (context.maximumFootprint.x > 0f &&
            context.maximumFootprint.y > 0f &&
            (asset.footprintX > context.maximumFootprint.x + 0.001f ||
             asset.footprintZ > context.maximumFootprint.y + 0.001f))
        {
            result.Add(YQAssetConstraintFailureV2.FootprintTooLarge);
        }

        if (context.allowedKitIds.Count > 0 &&
            !ContainsOrdinalIgnoreCase(context.allowedKitIds, kit.kitId))
        {
            result.Add(YQAssetConstraintFailureV2.ForbiddenKit);
        }

        if (ContainsOrdinalIgnoreCase(context.forbiddenKitIds, kit.kitId) ||
            (!string.IsNullOrWhiteSpace(context.primaryKitId) &&
             ContainsOrdinalIgnoreCase(
                 kit.forbiddenKitIds,
                 context.primaryKitId)))
        {
            result.Add(YQAssetConstraintFailureV2.ForbiddenKit);
        }

        if (!string.IsNullOrWhiteSpace(curation.variantGroupId) &&
            ContainsOrdinalIgnoreCase(
                context.excludedVariantGroupIds,
                curation.variantGroupId))
        {
            result.Add(YQAssetConstraintFailureV2.RepeatedVariant);
        }

        if (style != null &&
            style.contractVersion == YQKitStyleContractV2.SupportedContractVersion)
        {
            if (context.technologyBand != YQTechnologyBandV2.Unknown &&
                !style.technologyBands.Contains(context.technologyBand) &&
                !style.technologyBands.Contains(
                    YQTechnologyBandV2.NotApplicable))
            {
                result.Add(YQAssetConstraintFailureV2.IncompatibleTechnology);
            }

            if (context.allowedConstructionFamilies.Count > 0 &&
                !Intersects(
                    context.allowedConstructionFamilies,
                    style.constructionFamilies))
            {
                result.Add(YQAssetConstraintFailureV2.IncompatibleConstruction);
            }
        }

        if (context.requiredEcologyLayer != YQEcologyLayerV2.None)
        {
            if (curation.ecology == null ||
                curation.ecology.layer != context.requiredEcologyLayer ||
                context.moisture < curation.ecology.minimumMoisture ||
                context.moisture > curation.ecology.maximumMoisture ||
                context.normalizedElevation <
                    curation.ecology.minimumNormalizedElevation ||
                context.normalizedElevation >
                    curation.ecology.maximumNormalizedElevation)
            {
                result.Add(YQAssetConstraintFailureV2.MissingEcologyProfile);
            }
        }
    }

    private static bool RequiresSupportPolygon(YQAssetSupportModeV2 mode)
    {
        return mode == YQAssetSupportModeV2.Ground ||
               mode == YQAssetSupportModeV2.Foundation ||
               mode == YQAssetSupportModeV2.TerrainEmbedded;
    }

    private static bool RequiresAttachmentSocket(YQAssetSupportModeV2 mode)
    {
        return mode == YQAssetSupportModeV2.Wall ||
               mode == YQAssetSupportModeV2.Ceiling ||
               mode == YQAssetSupportModeV2.Suspended ||
               mode == YQAssetSupportModeV2.SocketOnly;
    }

    private static bool RequiresSocketClearance(YQAssetSocketKindV2 kind)
    {
        return kind == YQAssetSocketKindV2.Connection ||
               kind == YQAssetSocketKindV2.Entrance ||
               kind == YQAssetSocketKindV2.Exit ||
               kind == YQAssetSocketKindV2.Portal ||
               kind == YQAssetSocketKindV2.Road ||
               kind == YQAssetSocketKindV2.StairTop ||
               kind == YQAssetSocketKindV2.StairBottom;
    }

    private static bool HasFinitePolygon(IReadOnlyList<Vector2> points)
    {
        if (points == null)
            return false;

        // note: NaN support points must fail before the shoelace calculation, where NaN would otherwise bypass the area threshold.
        for (int index = 0; index < points.Count; index++)
        {
            if (!IsFinite(points[index]))
                return false;
        }

        return true;
    }

    private static float PolygonArea(IReadOnlyList<Vector2> points)
    {
        float doubledArea = 0f;

        // note: Signed shoelace area rejects collapsed support outlines before terrain fitting begins.
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % points.Count];
            doubledArea += current.x * next.y - next.x * current.y;
        }

        return doubledArea * 0.5f;
    }

    private static bool ContainsOrdinalIgnoreCase(
        IReadOnlyList<string> values,
        string expected)
    {
        if (values == null || string.IsNullOrWhiteSpace(expected))
            return false;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(
                    values[index],
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects<T>(
        IReadOnlyList<T> left,
        IReadOnlyList<T> right)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        for (int leftIndex = 0; leftIndex < left.Count; leftIndex++)
        {
            for (int rightIndex = 0; rightIndex < right.Count; rightIndex++)
            {
                if (comparer.Equals(left[leftIndex], right[rightIndex]))
                    return true;
            }
        }

        return false;
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsPositiveFinite(Vector3 value)
    {
        return IsPositiveFinite(value.x) &&
               IsPositiveFinite(value.y) &&
               IsPositiveFinite(value.z);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z) &&
               IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public static class YQDeterministicAssetSelectorV2
{
    public static bool TrySelect(
        IReadOnlyList<YQAssetSelectionCandidateV2> candidates,
        YQAssetPlacementContextV2 context,
        out YQSpatialAssetRecord selected,
        out string failure)
    {
        selected = null;
        failure = string.Empty;
        context ??= new YQAssetPlacementContextV2();
        context.EnsureCollections();

        int bestScore = int.MinValue;
        uint bestTie = uint.MaxValue;
        string bestStableId = string.Empty;
        int rejected = 0;
        YQAssetConstraintFailureV2 firstFailure =
            YQAssetConstraintFailureV2.None;

        if (candidates == null)
        {
            failure = "No V2 asset candidates were supplied.";
            return false;
        }

        // note: Hard constraints filter candidates first; deterministic scoring and stable-ID hashing make selection independent of catalog enumeration order.
        for (int index = 0; index < candidates.Count; index++)
        {
            YQAssetSelectionCandidateV2 candidate = candidates[index];
            YQAssetConstraintEvaluationV2 evaluation =
                YQAssetConstraintEvaluatorV2.Evaluate(
                    candidate != null ? candidate.kit : null,
                    candidate != null ? candidate.asset : null,
                    context);

            if (!evaluation.Accepted)
            {
                rejected++;
                if (firstFailure == YQAssetConstraintFailureV2.None &&
                    evaluation.Failures.Count > 0)
                {
                    firstFailure = evaluation.Failures[0];
                }

                continue;
            }

            YQSpatialAssetRecord asset = candidate.asset;
            int score = Score(candidate.kit, asset, context);
            string stableId = asset.stableAssetId ?? string.Empty;
            uint tie = StableHash(
                context.worldSeed + "|" +
                context.ownerId + "|" +
                context.slotId + "|" +
                stableId);

            if (selected == null ||
                score > bestScore ||
                (score == bestScore && tie < bestTie) ||
                (score == bestScore && tie == bestTie &&
                 string.CompareOrdinal(stableId, bestStableId) < 0))
            {
                selected = asset;
                bestScore = score;
                bestTie = tie;
                bestStableId = stableId;
            }
        }

        if (selected != null)
            return true;

        failure = "No reviewed V2 asset satisfied the placement context. " +
                  "Rejected=" + rejected +
                  (firstFailure == YQAssetConstraintFailureV2.None
                      ? string.Empty
                      : ", firstFailure=" + firstFailure) + ".";
        return false;
    }

    private static int Score(
        YQAssetKitManifest kit,
        YQSpatialAssetRecord asset,
        YQAssetPlacementContextV2 context)
    {
        int score = 0;
        YQAssetCurationContractV2 curation = asset.curationV2;

        if (curation.primaryRole == context.requiredRole)
            score += 1000;
        if (curation.primaryFunction == context.requiredFunction)
            score += 800;
        if (!string.IsNullOrWhiteSpace(context.preferredKitId) &&
            string.Equals(
                kit.kitId,
                context.preferredKitId,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 600;
        }
        if (kit.styleV2.technologyBands.Contains(context.technologyBand))
            score += 300;
        if (curation.environments.Contains(context.requiredEnvironment))
            score += 150;

        if (context.maximumFootprint.x > 0f &&
            context.maximumFootprint.y > 0f)
        {
            float usedArea = asset.footprintX * asset.footprintZ;
            float availableArea =
                context.maximumFootprint.x * context.maximumFootprint.y;
            score += Mathf.RoundToInt(
                Mathf.Clamp01(usedArea / availableArea) * 100f);
        }

        return score;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;

            // note: FNV-1a provides a stable cross-session tiebreaker without relying on registry order or runtime random state.
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }

            return hash;
        }
    }
}
