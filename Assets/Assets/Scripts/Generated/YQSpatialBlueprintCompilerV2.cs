using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class YQSpatialBlueprintCompilerV2
{
    // note: The accepted beta contract now reserves a larger deterministic continuation envelope so normal travel encounters authored geography beyond the 1024m origin instead of exhausting into ecology-only tiles.
    // note: The accepted beta contract covers the previously observed roughly 5 km streamed coordinates while physical residency remains bounded by the streamer.
    private const float DefaultWorldSize = 16384f;

    public static bool TryCompile(
        GeneratedWorldPlanRecord plan,
        out GeneratedSpatialWorldPlanV2Record compiled,
        out string failure)
    {
        if (!TryCreateContext(plan, out compiled,
                out YQSpatialBlueprintBuildContextV2 context, out failure))
        {
            return false;
        }

        // note: Terrain causality is fixed before sites or props: domains, geological fields, then downhill drainage.
        BuildRegionDomains(context);
        BuildGeologicalFields(context);
        BuildHydrology(context);
        YQSpatialSiteNetworkCompilerV2.Build(context);
        SortCanonical(compiled.blueprint);
        return FinalizeCompilation(compiled, out failure);
    }

    public static IEnumerator CompileRoutine(
        GeneratedWorldPlanRecord plan,
        Action<GeneratedSpatialWorldPlanV2Record, string> completed)
    {
        if (!TryCreateContext(plan, out GeneratedSpatialWorldPlanV2Record compiled,
                out YQSpatialBlueprintBuildContextV2 context,
                out string failure))
        {
            completed?.Invoke(null, failure);
            yield break;
        }

        // note: Each causal world-planning stage yields before the next one so shadow compilation cannot monopolize a frame.
        if (!TryRunPhase(() => BuildRegionDomains(context),
                "region domain compilation", out failure))
        {
            completed?.Invoke(compiled, failure);
            yield break;
        }
        yield return null;

        if (!TryRunPhase(() => BuildGeologicalFields(context),
                "geological field compilation", out failure))
        {
            completed?.Invoke(compiled, failure);
            yield break;
        }
        yield return null;

        if (!TryRunPhase(() => BuildHydrology(context),
                "hydrology compilation", out failure))
        {
            completed?.Invoke(compiled, failure);
            yield break;
        }
        yield return null;

        IEnumerator siteNetwork =
            YQSpatialSiteNetworkCompilerV2.BuildRoutine(context);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = siteNetwork.MoveNext();
            }
            catch (Exception exception)
            {
                completed?.Invoke(
                    compiled,
                    "V2 site-network compilation failed: " +
                    exception.Message);
                yield break;
            }

            if (!hasNext)
            {
                break;
            }

            yield return siteNetwork.Current;
        }

        if (!TryRunPhase(() => SortCanonical(compiled.blueprint),
                "canonical blueprint sorting", out failure))
        {
            completed?.Invoke(compiled, failure);
            yield break;
        }
        yield return null;

        bool accepted = FinalizeCompilation(compiled, out failure);
        completed?.Invoke(compiled, accepted ? string.Empty : failure);
    }

    private static bool TryCreateContext(
        GeneratedWorldPlanRecord plan,
        out GeneratedSpatialWorldPlanV2Record compiled,
        out YQSpatialBlueprintBuildContextV2 context,
        out string failure)
    {
        compiled = null;
        context = null;
        failure = string.Empty;

        if (plan == null)
        {
            failure = "The semantic world plan is missing.";
            return false;
        }

        plan.EnsureCollections();
        string seed = (plan.worldSeed ?? string.Empty).Trim();
        string semanticFingerprint =
            plan.spatialPlan != null
                ? (plan.spatialPlan.semanticFingerprint ?? string.Empty).Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(seed))
        {
            failure = "The semantic world plan has no stable seed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(semanticFingerprint))
        {
            failure = "The accepted semantic fingerprint is missing.";
            return false;
        }

        compiled = new GeneratedSpatialWorldPlanV2Record
        {
            schemaVersion =
                GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
            generationVersion =
                GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
            validationVersion =
                GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
            worldSeed = seed,
            semanticFingerprint = semanticFingerprint,
            acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft,
            blueprint = new YQSpatialBlueprintV2
            {
                worldSize = ResolveWorldSize(plan),
                originSiteId = "site:origin"
            }
        };
        compiled.EnsureCollections();
        context = new YQSpatialBlueprintBuildContextV2(plan, compiled, seed);
        return true;
    }

    private static bool FinalizeCompilation(
        GeneratedSpatialWorldPlanV2Record compiled,
        out string failure)
    {
        failure = string.Empty;

        YQSpatialBlueprintValidationResultV2 validation =
            YQSpatialBlueprintValidatorV2.Validate(compiled);
        compiled.validationErrors.Clear();
        compiled.validationErrors.AddRange(validation.Errors);
        // note: A rejected draft must never retain evidence that a previous result passed acceptance.
        compiled.validatedContentHash = string.Empty;
        compiled.contentHash =
            YQSpatialBlueprintHasherV2.ComputeContentHash(compiled);

        if (!validation.Accepted)
        {
            compiled.acceptanceState =
                GeneratedSpatialPlanAcceptanceState.Rejected;
            failure = "The V2 blueprint failed validation: " +
                      validation.Errors[0];
            return false;
        }

        compiled.validatedContentHash = compiled.contentHash;
        compiled.acceptanceState =
            GeneratedSpatialPlanAcceptanceState.Accepted;
        return true;
    }

    private static bool TryRunPhase(
        Action phase,
        string phaseName,
        out string failure)
    {
        failure = string.Empty;
        try
        {
            phase();
            return true;
        }
        catch (Exception exception)
        {
            failure = "V2 " + phaseName + " failed: " + exception.Message;
            return false;
        }
    }

    public static bool TryCompileShadow(
        GeneratedWorldPlanRecord plan,
        out string failure)
    {
        failure = string.Empty;
        if (!YQWorldGenerationArchitecture.RunsV2Shadow)
        {
            failure = "V2 shadow planning is not enabled.";
            return false;
        }

        // note: Accepted content is persisted authority, even if new checks find it needs repair. Shadow compilation cannot silently replace it.
        if (plan?.spatialPlanV2?.acceptanceState == GeneratedSpatialPlanAcceptanceState.Accepted)
            return YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out failure);

        if (!TryCompile(plan, out GeneratedSpatialWorldPlanV2Record compiled,
                out failure))
        {
            return false;
        }

        // note: A shadow artifact is attached only after full validation; V1 remains the runtime authority in shadow mode.
        plan.spatialPlanV2 = compiled;
        return true;
    }

    private static float ResolveWorldSize(GeneratedWorldPlanRecord plan)
    {
        float existing = plan.spatialPlan != null
            ? plan.spatialPlan.worldSize
            : 0f;
        return Mathf.Clamp(
            Mathf.Max(existing >= 512f ? existing : DefaultWorldSize, DefaultWorldSize),
            DefaultWorldSize,
            16384f);
    }

    private static void BuildRegionDomains(
        YQSpatialBlueprintBuildContextV2 context)
    {
        List<GeneratedRegionRecord> semanticRegions =
            YQSpatialBlueprintDeterminismV2.SortedRecords(
                context.plan.regions,
                region => region != null ? region.regionId : string.Empty);

        if (semanticRegions.Count == 0)
        {
            semanticRegions.Add(new GeneratedRegionRecord
            {
                regionId = "region:world",
                displayName = "World",
                deterministicSeed = context.seed
            });
        }

        int minimumGridX = int.MaxValue;
        int maximumGridX = int.MinValue;
        int minimumGridY = int.MaxValue;
        int maximumGridY = int.MinValue;

        for (int index = 0; index < semanticRegions.Count; index++)
        {
            GeneratedRegionRecord region = semanticRegions[index];
            minimumGridX = Mathf.Min(minimumGridX, region.gridX);
            maximumGridX = Mathf.Max(maximumGridX, region.gridX);
            minimumGridY = Mathf.Min(minimumGridY, region.gridY);
            maximumGridY = Mathf.Max(maximumGridY, region.gridY);
        }

        bool hasGridSpread = minimumGridX != maximumGridX ||
                             minimumGridY != maximumGridY;
        float usableHalfExtent = context.UsableHalfExtent;
        float domainRadius = Mathf.Clamp(
            context.blueprint.worldSize /
            (Mathf.Sqrt(semanticRegions.Count) * 2.45f),
            118f,
            250f);

        for (int index = 0; index < semanticRegions.Count; index++)
        {
            GeneratedRegionRecord semantic = semanticRegions[index];
            string regionId = YQSpatialBlueprintDeterminismV2.SafeId(
                semantic.regionId,
                "region:" + index);
            Vector2 center = hasGridSpread
                ? GridCenter(
                    semantic,
                    minimumGridX,
                    maximumGridX,
                    minimumGridY,
                    maximumGridY,
                    usableHalfExtent)
                : RingCenter(
                    context.seed,
                    regionId,
                    index,
                    semanticRegions.Count,
                    usableHalfExtent);

            float moisture = ResolveMoisture(semantic, context.seed);
            float ruggedness = ResolveRuggedness(semantic, context.seed);
            int settlementCount = CountRegionEntries(
                context.plan.settlements,
                regionId,
                settlement => settlement != null
                    ? settlement.regionId
                    : string.Empty);
            float civilization = Mathf.Clamp01(
                0.18f + settlementCount * 0.2f +
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|civilization|" + regionId) * 0.18f);
            YQRegionDomainV2 domain = new YQRegionDomainV2
            {
                regionId = regionId,
                centerX = center.x,
                centerZ = center.y,
                radius = domainRadius,
                elevationBias = Mathf.Lerp(
                    -0.12f,
                    0.22f,
                    YQSpatialBlueprintDeterminismV2.Hash01(
                        context.seed + "|elevation|" + regionId)),
                moisture = moisture,
                ruggedness = ruggedness,
                civilizationDensity = civilization,
                danger = Mathf.Clamp01(semantic.dangerTier / 10f)
            };
            domain.EnsureCollections();
            CopyStableTags(semantic.biomeTags, domain.biomeTags);
            context.blueprint.regions.Add(domain);
            context.regionsById[regionId] = domain;
        }
    }

    private static Vector2 GridCenter(
        GeneratedRegionRecord region,
        int minimumGridX,
        int maximumGridX,
        int minimumGridY,
        int maximumGridY,
        float usableHalfExtent)
    {
        float normalizedX = maximumGridX == minimumGridX
            ? 0f
            : Mathf.Lerp(
                -0.7f,
                0.7f,
                (region.gridX - minimumGridX) /
                (float)(maximumGridX - minimumGridX));
        float normalizedZ = maximumGridY == minimumGridY
            ? 0f
            : Mathf.Lerp(
                -0.7f,
                0.7f,
                (region.gridY - minimumGridY) /
                (float)(maximumGridY - minimumGridY));
        return new Vector2(
            normalizedX * usableHalfExtent,
            normalizedZ * usableHalfExtent);
    }

    private static Vector2 RingCenter(
        string seed,
        string regionId,
        int index,
        int count,
        float usableHalfExtent)
    {
        if (count <= 1)
            return Vector2.zero;

        float phase = YQSpatialBlueprintDeterminismV2.Hash01(
            seed + "|region_phase") * Mathf.PI * 2f;
        float angle = phase + index * Mathf.PI * 2f / count;
        float radius = usableHalfExtent *
                       Mathf.Lerp(
                           0.42f,
                           0.62f,
                           YQSpatialBlueprintDeterminismV2.Hash01(
                               seed + "|region_radius|" + regionId));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static void BuildGeologicalFields(
        YQSpatialBlueprintBuildContextV2 context)
    {
        float extent = context.UsableHalfExtent;
        float ridgeAngle = Mathf.Lerp(
            18f,
            162f,
            YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|ridge_heading"));
        Vector2 ridgeAxis = YQSpatialBlueprintDeterminismV2.Direction(
            ridgeAngle);
        Vector2 valleyAxis = new Vector2(-ridgeAxis.y, ridgeAxis.x);

        YQTerrainFieldV2 baseSurface = new YQTerrainFieldV2
        {
            fieldId = "terrain:base",
            kind = YQTerrainFieldKindV2.BaseSurface,
            strength = 0.36f,
            radius = context.blueprint.worldSize * 0.5f,
            falloff = 1f
        };
        baseSurface.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.Point(
                Vector2.zero,
                0.36f,
                context.blueprint.worldSize));
        baseSurface.tags.Add("continuous_surface");
        context.blueprint.terrainFields.Add(baseSurface);

        YQTerrainFieldV2 ridge = new YQTerrainFieldV2
        {
            fieldId = "terrain:ridge:primary",
            kind = YQTerrainFieldKindV2.RidgeChain,
            strength = 0.86f,
            radius = 74f,
            falloff = 2.4f
        };
        for (int index = 0; index < 7; index++)
        {
            float t = index / 6f;
            float signed = t * 2f - 1f;
            float wobble = Mathf.Sin((t * 2.3f +
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|ridge_wobble")) * Mathf.PI) * 34f;
            Vector2 position = ridgeAxis * (signed * extent) +
                               valleyAxis * wobble;
            ridge.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    position,
                    0.72f + Mathf.Sin(t * Mathf.PI) * 0.2f,
                    Mathf.Lerp(92f, 58f, Mathf.Sin(t * Mathf.PI))));
        }
        ridge.tags.Add("macro_silhouette");
        ridge.tags.Add("cave_bearing_mass");
        context.blueprint.terrainFields.Add(ridge);

        YQTerrainFieldV2 valley = new YQTerrainFieldV2
        {
            fieldId = "terrain:valley:primary",
            kind = YQTerrainFieldKindV2.ValleyCorridor,
            strength = -0.48f,
            radius = 82f,
            falloff = 2.1f
        };
        for (int index = 0; index < 7; index++)
        {
            float t = index / 6f;
            Vector2 position = valleyAxis * Mathf.Lerp(-extent, extent, t) +
                               ridgeAxis * Mathf.Sin(t * Mathf.PI * 2f) * 22f;
            valley.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    position,
                    Mathf.Lerp(0.62f, 0.2f, t),
                    86f));
        }
        valley.tags.Add("drainage_corridor");
        valley.tags.Add("preferred_traversal_band");
        context.blueprint.terrainFields.Add(valley);

        Vector2 basinPosition =
            new Vector2(
                valley.controlPoints[valley.controlPoints.Count - 1].x,
                valley.controlPoints[valley.controlPoints.Count - 1].z);
        YQTerrainFieldV2 basin = new YQTerrainFieldV2
        {
            fieldId = "terrain:basin:primary",
            kind = YQTerrainFieldKindV2.Basin,
            strength = -0.56f,
            radius = 72f,
            falloff = 2.6f
        };
        basin.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.Point(
                basinPosition,
                0.18f,
                118f));
        basin.tags.Add("water_basin");
        context.blueprint.terrainFields.Add(basin);

        for (int index = 0; index < context.blueprint.regions.Count; index++)
        {
            YQRegionDomainV2 region = context.blueprint.regions[index];
            AddRegionalRelief(context, region);
        }
    }

    private static void AddRegionalRelief(
        YQSpatialBlueprintBuildContextV2 context,
        YQRegionDomainV2 region)
    {
        YQTerrainFieldV2 hills = new YQTerrainFieldV2
        {
            fieldId = "terrain:hills:" + region.regionId,
            parentRegionId = region.regionId,
            kind = YQTerrainFieldKindV2.HillCluster,
            strength = Mathf.Lerp(0.14f, 0.42f, region.ruggedness),
            radius = Mathf.Lerp(34f, 62f, region.ruggedness),
            falloff = 2.2f
        };

        for (int index = 0; index < 3; index++)
        {
            float angle = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|hill_angle|" + region.regionId + "|" + index) *
                360f;
            float radius = region.radius * Mathf.Lerp(
                0.22f,
                0.68f,
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|hill_radius|" + region.regionId + "|" + index));
            Vector2 position = new Vector2(region.centerX, region.centerZ) +
                               YQSpatialBlueprintDeterminismV2.Direction(angle) *
                               radius;
            hills.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    context.Clamp(position),
                    Mathf.Clamp01(0.42f + region.elevationBias +
                                  region.ruggedness * 0.24f),
                    hills.radius));
        }

        hills.tags.Add("regional_relief");
        context.blueprint.terrainFields.Add(hills);

        if (region.ruggedness < 0.56f)
            return;

        YQTerrainFieldV2 escarpment = new YQTerrainFieldV2
        {
            fieldId = "terrain:escarpment:" + region.regionId,
            parentRegionId = region.regionId,
            kind = YQTerrainFieldKindV2.Escarpment,
            strength = 0.36f + region.ruggedness * 0.24f,
            radius = 38f,
            falloff = 3.4f
        };
        float heading = YQSpatialBlueprintDeterminismV2.Hash01(
            context.seed + "|escarpment|" + region.regionId) * 180f;
        Vector2 axis = YQSpatialBlueprintDeterminismV2.Direction(heading);
        Vector2 center = new Vector2(region.centerX, region.centerZ);
        escarpment.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.Point(
                context.Clamp(center - axis * region.radius * 0.5f),
                0.64f,
                44f));
        escarpment.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.Point(
                context.Clamp(center + axis * region.radius * 0.5f),
                0.7f,
                44f));
        escarpment.tags.Add("rock_outcrop_band");
        context.blueprint.terrainFields.Add(escarpment);
    }

    private static void BuildHydrology(
        YQSpatialBlueprintBuildContextV2 context)
    {
        YQTerrainFieldV2 valley = FindTerrain(
            context.blueprint,
            "terrain:valley:primary");
        YQTerrainFieldV2 basin = FindTerrain(
            context.blueprint,
            "terrain:basin:primary");

        YQHydrologyFeatureV2 lake = new YQHydrologyFeatureV2
        {
            hydrologyId = "water:lake:primary",
            kind = YQHydrologyKindV2.Lake,
            sourceTerrainFieldId = basin.fieldId,
            waterLevelNormalized = 0.2f,
            nominalWidth = 112f,
            nominalDepth = 12f
        };
        lake.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.CopyPoint(
                basin.controlPoints[0],
                0.2f,
                112f));
        lake.tags.Add("persistent_water_body");
        context.blueprint.hydrology.Add(lake);

        YQHydrologyFeatureV2 river = new YQHydrologyFeatureV2
        {
            hydrologyId = "water:river:primary",
            kind = YQHydrologyKindV2.River,
            sourceTerrainFieldId = "terrain:ridge:primary",
            sinkHydrologyId = lake.hydrologyId,
            waterLevelNormalized = 0.26f,
            nominalWidth = 8f,
            nominalDepth = 2.4f
        };
        for (int index = 0; index < valley.controlPoints.Count; index++)
        {
            float t = index / (float)(valley.controlPoints.Count - 1);
            Vector2 position = new Vector2(
                valley.controlPoints[index].x,
                valley.controlPoints[index].z);
            // note: Add a bounded, seed-stable lateral meander so the river follows the valley without reading as a ruler-straight cut.
            Vector2 before = new Vector2(
                valley.controlPoints[Mathf.Max(0, index - 1)].x,
                valley.controlPoints[Mathf.Max(0, index - 1)].z);
            Vector2 after = new Vector2(
                valley.controlPoints[Mathf.Min(valley.controlPoints.Count - 1, index + 1)].x,
                valley.controlPoints[Mathf.Min(valley.controlPoints.Count - 1, index + 1)].z);
            Vector2 tangent = after - before;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector2.up;
            tangent.Normalize();
            Vector2 bankNormal = new Vector2(-tangent.y, tangent.x);
            float meanderPhase = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|river_meander_phase") * Mathf.PI * 2f;
            float meander = Mathf.Sin(t * Mathf.PI * 2.1f + meanderPhase) *
                            Mathf.Sin(t * Mathf.PI) * 12f;
            position += bankNormal * meander;
            float widthJitter = Mathf.Lerp(.91f, 1.09f,
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|river_width|" + index));
            river.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    position,
                    Mathf.Lerp(0.66f, 0.2f, t),
                    Mathf.Clamp(Mathf.Lerp(5.5f, 10.5f, t) * widthJitter, 5f, 12f)));
        }
        river.tags.Add("downhill_drainage");
        river.tags.Add("route_crossing_constraint");
        context.blueprint.hydrology.Add(river);

        AddBoundaryRiver(context);

        int waterfallIndex = Mathf.Clamp(
            1 + Mathf.FloorToInt(
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|waterfall_index") * 3f),
            1,
            river.controlPoints.Count - 2);
        YQBlueprintPointV2 waterfallTop = river.controlPoints[waterfallIndex];
        YQBlueprintPointV2 waterfallBottom =
            river.controlPoints[waterfallIndex + 1];
        YQHydrologyFeatureV2 waterfall = new YQHydrologyFeatureV2
        {
            hydrologyId = "water:waterfall:primary",
            kind = YQHydrologyKindV2.Waterfall,
            sourceTerrainFieldId = "terrain:ridge:primary",
            sinkHydrologyId = river.hydrologyId,
            waterLevelNormalized = waterfallBottom.normalizedElevation,
            nominalWidth = Mathf.Max(4f, waterfallTop.width),
            nominalDepth = 2f
        };
        waterfall.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.CopyPoint(
                waterfallTop,
                Mathf.Max(
                    waterfallTop.normalizedElevation,
                    waterfallBottom.normalizedElevation + 0.12f),
                waterfallTop.width));
        waterfall.controlPoints.Add(
            YQSpatialBlueprintDeterminismV2.CopyPoint(
                waterfallBottom,
                waterfallBottom.normalizedElevation,
                waterfallBottom.width));
        waterfall.tags.Add("vertical_water_transition");
        context.blueprint.hydrology.Add(waterfall);

        context.AddRelationship(
            "relationship:lake_fed_by_river",
            lake.hydrologyId,
            river.hydrologyId,
            YQSpatialRelationshipKindV2.FedBy,
            0f,
            context.blueprint.worldSize,
            "hydrology_chain");
        context.AddRelationship(
            "relationship:river_fed_by_waterfall",
            river.hydrologyId,
            waterfall.hydrologyId,
            YQSpatialRelationshipKindV2.FedBy,
            0f,
            context.blueprint.worldSize,
            "hydrology_chain");
    }

    private static void AddBoundaryRiver(
        YQSpatialBlueprintBuildContextV2 context)
    {
        // note: A free-flowing accepted channel reaches the same boundary terminals as continuation roads, so the first streamed cells inherit real hydrology instead of an empty semantic promise.
        float boundary = Mathf.Min(
            508f,
            context.blueprint.worldSize * 0.5f - 4f);
        if (boundary <= 0f)
            return;

        YQHydrologyFeatureV2 continuation = new YQHydrologyFeatureV2
        {
            hydrologyId = "water:river:streamed_continuation",
            kind = YQHydrologyKindV2.River,
            sourceTerrainFieldId = "terrain:base",
            sinkHydrologyId = string.Empty,
            waterLevelNormalized = 0.23f,
            nominalWidth = 10f,
            nominalDepth = 2.8f
        };
        float[] zValues = { boundary, 250f, 0f, -250f, -boundary };
        for (int index = 0; index < zValues.Length; index++)
        {
            float t = index / (float)(zValues.Length - 1);
            float z = zValues[index];
            float x = 150f + Mathf.Sin(t * Mathf.PI * 2.2f +
                YQSpatialBlueprintDeterminismV2.Hash01(
                    context.seed + "|streamed_river_meander") * Mathf.PI * 2f) * 34f;
            continuation.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    new Vector2(x, z),
                    Mathf.Lerp(0.56f, 0.16f, t),
                    Mathf.Lerp(8f, 12f, t)));
        }
        continuation.tags.Add("accepted_streamed_continuation");
        continuation.tags.Add("free_flowing_river");
        context.blueprint.hydrology.Add(continuation);
    }

    private static YQTerrainFieldV2 FindTerrain(
        YQSpatialBlueprintV2 blueprint,
        string fieldId)
    {
        for (int index = 0; index < blueprint.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 field = blueprint.terrainFields[index];
            if (field != null && string.Equals(
                    field.fieldId,
                    fieldId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }

    private static float ResolveMoisture(
        GeneratedRegionRecord region,
        string seed)
    {
        string text = ((region.climateProfile ?? string.Empty) + " " +
                       string.Join(" ", region.biomeTags ?? new List<string>()))
            .ToLowerInvariant();
        if (ContainsAny(text, "wet", "rain", "marsh", "swamp", "coast"))
            return 0.78f;
        if (ContainsAny(text, "dry", "arid", "desert", "waste"))
            return 0.18f;
        return Mathf.Lerp(
            0.32f,
            0.68f,
            YQSpatialBlueprintDeterminismV2.Hash01(
                seed + "|moisture|" + region.regionId));
    }

    private static float ResolveRuggedness(
        GeneratedRegionRecord region,
        string seed)
    {
        string text = ((region.terrainProfile ?? string.Empty) + " " +
                       (region.traversalHook ?? string.Empty))
            .ToLowerInvariant();
        if (ContainsAny(text, "mountain", "cliff", "ridge", "canyon", "crag"))
            return 0.82f;
        if (ContainsAny(text, "plain", "flat", "meadow", "farmland"))
            return 0.28f;
        return Mathf.Lerp(
            0.38f,
            0.72f,
            YQSpatialBlueprintDeterminismV2.Hash01(
                seed + "|ruggedness|" + region.regionId));
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            if (value.Contains(tokens[index]))
                return true;
        }

        return false;
    }

    private static int CountRegionEntries<T>(
        IReadOnlyList<T> values,
        string regionId,
        Func<T, string> regionSelector)
        where T : class
    {
        int count = 0;
        if (values == null)
            return count;

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] != null && string.Equals(
                    regionSelector(values[index]),
                    regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static void CopyStableTags(
        IReadOnlyList<string> source,
        List<string> destination)
    {
        if (source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            string value = (source[index] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(value) &&
                !destination.Contains(value))
            {
                destination.Add(value);
            }
        }

        destination.Sort(StringComparer.Ordinal);
    }

    private static void SortCanonical(YQSpatialBlueprintV2 blueprint)
    {
        // note: Persisted collection order is canonical, so equivalent semantic inputs produce byte-stable plan hashes.
        blueprint.regions.Sort(
            (left, right) => string.CompareOrdinal(left.regionId, right.regionId));
        blueprint.terrainFields.Sort(
            (left, right) => string.CompareOrdinal(left.fieldId, right.fieldId));
        blueprint.hydrology.Sort(
            (left, right) => string.CompareOrdinal(
                left.hydrologyId,
                right.hydrologyId));
        blueprint.sites.Sort(
            (left, right) => string.CompareOrdinal(left.siteId, right.siteId));
        blueprint.routes.Sort(
            (left, right) => string.CompareOrdinal(left.routeId, right.routeId));
        blueprint.relationships.Sort(
            (left, right) => string.CompareOrdinal(
                left.relationshipId,
                right.relationshipId));
    }
}

internal sealed class YQSpatialBlueprintBuildContextV2
{
    public readonly GeneratedWorldPlanRecord plan;
    public readonly GeneratedSpatialWorldPlanV2Record record;
    public readonly YQSpatialBlueprintV2 blueprint;
    public readonly string seed;
    public readonly Dictionary<string, YQRegionDomainV2> regionsById =
        new Dictionary<string, YQRegionDomainV2>(
            StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, YQSiteAnchorV2> sitesById =
        new Dictionary<string, YQSiteAnchorV2>(
            StringComparer.OrdinalIgnoreCase);

    public float UsableHalfExtent => blueprint.worldSize * 0.43f;

    public YQSpatialBlueprintBuildContextV2(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanV2Record record,
        string seed)
    {
        this.plan = plan;
        this.record = record;
        blueprint = record.blueprint;
        this.seed = seed;
    }

    public Vector2 Clamp(Vector2 point)
    {
        float extent = UsableHalfExtent;
        return new Vector2(
            Mathf.Clamp(point.x, -extent, extent),
            Mathf.Clamp(point.y, -extent, extent));
    }

    public YQRegionDomainV2 ResolveRegion(string requestedRegionId)
    {
        if (!string.IsNullOrWhiteSpace(requestedRegionId) &&
            regionsById.TryGetValue(
                requestedRegionId,
                out YQRegionDomainV2 requested))
        {
            return requested;
        }

        return blueprint.regions.Count > 0 ? blueprint.regions[0] : null;
    }

    public void AddRelationship(
        string relationshipId,
        string subjectId,
        string objectId,
        YQSpatialRelationshipKindV2 kind,
        float minimumDistance,
        float maximumDistance,
        string tag)
    {
        YQSpatialRelationshipV2 relationship =
            new YQSpatialRelationshipV2
            {
                relationshipId = relationshipId,
                subjectId = subjectId,
                objectId = objectId,
                kind = kind,
                minimumDistance = Mathf.Max(0f, minimumDistance),
                maximumDistance = Mathf.Max(
                    minimumDistance,
                    maximumDistance),
                required = true
            };
        if (!string.IsNullOrWhiteSpace(tag))
            relationship.tags.Add(tag);
        blueprint.relationships.Add(relationship);
    }
}

// note: Editor verification utilities need the same deterministic helpers as runtime compilation, so expose this pure utility without duplicating its logic.
public static class YQSpatialBlueprintDeterminismV2
{
    public static List<T> SortedRecords<T>(
        IReadOnlyList<T> values,
        Func<T, string> idSelector)
        where T : class
    {
        List<T> sorted = new List<T>();
        if (values != null)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] != null)
                    sorted.Add(values[index]);
            }
        }

        sorted.Sort(
            (left, right) => string.CompareOrdinal(
                idSelector(left) ?? string.Empty,
                idSelector(right) ?? string.Empty));
        return sorted;
    }

    public static string SafeId(string value, string fallback)
    {
        string trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    public static Vector2 Direction(float headingDegrees)
    {
        float radians = headingDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    public static YQBlueprintPointV2 Point(
        Vector2 position,
        float normalizedElevation,
        float width)
    {
        return new YQBlueprintPointV2
        {
            x = position.x,
            z = position.y,
            normalizedElevation = Mathf.Clamp01(normalizedElevation),
            width = Mathf.Max(0f, width)
        };
    }

    public static YQBlueprintPointV2 CopyPoint(
        YQBlueprintPointV2 source,
        float normalizedElevation,
        float width)
    {
        return new YQBlueprintPointV2
        {
            x = source.x,
            z = source.z,
            normalizedElevation = Mathf.Clamp01(normalizedElevation),
            width = Mathf.Max(0f, width)
        };
    }

    public static float Hash01(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }

            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
