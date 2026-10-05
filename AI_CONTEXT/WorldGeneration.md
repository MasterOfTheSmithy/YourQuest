# World-generation context

## Authorities

`YQWorldGenerationService` produces/persists structured `GeneratedWorldPlanRecord` data and deterministic fallback records. `YQWorldGenerationArchitecture` freezes the spatial/materialization choice per transaction. `YQSpatialPlanVersionRouter` selects a complete accepted V2 artifact or a persisted V1 compatibility artifact. `YQSpatialBlueprintCompilerV2`, `YQSpatialBlueprintV2Validation`, `YQSpatialMaterializationV2`, and `YQGeneratedWorldIntegrityValidator` own compilation, validation, prepared materialization, and physical acceptance checks.

## Generation phases

Profile/world load and origin acceptance precede plan generation. The plan records seed, semantic fingerprint, generation/schema/validation versions, content hashes, regions, terrain fields, hydrology, sites, routes, and relationships. The accepted plan feeds terrain, routes, water/bridges, settlement/POI/hostile site construction, NPC population, and materialization. The builder owns the compiled-world publication and startup readiness transaction.

## Continuity

`YQContinuousWorldCellAuthority` and `YQContinuousWorldFeatureAuthority` derive order-independent continuation from the persisted seed, accepted origin heightfield, route/water envelopes, semantic plan, and stable hash inputs. Contracts are `continuous_world_cell_v5` and `continuous_edge_v3`. `YQGeneratedWorldTilePlan` owns deterministic biome/relief profiles; route and hydrology data come from accepted spatial materialization. Do not use traversal order or global random state for canonical output.

## Status

The September 15 baseline records compiled-world startup and semantic save/reload/rebind evidence alongside failed streaming and persistence timing gates. It is a dated snapshot. Use `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` for the maintained G08 workflow and later evidence pointers. Asset/settlement eligibility still requires its actual acceptance evidence; an architecture sheet does not certify it.
