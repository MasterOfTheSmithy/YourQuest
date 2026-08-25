using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQWorldGenerationService : MonoBehaviour
{
    public static YQWorldGenerationService Instance { get; private set; }

    [Header("World LLM")]
    public bool enableLlmWorldGeneration = true;
    public bool replaceDeterministicFallbackWithLlm = true;
    public bool useLlmWorldGenerationDuringInitialLock = true;
    public int worldNumPredict = 2000;
    [Range(0f, 1f)] public float worldTemperature = 0.36f;

    [Header("Background Lore")]
    public bool enableBackgroundLoreRefresh = true;
    [Min(300f)] public float backgroundLoreRefreshSeconds = 1800f;
    [Range(256, 1400)] public int backgroundLoreNumPredict = 700;

    [Header("Generated Scale")]
    [Range(4, 10)] public int targetRegionCount = 6;
    [Range(4, 18)] public int targetSettlementCount = 9;
    [Range(6, 24)] public int targetEncampmentCount = 14;
    [Range(20, 80)] public int targetPlayableHoursMin = 20;
    [Range(20, 120)] public int targetPlayableHoursMax = 50;

    public string LastWorldGenerationMessage { get; private set; } = string.Empty;

    private bool _requestInFlight;
    public bool IsRequestInFlight =>
    _requestInFlight;
    private bool _backgroundLoreRequestInFlight;
    private float _nextBackgroundLoreRefreshTime;
    private const string InitialGenerationOwner =
    "InitialWorldGeneration";

    private const int StartupLlmRegionCount =
        3;

    private const int StartupLlmSettlementCount =
        2;

    private const int StartupLlmEncampmentCount =
        2;

    private static readonly string[] FallbackSyllablesA =
    {
        "Aer", "Bel", "Cor", "Dath", "Eld", "Fen", "Ghal", "Hal", "Ith", "Jor", "Kel", "Lor", "Mor", "Nar", "Or", "Pyr", "Quel", "Ryn", "Sar", "Tor", "Ul", "Vael", "Wyn", "Yor"
    };

    private static readonly string[] FallbackSyllablesB =
    {
        "aven", "barrow", "cairn", "dell", "ember", "fall", "glen", "hollow", "mere", "reach", "ridge", "run", "spire", "vale", "ward", "weald"
    };

    private static readonly string[] RegionLandforms =
    {
        "Reach", "Vale", "Ridge", "Basin", "Weald", "March", "Hollow", "Coast", "Highroad", "Crown"
    };

    private static readonly string[] BiomeTags =
    {
        "forest", "highland", "marsh", "coast", "steppe", "cavern", "ruins", "snowline", "ashfield", "orchard", "wetland", "badland"
    };

    private static readonly string[] SettlementKinds =
    {
        "hamlet", "village", "town", "city", "outpost", "riverhold", "market", "waystation"
    };

    private static readonly string[] ServiceKinds =
    {
        "inn", "blacksmith", "general_goods", "apothecary", "tailor", "stable", "scribe", "relic_broker", "trainer", "locksmith", "cookhouse", "banker"
    };

    private static readonly string[] EncampmentKinds =
    {
        "cave", "ruin", "camp", "mine", "crypt", "lair", "watchpost", "burrow", "shrine", "shipwreck", "sinkhole", "tower"
    };

    private static readonly string[] PressureAxes =
    {
        "guard", "advance", "trade", "craft", "survival", "curiosity", "stillness", "mobility", "mercy", "dominion", "spellwork", "recovery"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQWorldGenerationService>() != null)
            return;

        GameObject go = new GameObject("00__YQ_WorldGenerationService");
        DontDestroyOnLoad(go);
        go.AddComponent<YQWorldGenerationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        // note: Initial generation is model-authored; serialized prototype values may not silently disable the canonical world request.
        useLlmWorldGenerationDuringInitialLock = true;
        // note: The compact startup schema still needs enough completion budget to close every JSON array; 1,250 tokens repeatedly truncated valid plans at the routes section.
        worldNumPredict = Mathf.Clamp(worldNumPredict, 1800, 2200);
        _nextBackgroundLoreRefreshTime =
            Time.unscaledTime +
            Mathf.Max(
                300f,
                backgroundLoreRefreshSeconds);
    }

    private void Update()
    {
        TryQueueBackgroundLoreRefresh();
    }

    private void TryQueueBackgroundLoreRefresh()
    {
        if (!enableBackgroundLoreRefresh ||
            _backgroundLoreRequestInFlight ||
            _requestInFlight ||
            YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked ||
            Time.unscaledTime <
                _nextBackgroundLoreRefreshTime)
        {
            return;
        }

        LLMClient llm =
            LLMClient.Instance;

        if (llm == null ||
            llm.IsBusy)
        {
            // note: Background flavor is nice, not urgent; retry later instead of joining a busy generation queue.
            _nextBackgroundLoreRefreshTime =
                Time.unscaledTime +
                60f;

            return;
        }

        WorldStateManager worldManager =
            WorldStateManager.Instance;

        PlayerStateManager playerManager =
            PlayerStateManager.Instance;

        WorldState world =
            worldManager != null
                ? worldManager.State
                : null;

        PlayerState player =
            playerManager != null
                ? playerManager.state
                : null;

        if (world == null ||
            !IsUsablePlan(
                world.generatedWorldPlan) ||
            !GeneratedRpgContentService
                .HasCompletedOrigin(
                    player))
        {
            _nextBackgroundLoreRefreshTime =
                Time.unscaledTime +
                60f;

            return;
        }

        string prompt =
            BuildBackgroundLoreRefreshPrompt(
                player,
                world);

        Dictionary<string, object> options =
            new Dictionary<string, object>
            {
                {
                    "num_predict",
                    Mathf.Clamp(
                        backgroundLoreNumPredict,
                        256,
                        1400)
                },
                {
                    "temperature",
                    0.52f
                },
                {
                    "top_p",
                    0.88f
                },
                {
                    // note: Background lore expires quickly so it cannot block gameplay-facing model work.
                    "request_timeout_seconds",
                    120
                }
            };

        _backgroundLoreRequestInFlight =
            true;

        _nextBackgroundLoreRefreshTime =
            Time.unscaledTime +
            Mathf.Max(
                300f,
                backgroundLoreRefreshSeconds);

        LastWorldGenerationMessage =
            "Queued non-destructive background world lore refresh.";

        // note: Lore refresh is player-invisible background work and must not preempt player-facing dialogue.
        llm.Submit(
            new YQLlmRequest
            {
                prompt = prompt,
                debugTag = "BackgroundWorldLoreRefresh",
                category = LLMGenerationCategory.WorldGeneration,
                priority = YQLlmRequestPriority.Background,
                requireJson = true,
                optionsOverride = options
            },
            result =>
            {
                // note: Rejecting malformed model output keeps accepted world canon unchanged.
                string raw = result.success ? result.text : null;
                _backgroundLoreRequestInFlight =
                    false;

                WorldStateManager activeWorldManager =
                    WorldStateManager.Instance;

                WorldState activeWorld =
                    activeWorldManager != null
                        ? activeWorldManager.State
                        : null;

                if (activeWorld == null ||
                    string.IsNullOrWhiteSpace(
                        raw))
                {
                    LastWorldGenerationMessage =
                        "Background world lore refresh produced no usable text.";

                    return;
                }

                if (TryApplyBackgroundLoreRefresh(
                        raw,
                        activeWorld,
                        out string message))
                {
                    LastWorldGenerationMessage =
                        message;

                    activeWorldManager?.Save();
                }
                else
                {
                    LastWorldGenerationMessage =
                        "Background world lore refresh rejected: " +
                        message;
                }
            });
    }

    public GeneratedWorldPlanRecord EnsureWorldPlan(PlayerState state, WorldState world, bool requestLlmIfFallback = false)
    {
        if (world == null)
            return null;

        world.EnsureCollections();
        if (!IsUsablePlan(world.generatedWorldPlan))
        {
            GeneratedWorldPlanRecord fallback = GenerateDeterministicFallbackPlan(state, world, "missing_plan");
            ApplyPlanToWorldState(fallback, world);
            LastWorldGenerationMessage = "Created deterministic world scaffold while waiting for LLM world generation.";
        }

        if (requestLlmIfFallback && ShouldRequestLlmPlan(world.generatedWorldPlan))
            TryRequestWorldPlan(state, world, null);

        return world.generatedWorldPlan;
    }

    public GeneratedWorldPlanRecord RegenerateAfterOrigin(PlayerState state, WorldState world, bool requestLlm = true)
    {
        if (world == null)
            return null;

        GeneratedWorldPlanRecord fallback = GenerateDeterministicFallbackPlan(state, world, "origin_committed");
        ApplyPlanToWorldState(fallback, world);
        LastWorldGenerationMessage = "Regenerated world scaffold from committed player origin.";

        if (requestLlm &&
            replaceDeterministicFallbackWithLlm &&
            (useLlmWorldGenerationDuringInitialLock ||
             !YQGeneratedWorldRuntimeBuilder
                 .IsInitialGenerationGameplayLocked))
        {
            // note: Large local-model world JSON is optional polish; locked startup keeps the valid deterministic scaffold.
            TryRequestWorldPlan(state, world, null);
        }

        return fallback;
    }

    public bool TryRequestWorldPlan(PlayerState state, WorldState world, Action<GeneratedWorldPlanRecord> onReady)
    {
        if (!enableLlmWorldGeneration || LLMClient.Instance == null || world == null || _requestInFlight)
            return false;

        if (YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked &&
            !useLlmWorldGenerationDuringInitialLock)
        {
            // note: The local model currently over-expands world-plan JSON; keep first load playable and enrich after unlock.
            LastWorldGenerationMessage =
                "Skipped blocking LLM world replacement during initial generation.";

            return false;
        }

        world.EnsureCollections();
        if (!replaceDeterministicFallbackWithLlm)
        {
            // note: Do not enqueue a multi-minute world replacement pass unless the project explicitly opts in.
            return false;
        }

        string seed = BuildWorldSeed(state, world);
        string prompt = BuildPrompt(state, world, seed);
        prompt =
    RemoveTemporaryWorldScaffoldFromGenerationPrompt(
        prompt);
        Dictionary<string, object> options = new Dictionary<string, object>
        {
            // note: Startup world plans need complete compact JSON, not long prose that overloads local VRAM.
            // note: Compact canonical records leave a reliable completion margin without monopolizing the local model for several minutes.
            { "num_predict", Mathf.Clamp(worldNumPredict, 1800, 2200) },
            { "temperature", Mathf.Clamp01(worldTemperature) },
            { "top_p", 0.84f },
            { "repeat_penalty", 1.05f },
            // note: World authoring may be slow, but it must fail back to the deterministic scaffold instead of freezing the startup lock.
            { "request_timeout_seconds", 90 }
        };

        _requestInFlight = true;
        LastWorldGenerationMessage = "Queued LLM world plan generation for seed " + seed + ".";
        
        // note: The first world plan is a startup transaction and cannot share the queue with unrelated work.
        LLMClient.Instance.Submit(
    new YQLlmRequest
    {
        prompt = prompt,
        debugTag = "WorldPlanGeneration",
        category = LLMGenerationCategory.WorldGeneration,
        priority = YQLlmRequestPriority.StartupExclusive,
        // note: Keep JSON-mode transport enabled; the world validator remains the authority for canonical acceptance and optional voice repair.
        requireJson = true,
        // note: The world validator owns optional Goddess-prose repair before strict canonical world validation.
        deferJsonValidationToCaller = true,
        exclusiveOwner = InitialGenerationOwner,
        disableTimeout = false,
        maxRetries = 0,
        optionsOverride = options
    },
    result =>
    {
        // note: Only a successfully normalized object reaches the deterministic plan parser.
        string raw = result.success ? result.text : null;
        _requestInFlight = false;
            WorldState targetWorld = WorldStateManager.Instance != null && WorldStateManager.Instance.State != null
                ? WorldStateManager.Instance.State
                : world;
            PlayerState targetPlayer = PlayerStateManager.Instance != null && PlayerStateManager.Instance.state != null
                ? PlayerStateManager.Instance.state
                : state;

            string currentSeed = BuildWorldSeed(targetPlayer, targetWorld);
            if (!string.Equals(currentSeed, seed, StringComparison.OrdinalIgnoreCase))
            {
                LastWorldGenerationMessage =
    "Discarded stale world LLM result for seed " +
    seed +
    "; active seed is " +
    currentSeed +
    ".";

            // note: A stale result does not get static Goddess filler; only accepted model output can enter the loading transcript.
            YQStartupLoadingScreen.SetGenerationStage(
                YQGoddessGenerationDialogue.TakeWorldCompletion(string.Empty),
                0.68f);

            onReady?.Invoke(
                    targetWorld != null
                        ? targetWorld.generatedWorldPlan
                        : null);

                return;
            }

            if (!TryParseWorldPlan(
        raw,
        seed,
        out GeneratedWorldPlanRecord plan,
        out string error))
            {
                LastWorldGenerationMessage =
                    "World LLM result rejected: " +
                    error;

                Debug.LogWarning(
                    "[YQWorldGenerationService] " +
                    LastWorldGenerationMessage +
                    "\nRAW:\n" +
                    TruncateForLog(raw ?? "<null>"));

                /*
                 * The deterministic scaffold already exists, so failure of
                 * the authored world pass is recoverable. Do not leave the
                 * player staring at an apparently frozen 45% screen.
                 */
                YQStartupLoadingScreen.SetGenerationStage(
                    string.Empty,
                    0.64f);

                onReady?.Invoke(
                    targetWorld != null
                        ? targetWorld.generatedWorldPlan
                        : null);

                return;
        }

        ApplyPlanToWorldState(
plan,
targetWorld);

        // note: The loading transcript reads the accepted plan's own data instead of awaiting a separate filler response.
        YQGoddessGenerationDialogue
            .SetWorldReadout(
                plan);


        WorldStateManager.Instance?.Save();

            LastWorldGenerationMessage =
                "World LLM result accepted: " +
                plan.regions.Count +
                " regions, " +
                plan.settlements.Count +
                " settlements, " +
                plan.encampments.Count +
                " encampments.";

            YQStartupLoadingScreen.SetGenerationStage(
                string.Empty,
                0.68f);

            onReady?.Invoke(
                plan);
    });

        return true;
    }
    private static string RemoveTemporaryWorldScaffoldFromGenerationPrompt(
    string prompt)
    {
        if (string.IsNullOrWhiteSpace(
                prompt))
        {
            return prompt;
        }

        const string startMarker =
            "GENERATED_WORLD_PLAN (compact)";

        const string endMarker =
            "PLAYER_SNAPSHOT";

        int start =
            prompt.IndexOf(
                startMarker,
                StringComparison.Ordinal);

        if (start < 0)
        {
            return prompt;
        }

        int end =
            prompt.IndexOf(
                endMarker,
                start,
                StringComparison.Ordinal);

        if (end <= start)
        {
            return prompt;
        }

        return
            prompt.Substring(
                0,
                start) +
            prompt.Substring(
                end);
    }
    private GeneratedWorldPlanRecord GenerateDeterministicFallbackPlan(PlayerState state, WorldState world, string reason)
    {
        string seed = BuildWorldSeed(state, world) + ":" + reason;
        System.Random rng = new System.Random(PositiveHash(seed));
        // note: A failed initial LLM request must leave a compact playable scaffold, not a full renderer and NPC stress test.
        int regionCount = GetStartupLlmRegionCount();
        int settlementTarget = GetStartupLlmSettlementCount(regionCount);
        int encampmentTarget = GetStartupLlmEncampmentCount(regionCount);
        string playerAxis = ResolvePlayerAxis(state, rng);
        string originPhrase = BuildOriginPhrase(state, playerAxis);

        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
        {
            schemaVersion = "world_plan_v1",
            source = "deterministic_world_fallback",
            worldSeed = BuildWorldSeed(state, world),
            generatorPromptHash = StableHex(seed),
            promptBudgetPolicy = "Persist verbose internals in save data; prompts receive compact world summaries, active-region details, and stable content IDs only.",
            summary = "A deterministic generated world scaffold built around the player's committed stimulus: " + originPhrase + ".",
            designNotes = "Fallback only: the LLM should replace this with authored generated specifics when available. Scale is representational so the 20-50 hour run stays stable.",
            rawJson = string.Empty,
            generatedUnixString = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            targetPlayableHoursMin = Mathf.Min(targetPlayableHoursMin, targetPlayableHoursMax),
            targetPlayableHoursMax = Mathf.Max(targetPlayableHoursMin, targetPlayableHoursMax),
            maxPromptWorldLines = 22
        };
        plan.EnsureCollections();
        plan.verboseInternals.Add("seed=" + plan.worldSeed + " reason=" + reason + " playerAxis=" + playerAxis);
        plan.verboseInternals.Add("content_budget=regions:" + regionCount + " settlements:" + settlementTarget + " encampments:" + encampmentTarget);
        plan.verboseInternals.Add("scale_rule=towns and cities describe simulated populations/services; runtime only needs notable actors until the scene streamer exists.");
        plan.verboseInternals.Add("prompt_policy=carry IDs, short summaries, and active-region internals; roll old details into ledger summaries.");

        for (int i = 0; i < regionCount; i++)
        {
            string regionSeed = seed + ":region:" + i;
            string name = BuildFallbackName(rng) + " " + Pick(RegionLandforms, rng);
            string biomeA = Pick(BiomeTags, rng);
            string biomeB = PickDistinct(BiomeTags, biomeA, rng);
            string pressure = i == 0 ? playerAxis : Pick(PressureAxes, rng);
            GeneratedRegionRecord region = new GeneratedRegionRecord
            {
                regionId = MakeId("region", name, regionSeed),
                displayName = name,
                regionIndex = i,
                role = i == 0 ? "origin-adjacent tutorial frontier" : "generated frontier",
                scaleHint = i == 0 ? "local" : (i == regionCount - 1 ? "distant" : "regional"),
                dangerTier = Mathf.Clamp(1 + i / 2 + rng.Next(0, 2), 1, 9),
                gridX = Mathf.RoundToInt(Mathf.Cos(i / (float)regionCount * Mathf.PI * 2f) * (2 + i % 3)),
                gridY = Mathf.RoundToInt(Mathf.Sin(i / (float)regionCount * Mathf.PI * 2f) * (2 + i % 3)),
                deterministicSeed = StableHex(regionSeed),
                terrainProfile = biomeA + " threaded with " + biomeB,
                climateProfile = BuildClimatePhrase(biomeA, biomeB, rng),
                playerPressure = "Tests " + pressure + " as a response to player behavior, not as a region title.",
                lore = "Local myths treat this place as evidence gathered by the old system after mortals began answering impossible pressure.",
                gameplayPremise = "Exploration, settlement errands, and enemy sites should branch around the player's repeated habits.",
                traversalHook = Pick(new[] { "switchback paths", "locked passes", "weather timing", "hidden lower route", "unstable bridges", "landmark navigation" }, rng),
                economyHook = Pick(new[] { "scarce metal", "rare herbs", "pilgrim trade", "salvage rights", "beast parts", "script contracts" }, rng),
                enemyPressureHook = Pick(new[] { "ambush discipline", "ranged harassment", "burrowing pursuit", "pack evasion", "elite spell use", "trap control" }, rng)
            };
            region.EnsureCollections();
            AddUnique(region.biomeTags, biomeA);
            AddUnique(region.biomeTags, biomeB);
            region.verboseInternals.Add("region_seed=" + region.deterministicSeed);
            region.verboseInternals.Add("placement=radial index " + i + " around origin; streamer can convert grid to world coordinates.");
            region.verboseInternals.Add("player_response_axis=" + pressure);
            plan.regions.Add(region);
        }

        for (int i = 0; i < settlementTarget; i++)
        {
            GeneratedRegionRecord region = plan.regions[i % plan.regions.Count];
            string settlementSeed = seed + ":settlement:" + i;
            string kind = i == 0 ? "town" : Pick(SettlementKinds, rng);
            string name = BuildFallbackName(rng) + " " + BuildSettlementSuffix(kind, rng);
            int serviceCount = ResolveServiceCount(kind, rng);
            GeneratedSettlementRecord settlement = new GeneratedSettlementRecord
            {
                settlementId = MakeId("settlement", name, settlementSeed),
                regionId = region.regionId,
                displayName = name,
                kind = kind,
                approxPopulation = ResolvePopulation(kind, rng),
                populationBand = BuildPopulationBand(kind),
                gridX = region.gridX * 8 + rng.Next(-3, 4),
                gridY = region.gridY * 8 + rng.Next(-3, 4),
                deterministicSeed = StableHex(settlementSeed),
                securityProfile = Pick(new[] { "open gates", "watch rotation", "contract wardens", "militia bell", "hidden patrols", "guild protection" }, rng),
                marketBias = Pick(new[] { "food and repairs", "road supplies", "rare components", "ore and tools", "books and rumors", "livestock and medicine" }, rng),
                lore = "The settlement exists to make the player's route legible: services, rumors, and disputes should answer what the player keeps doing.",
                dailyLoop = "Residents work, trade, worry over nearby threats, and expose quests through grounded needs."
            };
            settlement.EnsureCollections();
            FillServices(settlement.serviceSlots, serviceCount, rng);
            FillResidentRoles(settlement.residentRoles, kind, rng);
            AddUnique(settlement.factionIds, region.regionId + "_civic");
            settlement.questHookIds.Add(MakeId("questhook", name + "_local_need", settlementSeed));
            settlement.verboseInternals.Add("settlement_seed=" + settlement.deterministicSeed);
            settlement.verboseInternals.Add("scale=" + kind + " population=" + settlement.approxPopulation + " services=" + settlement.serviceSlots.Count);
            settlement.verboseInternals.Add("purpose=localized vendors, rumors, rest, and one conflict tied to nearby encampments.");
            plan.settlements.Add(settlement);
            AddUnique(region.settlementIds, settlement.settlementId);
        }

        for (int i = 0; i < encampmentTarget; i++)
        {
            GeneratedRegionRecord region = plan.regions[i % plan.regions.Count];
            string encampmentSeed = seed + ":encampment:" + i;
            string kind = Pick(EncampmentKinds, rng);
            string name = BuildFallbackName(rng) + " " + BuildEncampmentSuffix(kind, rng);
            GeneratedEncampmentRecord encampment = new GeneratedEncampmentRecord
            {
                encampmentId = MakeId("encampment", name, encampmentSeed),
                regionId = region.regionId,
                displayName = name,
                kind = kind,
                threatTier = Mathf.Clamp(region.dangerTier + rng.Next(0, 3), 1, 10),
                gridX = region.gridX * 8 + rng.Next(-5, 6),
                gridY = region.gridY * 8 + rng.Next(-5, 6),
                deterministicSeed = StableHex(encampmentSeed),
                inhabitantFactionId = MakeId("faction", name + "_inhabitants", encampmentSeed),
                // note: Fallback enemy identities mirror the imported creature/site vocabulary exposed to the Goddess prompt.
                monsterFamily = Pick(new[] { "raiders", "soldiers", "cult deserters", "cathedral fanatics", "sewer mutants", "restless dead", "mimic brood", "rogue constructs", "witchbound villagers", "gladiator shades", "blood-marked beasts", "spellbound beasts", "burrowers" }, rng),
                layoutIntent = Pick(new[] { "looped cave route", "collapsed multi-room ruin", "outer camp with inner boss tent", "vertical mine descent", "crypt ring with locked reliquary" }, rng),
                stealthApproach = Pick(new[] { "rear crawlspace", "high overlook", "riverbed approach", "disguised service path", "fog timing" }, rng),
                abilityProfile = Pick(new[] { "one caster, one evasive skirmisher, one brute", "burrowers surface near the player with dust VFX", "archers kite while guards flank", "casters shield wounded allies", "pack beasts retreat to traps" }, rng),
                surfacePresentation = Pick(new[] { "visible patrols", "subsurface particle trail", "torch smoke", "rune pulses", "broken banners", "disturbed soil" }, rng),
                bossIntent = Pick(new[] { "teaches interrupt timing", "teaches retreat discipline", "teaches lockpick reward risk", "teaches target priority", "teaches line-of-sight spell avoidance" }, rng),
                rewardProfile = Pick(new[] { "serviceable gear plus clue", "rare component cache", "local faction proof", "locked chest and map scrap", "spell reagent bundle" }, rng),
                lore = "This enemy site should be generated as a local problem with a reason to exist, a reward, and a behavior lesson."
            };
            encampment.EnsureCollections();
            encampment.questHookIds.Add(MakeId("questhook", name + "_threat", encampmentSeed));
            encampment.verboseInternals.Add("encampment_seed=" + encampment.deterministicSeed);
            encampment.verboseInternals.Add("region_pressure=" + region.playerPressure);
            encampment.verboseInternals.Add("ai_goal=distinct movement and ability patterns, not direct bum-rush only.");
            plan.encampments.Add(encampment);
            AddUnique(region.encampmentIds, encampment.encampmentId);
        }

        for (int i = 0; i < plan.regions.Count; i++)
        {
            GeneratedRegionRecord from = plan.regions[i];
            GeneratedRegionRecord to = plan.regions[(i + 1) % plan.regions.Count];
            GeneratedWorldRouteRecord route = new GeneratedWorldRouteRecord
            {
                routeId = MakeId("route", from.displayName + "_to_" + to.displayName, seed + ":route:" + i),
                fromRegionId = from.regionId,
                toRegionId = to.regionId,
                routeKind = Pick(new[] { "road", "ridge_pass", "river_path", "old_causeway", "cavern_cut", "pilgrim_track" }, rng),
                travelHook = "Route events should vary by player pace, waiting, stealth, combat habits, and carried items.",
                gateCondition = i == 0 ? "tutorial proof complete" : "generated local quest or traversal proof"
            };
            route.EnsureCollections();
            route.riskTags.Add(from.enemyPressureHook);
            route.riskTags.Add(to.enemyPressureHook);
            route.verboseInternals.Add("connects generated region graph index " + i + " to " + ((i + 1) % plan.regions.Count));
            plan.routes.Add(route);
        }

        for (int i = 0; i < Mathf.Clamp(regionCount + 2, 4, 12); i++)
        {
            string factionSeed = seed + ":faction:" + i;
            GeneratedRegionRecord region = plan.regions[i % plan.regions.Count];
            string name = BuildFallbackName(rng) + " " + Pick(new[] { "Compact", "Ledger", "Circle", "Host", "Company", "Covenant", "Kin", "Order" }, rng);
            GeneratedFactionPlanRecord faction = new GeneratedFactionPlanRecord
            {
                factionId = MakeId("faction", name, factionSeed),
                displayName = name,
                factionKind = Pick(new[] { "civic", "mercantile", "religious", "monster", "raider", "scholar", "craft", "warden" }, rng),
                homeRegionId = region.regionId,
                attitudeToPlayer = Mathf.Clamp((float)(rng.NextDouble() * 1.2 - 0.55), -1f, 1f),
                motive = Pick(new[] { "secure a road", "monopolize a resource", "interpret system signs", "survive a local threat", "hunt an old relic", "control settlement debt" }, rng),
                publicFace = Pick(new[] { "helpful", "wary", "predatory", "lawful", "desperate", "mysterious" }, rng),
                relationToPlayer = "Initial stance should update from witnessed choices, not faction label alone."
            };
            faction.EnsureCollections();
            faction.conflictTags.Add(region.enemyPressureHook);
            faction.verboseInternals.Add("faction_seed=" + StableHex(factionSeed));
            faction.verboseInternals.Add("used_for=vendors, hostile sites, NPC attitudes, and generated quests.");
            plan.factions.Add(faction);
        }

        return NormalizePlan(plan, plan.worldSeed, string.Empty);
    }

    private bool TryParseWorldPlan(
    string raw,
    string seed,
    out GeneratedWorldPlanRecord plan,
    out string error)
    {
        plan =
            null;

        error =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                raw))
        {
            error =
                "empty response";

            return false;
        }

        try
        {
            string json =
                ExtractFirstJsonObject(
                    raw);

            if (string.IsNullOrWhiteSpace(
                    json))
            {
                error =
                    "no JSON object";

                return false;
            }

            JObject root;
            if (!TryParseRootIgnoringBrokenGoddessVoice(
                    json,
                    out root,
                    out string parseError))
            {
                error =
                    parseError;

                return false;
            }

            // note: Repair a common local-model schema slip before typed deserialization; the first declared faction remains the deterministic authority.
            NormalizeGeneratedStringField(
                root["encampments"] as JArray,
                "inhabitantFactionId");

            YQGoddessGenerationVoiceDto goddessVoice =
                null;

            try
            {
                goddessVoice =
                    root["goddessVoice"]
                        ?.ToObject<
                            YQGoddessGenerationVoiceDto>();
            }
            catch
            {
                /*
                 * Presentation failure must never invalidate the world.
                 */
                goddessVoice =
                    null;
            }

            /*
             * Never persist Goddess presentation text into canonical world JSON.
             */
            root.Remove(
                "goddessVoice");

            string canonicalJson =
                root.ToString(
                    Formatting.None);

            plan =
                JsonConvert.DeserializeObject<
                    GeneratedWorldPlanRecord>(
                        canonicalJson);

            if (plan == null)
            {
                error =
                    "JSON parsed to null";

                return false;
            }

            plan =
                NormalizePlan(
                    plan,
                    seed,
                    canonicalJson);


            /*
             * Basic structural validation.
             *
             * This catches empty/truncated responses before applying
             * the stricter new-world generation contract below.
             */
            if (!IsUsablePlan(
                    plan))
            {
                error =
                    "plan has insufficient regions, settlements, or encampments";

                return false;
            }

            /*
             * NEW-SAVE WORLD SCALE CONTRACT
             *
             * The configured generation targets are not suggestions.
             *
             * A newly-authored LLM world may replace the deterministic
             * scaffold only if it contains the exact requested canonical
             * counts.
             *
             * This prevents a response such as:
             *
             *     4 regions
             *     2 settlements
             *     4 encampments
             *
             * from replacing the valid configured:
             *
             *     6 regions
             *     9 settlements
             *     14 encampments
             *
             * world.
             */
            int requiredRegions =
                GetStartupLlmRegionCount();

            int requiredSettlements =
                GetStartupLlmSettlementCount(
                    requiredRegions);

            int requiredEncampments =
                GetStartupLlmEncampmentCount(
                    requiredRegions);

            if (plan.regions.Count !=
                requiredRegions)
            {
                error =
                    "world scale contract failed: expected exactly " +
                    requiredRegions +
                    " regions but received " +
                    plan.regions.Count;

                return false;
            }

            if (plan.settlements.Count !=
                requiredSettlements)
            {
                error =
                    "world scale contract failed: expected exactly " +
                    requiredSettlements +
                    " settlements but received " +
                    plan.settlements.Count;

                return false;
            }

            if (plan.encampments.Count !=
    requiredEncampments)
            {
                error =
                    "world scale contract failed: expected exactly " +
                    requiredEncampments +
                    " encampments but received " +
                    plan.encampments.Count;

                return false;
            }
            if (!ValidateWorldIdentityUniqueness(
        plan,
        out string uniquenessError))
            {
                error =
                    "world identity contract failed: " +
                    uniquenessError;

                return false;
            }
            /*
             * COUNT ALONE IS NOT CANONICAL UNIQUENESS.
             *
             * Small models can satisfy an array length requirement by
             * repeating the same region, settlement, or encampment several
             * times. Such a response must never replace the deterministic
             * scaffold.
             */
            if (!ValidateCanonicalWorldUniqueness(
                    plan,
                    out error))
            {
                return false;
            }
            /*
 * Only accept the presentation bundle AFTER the canonical world
 * itself has passed every structural and uniqueness validator.
 */
            YQGoddessGenerationDialogue
                .SetWorldVoice(
                    goddessVoice);

            return true;
            
        }
        catch (Exception ex)
        {
            error =
                ex.Message;

            return false;
        }
    }
    private static bool ValidateWorldIdentityUniqueness(
    GeneratedWorldPlanRecord plan,
    out string error)
    {
        error =
            string.Empty;

        if (plan == null)
        {
            error =
                "world plan is null";

            return false;
        }

        plan.EnsureCollections();

        HashSet<string> regionIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> regionNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region == null)
            {
                error =
                    "region[" +
                    i +
                    "] is null";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    region.regionId))
            {
                error =
                    "region[" +
                    i +
                    "] has no regionId";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    region.displayName))
            {
                error =
                    "region '" +
                    region.regionId +
                    "' has no displayName";

                return false;
            }

            if (!regionIds.Add(
                    region.regionId.Trim()))
            {
                error =
                    "duplicate regionId '" +
                    region.regionId +
                    "'";

                return false;
            }

            if (!regionNames.Add(
                    region.displayName.Trim()))
            {
                error =
                    "duplicate region displayName '" +
                    region.displayName +
                    "'";

                return false;
            }
        }

        HashSet<string> settlementIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> settlementNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement == null)
            {
                error =
                    "settlement[" +
                    i +
                    "] is null";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settlement.settlementId))
            {
                error =
                    "settlement[" +
                    i +
                    "] has no settlementId";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settlement.displayName))
            {
                error =
                    "settlement '" +
                    settlement.settlementId +
                    "' has no displayName";

                return false;
            }

            if (!settlementIds.Add(
                    settlement.settlementId.Trim()))
            {
                error =
                    "duplicate settlementId '" +
                    settlement.settlementId +
                    "'";

                return false;
            }

            if (!settlementNames.Add(
                    settlement.displayName.Trim()))
            {
                error =
                    "duplicate settlement displayName '" +
                    settlement.displayName +
                    "'";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settlement.regionId) ||
                !regionIds.Contains(
                    settlement.regionId.Trim()))
            {
                error =
                    "settlement '" +
                    settlement.displayName +
                    "' references unknown regionId '" +
                    settlement.regionId +
                    "'";

                return false;
            }
        }

        HashSet<string> encampmentIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> encampmentNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment == null)
            {
                error =
                    "encampment[" +
                    i +
                    "] is null";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    encampment.encampmentId))
            {
                error =
                    "encampment[" +
                    i +
                    "] has no encampmentId";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    encampment.displayName))
            {
                error =
                    "encampment '" +
                    encampment.encampmentId +
                    "' has no displayName";

                return false;
            }

            if (!encampmentIds.Add(
                    encampment.encampmentId.Trim()))
            {
                error =
                    "duplicate encampmentId '" +
                    encampment.encampmentId +
                    "'";

                return false;
            }

            if (!encampmentNames.Add(
                    encampment.displayName.Trim()))
            {
                error =
                    "duplicate encampment displayName '" +
                    encampment.displayName +
                    "'";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    encampment.regionId) ||
                !regionIds.Contains(
                    encampment.regionId.Trim()))
            {
                error =
                    "encampment '" +
                    encampment.displayName +
                    "' references unknown regionId '" +
                    encampment.regionId +
                    "'";

                return false;
            }
        }

        return true;
    }
    private static bool ValidateCanonicalWorldUniqueness(
    GeneratedWorldPlanRecord plan,
    out string error)
    {
        error =
            string.Empty;

        if (plan == null)
        {
            error =
                "world uniqueness validation received null plan";

            return false;
        }

        plan.EnsureCollections();

        HashSet<string> regionIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> regionNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region == null)
            {
                error =
                    "world uniqueness contract failed: null region at index " +
                    i;

                return false;
            }

            string id =
                Safe(
                    region.regionId,
                    string.Empty);

            string name =
                Safe(
                    region.displayName,
                    string.Empty);

            if (string.IsNullOrWhiteSpace(id))
            {
                error =
                    "world uniqueness contract failed: region at index " +
                    i +
                    " has no regionId";

                return false;
            }

            if (!regionIds.Add(id))
            {
                error =
                    "world uniqueness contract failed: duplicate regionId '" +
                    id +
                    "'";

                return false;
            }

            if (!string.IsNullOrWhiteSpace(name) &&
                !regionNames.Add(name))
            {
                error =
                    "world uniqueness contract failed: duplicate region name '" +
                    name +
                    "'";

                return false;
            }
        }

        HashSet<string> settlementIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> settlementNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement == null)
            {
                error =
                    "world uniqueness contract failed: null settlement at index " +
                    i;

                return false;
            }

            string id =
                Safe(
                    settlement.settlementId,
                    string.Empty);

            string name =
                Safe(
                    settlement.displayName,
                    string.Empty);

            if (string.IsNullOrWhiteSpace(id))
            {
                error =
                    "world uniqueness contract failed: settlement at index " +
                    i +
                    " has no settlementId";

                return false;
            }

            if (!settlementIds.Add(id))
            {
                error =
                    "world uniqueness contract failed: duplicate settlementId '" +
                    id +
                    "'";

                return false;
            }

            if (!string.IsNullOrWhiteSpace(name) &&
                !settlementNames.Add(name))
            {
                error =
                    "world uniqueness contract failed: duplicate settlement name '" +
                    name +
                    "'";

                return false;
            }
        }

        HashSet<string> encampmentIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> encampmentNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment == null)
            {
                error =
                    "world uniqueness contract failed: null encampment at index " +
                    i;

                return false;
            }

            string id =
                Safe(
                    encampment.encampmentId,
                    string.Empty);

            string name =
                Safe(
                    encampment.displayName,
                    string.Empty);

            if (string.IsNullOrWhiteSpace(id))
            {
                error =
                    "world uniqueness contract failed: encampment at index " +
                    i +
                    " has no encampmentId";

                return false;
            }

            if (!encampmentIds.Add(id))
            {
                error =
                    "world uniqueness contract failed: duplicate encampmentId '" +
                    id +
                    "'";

                return false;
            }

            if (!string.IsNullOrWhiteSpace(name) &&
                !encampmentNames.Add(name))
            {
                error =
                    "world uniqueness contract failed: duplicate encampment name '" +
                    name +
                    "'";

                return false;
            }
        }

        return true;
    }
    private static GeneratedWorldPlanRecord NormalizePlan(GeneratedWorldPlanRecord plan, string seed, string rawJson)
    {
        plan ??= new GeneratedWorldPlanRecord();
        plan.EnsureCollections();
        plan.schemaVersion = Safe(plan.schemaVersion, "world_plan_v1");
        plan.source = Safe(plan.source, string.IsNullOrWhiteSpace(rawJson) ? "deterministic_world_fallback" : "llm_world_plan_v1");
        plan.worldSeed = Safe(plan.worldSeed, seed);
        plan.generatorPromptHash = Safe(plan.generatorPromptHash, StableHex(seed + ":world_plan_prompt"));
        plan.promptBudgetPolicy = TrimTo(Safe(plan.promptBudgetPolicy, "Use compact summaries in prompts and persist verbose internals in save data."), 360);
        plan.summary = TrimTo(Safe(plan.summary, "Generated world plan."), 560);
        plan.designNotes = TrimTo(Safe(plan.designNotes, "Generated deterministically from the save seed and player state."), 720);
        plan.rawJson = rawJson ?? string.Empty;
        plan.generatedUnixString = Safe(plan.generatedUnixString, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        plan.targetPlayableHoursMin = Mathf.Clamp(plan.targetPlayableHoursMin <= 0 ? 20 : plan.targetPlayableHoursMin, 5, 120);
        plan.targetPlayableHoursMax = Mathf.Clamp(plan.targetPlayableHoursMax <= 0 ? 50 : plan.targetPlayableHoursMax, plan.targetPlayableHoursMin, 160);
        plan.maxPromptWorldLines = Mathf.Clamp(plan.maxPromptWorldLines <= 0 ? 22 : plan.maxPromptWorldLines, 8, 60);
        TrimList(plan.verboseInternals, 24, 240);

        for (int i = plan.regions.Count - 1; i >= 0; i--)
        {
            GeneratedRegionRecord region = plan.regions[i];
            if (region == null)
            {
                plan.regions.RemoveAt(i);
                continue;
            }

            region.EnsureCollections();
            region.displayName = TrimTo(Safe(region.displayName, "Generated Region " + (i + 1)), 72);
            region.regionId = SafeId(region.regionId, "region", region.displayName, seed + ":region:" + i);
            region.regionIndex = i;
            region.role = TrimTo(Safe(region.role, "generated frontier"), 80);
            region.scaleHint = TrimTo(Safe(region.scaleHint, "regional"), 80);
            region.dangerTier = Mathf.Clamp(region.dangerTier <= 0 ? 1 + i / 2 : region.dangerTier, 1, 10);
            region.deterministicSeed = Safe(region.deterministicSeed, StableHex(seed + ":region:" + i));
            region.terrainProfile = TrimTo(Safe(region.terrainProfile, "mixed terrain"), 160);
            region.climateProfile = TrimTo(Safe(region.climateProfile, "variable local weather"), 160);
            region.playerPressure = TrimTo(Safe(region.playerPressure, "Responds to the player's repeated habits."), 240);
            region.lore = TrimTo(Safe(region.lore, "Generated local lore pending player discovery."), 480);
            region.gameplayPremise = TrimTo(Safe(region.gameplayPremise, "Exploration, settlement tasks, and enemy pressure."), 360);
            region.traversalHook = TrimTo(Safe(region.traversalHook, "landmark navigation"), 120);
            region.economyHook = TrimTo(Safe(region.economyHook, "local trade pressure"), 120);
            region.enemyPressureHook = TrimTo(Safe(region.enemyPressureHook, "varied hostile behavior"), 140);
            TrimList(region.biomeTags, 8, 40);
            TrimList(region.settlementIds, 12, 80);
            TrimList(region.encampmentIds, 16, 80);
            TrimList(region.landmarkIds, 12, 80);
            TrimList(region.verboseInternals, 8, 220);
        }

        for (int i = plan.settlements.Count - 1; i >= 0; i--)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement == null)
            {
                plan.settlements.RemoveAt(i);
                continue;
            }

            settlement.EnsureCollections();
            settlement.displayName = TrimTo(Safe(settlement.displayName, "Generated Settlement " + (i + 1)), 72);
            settlement.settlementId = SafeId(settlement.settlementId, "settlement", settlement.displayName, seed + ":settlement:" + i);
            settlement.regionId = SafeRegionId(plan, settlement.regionId, i);
            settlement.kind = NormalizeKey(Safe(settlement.kind, "settlement"));
            settlement.approxPopulation = Mathf.Clamp(settlement.approxPopulation <= 0 ? DefaultPopulation(settlement.kind) : settlement.approxPopulation, 4, 220);
            settlement.populationBand = TrimTo(Safe(settlement.populationBand, BuildPopulationBand(settlement.kind)), 90);
            settlement.deterministicSeed = Safe(settlement.deterministicSeed, StableHex(seed + ":settlement:" + i));
            settlement.siteStyleIntent = NormalizeKey(
                TrimTo(Safe(
                    settlement.siteStyleIntent,
                    FindRegion(plan, settlement.regionId)?.assetStyleKey),
                    80));
            settlement.siteRoleIntent = NormalizeKey(
                TrimTo(Safe(settlement.siteRoleIntent, settlement.kind), 80));
            settlement.securityProfile = TrimTo(Safe(settlement.securityProfile, "local watch"), 160);
            settlement.marketBias = TrimTo(Safe(settlement.marketBias, "general supplies"), 160);
            settlement.lore = TrimTo(Safe(settlement.lore, "Generated settlement lore pending player discovery."), 420);
            settlement.dailyLoop = TrimTo(Safe(settlement.dailyLoop, "Residents work, trade, rest, and expose local needs."), 320);
            TrimList(settlement.serviceSlots, 10, 60);
            TrimList(settlement.residentRoles, 12, 70);
            TrimList(settlement.notableNpcIds, 12, 80);
            TrimList(settlement.factionIds, 8, 80);
            TrimList(settlement.questHookIds, 12, 100);
            TrimList(settlement.verboseInternals, 8, 220);
            LinkSettlement(plan, settlement);
        }

        for (int i = plan.encampments.Count - 1; i >= 0; i--)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[i];
            if (encampment == null)
            {
                plan.encampments.RemoveAt(i);
                continue;
            }

            encampment.EnsureCollections();
            encampment.displayName = TrimTo(Safe(encampment.displayName, "Generated Enemy Site " + (i + 1)), 72);
            encampment.encampmentId = SafeId(encampment.encampmentId, "encampment", encampment.displayName, seed + ":encampment:" + i);
            encampment.regionId = SafeRegionId(plan, encampment.regionId, i);
            encampment.kind = NormalizeKey(Safe(encampment.kind, "site"));
            encampment.threatTier = Mathf.Clamp(encampment.threatTier <= 0 ? 1 : encampment.threatTier, 1, 12);
            encampment.deterministicSeed = Safe(encampment.deterministicSeed, StableHex(seed + ":encampment:" + i));
            encampment.siteStyleIntent = NormalizeKey(
                TrimTo(Safe(
                    encampment.siteStyleIntent,
                    FindRegion(plan, encampment.regionId)?.assetStyleKey),
                    80));
            encampment.siteRoleIntent = NormalizeKey(
                TrimTo(Safe(
                    encampment.siteRoleIntent,
                    encampment.layoutIntent),
                    80));
            encampment.inhabitantFactionId = SafeId(encampment.inhabitantFactionId, "faction", encampment.displayName + "_inhabitants", seed + ":encampment_faction:" + i);
            encampment.monsterFamily = TrimTo(Safe(encampment.monsterFamily, "generated hostiles"), 120);
            encampment.layoutIntent = TrimTo(Safe(encampment.layoutIntent, "small encounter site with reward room"), 200);
            encampment.stealthApproach = TrimTo(Safe(encampment.stealthApproach, "optional alternate approach"), 160);
            encampment.abilityProfile = TrimTo(Safe(encampment.abilityProfile, "mixed melee, evade, and ranged pressure"), 220);
            encampment.surfacePresentation = TrimTo(Safe(encampment.surfacePresentation, "visible patrols or surface tells"), 160);
            encampment.bossIntent = TrimTo(Safe(encampment.bossIntent, "teaches a specific counterplay lesson"), 180);
            encampment.rewardProfile = TrimTo(Safe(encampment.rewardProfile, "loot plus local clue"), 160);
            encampment.lore = TrimTo(Safe(encampment.lore, "Generated enemy-site lore pending player discovery."), 420);
            TrimList(encampment.questHookIds, 12, 100);
            TrimList(encampment.verboseInternals, 8, 220);
            LinkEncampment(plan, encampment);
        }

        SeparateHostileSitesFromSettlements(
            plan,
            seed);

        for (int i = plan.routes.Count - 1; i >= 0; i--)
        {
            GeneratedWorldRouteRecord route = plan.routes[i];
            if (route == null)
            {
                plan.routes.RemoveAt(i);
                continue;
            }

            route.EnsureCollections();
            route.routeId = SafeId(route.routeId, "route", route.fromRegionId + "_to_" + route.toRegionId, seed + ":route:" + i);
            route.fromRegionId = SafeRegionId(plan, route.fromRegionId, i);
            route.toRegionId = SafeRegionId(plan, route.toRegionId, i + 1);
            route.routeKind = NormalizeKey(Safe(route.routeKind, "road"));
            route.travelHook = TrimTo(Safe(route.travelHook, "Generated route events respond to player behavior."), 220);
            route.gateCondition = TrimTo(Safe(route.gateCondition, "local proof or route discovery"), 160);
            TrimList(route.riskTags, 8, 60);
            TrimList(route.landmarkIds, 8, 80);
            TrimList(route.verboseInternals, 6, 200);
        }

        for (int i = plan.factions.Count - 1; i >= 0; i--)
        {
            GeneratedFactionPlanRecord faction = plan.factions[i];
            if (faction == null)
            {
                plan.factions.RemoveAt(i);
                continue;
            }

            faction.EnsureCollections();
            faction.displayName = TrimTo(Safe(faction.displayName, "Generated Faction " + (i + 1)), 72);
            faction.factionId = SafeId(faction.factionId, "faction", faction.displayName, seed + ":faction:" + i);
            faction.factionKind = NormalizeKey(Safe(faction.factionKind, "local"));
            faction.homeRegionId = SafeRegionId(plan, faction.homeRegionId, i);
            faction.attitudeToPlayer = Mathf.Clamp(faction.attitudeToPlayer, -1f, 1f);
            faction.motive = TrimTo(Safe(faction.motive, "protect its local interests"), 180);
            faction.publicFace = TrimTo(Safe(faction.publicFace, "unknown"), 120);
            faction.relationToPlayer = TrimTo(Safe(faction.relationToPlayer, "Changes according to witnessed player choices."), 220);
            TrimList(faction.conflictTags, 8, 60);
            TrimList(faction.verboseInternals, 6, 200);
        }

        for (int i = plan.pointsOfInterest.Count - 1; i >= 0; i--)
        {
            GeneratedPointOfInterestRecord poi = plan.pointsOfInterest[i];
            if (poi == null)
            {
                plan.pointsOfInterest.RemoveAt(i);
                continue;
            }

            poi.EnsureCollections();
            poi.displayName = TrimTo(Safe(poi.displayName, "Generated POI " + (i + 1)), 72);
            poi.poiId = SafeId(poi.poiId, "poi", poi.displayName, seed + ":poi:" + i);
            poi.regionId = SafeRegionId(plan, poi.regionId, i);
            poi.kind = NormalizeKey(Safe(poi.kind, "landmark"));
            poi.deterministicSeed = Safe(poi.deterministicSeed, StableHex(seed + ":poi:" + i));
            poi.lore = TrimTo(Safe(poi.lore, "Generated point-of-interest lore."), 320);
            poi.gameplayHook = TrimTo(Safe(poi.gameplayHook, "Optional exploration hook."), 180);
            poi.visualStyleKey = TrimTo(Safe(poi.visualStyleKey, "inherit_region_style"), 80);
            TrimList(poi.questHookIds, 6, 100);
            TrimList(poi.landmarkIds, 6, 80);
            TrimList(poi.tags, 8, 50);
            AddLandmarksToRegion(plan, poi.regionId, poi.landmarkIds);
            AddWorldQuestHooksToLocation(plan, poi.poiId, poi.questHookIds);
        }

        for (int i = plan.worldQuestHooks.Count - 1; i >= 0; i--)
        {
            GeneratedWorldQuestHookRecord hook = plan.worldQuestHooks[i];
            if (hook == null)
            {
                plan.worldQuestHooks.RemoveAt(i);
                continue;
            }

            hook.EnsureCollections();
            hook.displayName = TrimTo(Safe(hook.displayName, "Generated World Quest " + (i + 1)), 72);
            hook.hookId = SafeId(hook.hookId, "questhook", hook.displayName, seed + ":world_quest:" + i);
            hook.regionId = SafeRegionId(plan, hook.regionId, i);
            hook.locationId = NormalizeId(Safe(hook.locationId, hook.regionId));
            hook.premise = TrimTo(Safe(hook.premise, "Generated world quest seed."), 320);
            hook.objectiveIntent = TrimTo(Safe(hook.objectiveIntent, "investigate"), 120);
            hook.rewardIntent = TrimTo(Safe(hook.rewardIntent, "local reward"), 120);
            TrimList(hook.tags, 8, 50);
            AddWorldQuestHookToPlan(plan, hook.locationId, hook.hookId);
        }

        for (int i = plan.notableObjects.Count - 1; i >= 0; i--)
        {
            GeneratedNotableWorldObjectRecord item = plan.notableObjects[i];
            if (item == null)
            {
                plan.notableObjects.RemoveAt(i);
                continue;
            }

            item.EnsureCollections();
            item.displayName = TrimTo(Safe(item.displayName, "Generated Object " + (i + 1)), 72);
            item.objectId = SafeId(item.objectId, "object", item.displayName, seed + ":notable_object:" + i);
            item.regionId = SafeRegionId(plan, item.regionId, i);
            item.locationId = NormalizeId(Safe(item.locationId, item.regionId));
            item.objectType = NormalizeKey(Safe(item.objectType, "relic"));
            item.itemType = NormalizeKey(Safe(item.itemType, "misc"));
            item.rarity = TrimTo(Safe(item.rarity, "uncommon"), 40);
            item.visualFamily = TrimTo(Safe(item.visualFamily, "region_style"), 80);
            item.gameplayUse = TrimTo(Safe(item.gameplayUse, "optional interaction hook"), 160);
            item.lore = TrimTo(Safe(item.lore, "Generated notable object lore."), 320);
            TrimList(item.tags, 8, 50);
        }

        YQWorldAssetCatalog.EnsureAssetPalettes(plan);
        return plan;
    }

    private static void ApplyPlanToWorldState(GeneratedWorldPlanRecord plan, WorldState world)
    {
        if (plan == null || world == null)
            return;

        plan.EnsureCollections();
        world.EnsureCollections();
        world.generatedWorldPlan = plan;
        world.globalFlags["worldplan:regions"] = plan.regions.Count;
        world.globalFlags["worldplan:settlements"] = plan.settlements.Count;
        world.globalFlags["worldplan:encampments"] = plan.encampments.Count;
        world.globalFlags["worldplan:asset_palettes"] = plan.assetPalettes != null ? plan.assetPalettes.Count : 0f;
        world.globalFlags["worldplan:source_is_llm"] = IsLlmPlan(plan) ? 1f : 0f;
        world.AppendCanon("Generated world plan active for seed " + plan.worldSeed + ": " + plan.summary, 64);

        for (int i = 0; i < plan.regions.Count; i++)
        {
            GeneratedRegionRecord region = plan.regions[i];
            if (region == null)
                continue;
            UpsertLocation(world, region.regionId, region.regionId, region.displayName, region.gameplayPremise, "generated_region", 0.7f + region.dangerTier * 0.04f, region.lore);
        }

        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement == null)
                continue;
            UpsertLocation(world, settlement.settlementId, settlement.regionId, settlement.displayName, settlement.lore, "generated_" + settlement.kind, 0.45f, settlement.dailyLoop);
        }

        for (int i = 0; i < plan.encampments.Count; i++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[i];
            if (encampment == null)
                continue;
            UpsertLocation(world, encampment.encampmentId, encampment.regionId, encampment.displayName, encampment.lore, "hostile_" + encampment.kind, 0.55f + encampment.threatTier * 0.03f, encampment.layoutIntent);
            UpsertFaction(world, encampment.inhabitantFactionId, encampment.displayName + " Inhabitants", encampment.monsterFamily, "hostile", -0.35f);
        }

        for (int i = 0; i < plan.factions.Count; i++)
        {
            GeneratedFactionPlanRecord faction = plan.factions[i];
            if (faction == null)
                continue;
            UpsertFaction(world, faction.factionId, faction.displayName, faction.motive, faction.publicFace, faction.attitudeToPlayer);
        }

        for (int i = 0; i < plan.pointsOfInterest.Count; i++)
        {
            GeneratedPointOfInterestRecord poi = plan.pointsOfInterest[i];
            if (poi == null)
                continue;

            // note: POIs become additive world locations so existing map/context systems can see them.
            UpsertLocation(world, poi.poiId, poi.regionId, poi.displayName, poi.lore, "generated_poi_" + poi.kind, 0.48f, poi.gameplayHook);
        }

        for (int i = 0; i < plan.worldQuestHooks.Count; i++)
        {
            GeneratedWorldQuestHookRecord hook = plan.worldQuestHooks[i];
            if (hook == null)
                continue;

            world.AppendCanon(
                "World quest hook " +
                hook.hookId +
                " at " +
                hook.locationId +
                ": " +
                hook.premise,
                96);
        }

        for (int i = 0; i < plan.notableObjects.Count; i++)
        {
            GeneratedNotableWorldObjectRecord item = plan.notableObjects[i];
            if (item == null)
                continue;

            world.AppendCanon(
                "Notable " +
                item.objectType +
                " " +
                item.objectId +
                " at " +
                item.locationId +
                ": " +
                item.lore,
                96);
        }

        world.lastLLMRationale = plan.source + ": " + plan.designNotes;
        world.TouchNow();
    }

    private static string BuildBackgroundLoreRefreshPrompt(
        PlayerState player,
        WorldState world)
    {
        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        plan?.EnsureCollections();

        StringBuilder context =
            new StringBuilder();

        context.AppendLine(
            "NON_DESTRUCTIVE_WORLD_LORE_REFRESH");

        context.AppendLine(
            "WORLD_SEED: " +
            Safe(
                plan != null
                    ? plan.worldSeed
                    : string.Empty,
                world.worldName));

        context.AppendLine(
            "WORLD_SUMMARY: " +
            Safe(
                plan != null
                    ? plan.summary
                    : string.Empty,
                world.worldName));

        context.AppendLine(
            "PLAYER_ORIGIN");

        context.AppendLine(
            BuildOriginBlock(
                player));

        context.AppendLine(
            "RECENT_CANON");

        List<string> canon =
            world.GetCanonLines();

        int canonStart =
            Mathf.Max(
                0,
                canon.Count - 10);

        for (int i = canonStart;
             i < canon.Count;
             i++)
        {
            context.AppendLine(
                "- " +
                canon[i]);
        }

        context.AppendLine(
            "EXISTING_GENERATED_LOCATIONS");

        if (plan != null)
        {
            AppendLocationSummary(
                context,
                plan);
        }

        string task =
            "Generate a compact non-destructive lore refresh for the already accepted world. " +
            "You may add context, rumors, POI hooks, notable object/item rumors, and world quest hooks. " +
            "Do not rename, delete, move, replace, contradict, or retcon any existing region, settlement, encampment, NPC, or faction. " +
            "Use existing region/location IDs when attaching new context. " +
            "New POIs must be additive side locations or landmarks inside an existing region. " +
            "Return only JSON and stop after the final closing brace.";

        string schema =
            PromptContextBuilder.WrapJsonSchema(
                "{" +
                "\"canonLines\":[\"1 short durable world-memory line\"]," +
                "\"poiLocations\":[{" +
                "\"locationId\":\"stable_new_or_existing_poi_id\"," +
                "\"regionId\":\"existing_region_id\"," +
                "\"name\":\"short POI name\"," +
                "\"description\":\"short concrete description\"," +
                "\"state\":\"generated_poi\"," +
                "\"importance\":0.4," +
                "\"text\":\"short gameplay-facing note\"," +
                "\"landmarkIds\":[\"stable_landmark_id\"]," +
                "\"questHookIds\":[\"stable_world_quest_hook_id\"]" +
                "}]," +
                "\"worldQuestHooks\":[{" +
                "\"hookId\":\"stable_world_quest_hook_id\"," +
                "\"locationId\":\"existing settlement, encampment, region, or POI id\"," +
                "\"summary\":\"short quest seed\"" +
                "}]," +
                "\"notableObjects\":[{" +
                "\"objectId\":\"stable_object_or_item_id\"," +
                "\"locationId\":\"existing location id\"," +
                "\"objectType\":\"weapon|accessory|tool|relic|container|document|machine|natural_feature\"," +
                "\"summary\":\"short grounded object/item rumor\"" +
                "}]" +
                "}");

        return PromptContextBuilder.BuildContext(
            task,
            schema,
            context.ToString(),
            BuildLedger(
                player));
    }

    private static void AppendLocationSummary(
        StringBuilder context,
        GeneratedWorldPlanRecord plan)
    {
        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region == null)
                continue;

            context.AppendLine(
                "- region " +
                region.regionId +
                ": " +
                region.displayName);
        }

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement == null)
                continue;

            context.AppendLine(
                "- settlement " +
                settlement.settlementId +
                " in " +
                settlement.regionId +
                ": " +
                settlement.displayName);
        }

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment == null)
                continue;

            context.AppendLine(
                "- encampment " +
                encampment.encampmentId +
                " in " +
                encampment.regionId +
                ": " +
                encampment.displayName);
        }
    }

    private static bool TryApplyBackgroundLoreRefresh(
        string raw,
        WorldState world,
        out string message)
    {
        message =
            string.Empty;

        if (world == null ||
            string.IsNullOrWhiteSpace(
                raw))
        {
            message =
                "empty response";

            return false;
        }

        string json =
            ExtractFirstJsonObject(
                raw);

        if (string.IsNullOrWhiteSpace(
                json))
        {
            message =
                "no JSON object";

            return false;
        }

        JObject root;

        try
        {
            root =
                JObject.Parse(
                    json);
        }
        catch (Exception ex)
        {
            message =
                ex.Message;

            return false;
        }

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        plan?.EnsureCollections();

        int applied =
            0;

        applied +=
            ApplyCanonLines(
                root["canonLines"],
                world);

        applied +=
            ApplyPoiLocations(
                root["poiLocations"],
                world,
                plan);

        applied +=
            ApplyWorldQuestHooks(
                root["worldQuestHooks"],
                world,
                plan);

        applied +=
            ApplyNotableObjects(
                root["notableObjects"],
                world);

        if (applied <= 0)
        {
            message =
                "no additive lore entries";

            return false;
        }

        world.globalFlags["world_lore_refresh_count"] =
            world.globalFlags.TryGetValue(
                "world_lore_refresh_count",
                out float count)
                ? count + 1f
                : 1f;

        world.TouchNow();

        message =
            "Applied non-destructive background world lore refresh: " +
            applied +
            " entries.";

        return true;
    }

    private static int ApplyCanonLines(
        JToken token,
        WorldState world)
    {
        JArray lines =
            token as JArray;

        if (lines == null)
            return 0;

        int applied =
            0;

        for (int i = 0;
             i < lines.Count &&
             i < 4;
             i++)
        {
            string line =
                TokenString(
                    lines[i],
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    line))
            {
                continue;
            }

            // note: AppendCanon already deduplicates and keeps the ledger bounded.
            world.AppendCanon(
                TrimTo(
                    line,
                    220),
                96);

            applied++;
        }

        return applied;
    }

    private static int ApplyPoiLocations(
        JToken token,
        WorldState world,
        GeneratedWorldPlanRecord plan)
    {
        JArray locations =
            token as JArray;

        if (locations == null)
            return 0;

        int applied =
            0;

        for (int i = 0;
             i < locations.Count &&
             i < 3;
             i++)
        {
            JObject item =
                locations[i] as JObject;

            if (item == null)
                continue;

            string regionId =
                ResolveExistingRegionId(
                    plan,
                    TokenString(
                        item["regionId"],
                        string.Empty));

            if (string.IsNullOrWhiteSpace(
                    regionId))
            {
                continue;
            }

            string name =
                TrimTo(
                    TokenString(
                        item["name"],
                        "Generated POI"),
                    72);

            string locationId =
                SafeId(
                    TokenString(
                        item["locationId"],
                        string.Empty),
                    "poi",
                    name,
                    regionId +
                    ":background_lore:" +
                    i);

            UpsertLocation(
                world,
                locationId,
                regionId,
                name,
                TrimTo(
                    TokenString(
                        item["description"],
                        "Additive generated point of interest."),
                    220),
                TrimTo(
                    TokenString(
                        item["state"],
                        "generated_poi"),
                    80),
                Mathf.Clamp(
                    TokenFloat(
                        item["importance"],
                        0.4f),
                    0.1f,
                    1f),
                TrimTo(
                    TokenString(
                        item["text"],
                    string.Empty),
                    260));

            UpsertPointOfInterestRecord(
                plan,
                locationId,
                regionId,
                name,
                item);

            AddLandmarksToRegion(
                plan,
                regionId,
                item["landmarkIds"]);

            AddWorldQuestHooksToLocation(
                plan,
                locationId,
                item["questHookIds"]);

            world.AppendCanon(
                "POI added in " +
                regionId +
                ": " +
                name +
                ".",
                96);

            applied++;
        }

        return applied;
    }

    private static int ApplyWorldQuestHooks(
        JToken token,
        WorldState world,
        GeneratedWorldPlanRecord plan)
    {
        JArray hooks =
            token as JArray;

        if (hooks == null)
            return 0;

        int applied =
            0;

        for (int i = 0;
             i < hooks.Count &&
             i < 4;
             i++)
        {
            JObject hook =
                hooks[i] as JObject;

            if (hook == null)
                continue;

            string hookId =
                NormalizeId(
                    TokenString(
                        hook["hookId"],
                        string.Empty));

            string locationId =
                NormalizeId(
                    TokenString(
                        hook["locationId"],
                        string.Empty));

            string summary =
                TrimTo(
                    TokenString(
                        hook["summary"],
                        string.Empty),
                    220);

            if (string.IsNullOrWhiteSpace(
                    hookId) ||
                string.IsNullOrWhiteSpace(
                    locationId) ||
                string.IsNullOrWhiteSpace(
                    summary))
            {
                continue;
            }

            AddWorldQuestHookToPlan(
                plan,
                locationId,
                hookId);

            UpsertWorldQuestHookRecord(
                plan,
                hookId,
                locationId,
                summary,
                hook);

            world.AppendCanon(
                "World quest hook " +
                hookId +
                " at " +
                locationId +
                ": " +
                summary,
                96);

            applied++;
        }

        return applied;
    }

    private static int ApplyNotableObjects(
        JToken token,
        WorldState world)
    {
        JArray objects =
            token as JArray;

        if (objects == null)
            return 0;

        int applied =
            0;

        for (int i = 0;
             i < objects.Count &&
             i < 4;
             i++)
        {
            JObject item =
                objects[i] as JObject;

            if (item == null)
                continue;

            string objectId =
                NormalizeId(
                    TokenString(
                        item["objectId"],
                        string.Empty));

            string locationId =
                NormalizeId(
                    TokenString(
                        item["locationId"],
                        string.Empty));

            string objectType =
                NormalizeKey(
                    TokenString(
                        item["objectType"],
                        "object"));

            string summary =
                TrimTo(
                    TokenString(
                        item["summary"],
                        string.Empty),
                    220);

            if (string.IsNullOrWhiteSpace(
                    objectId) ||
                string.IsNullOrWhiteSpace(
                    summary))
            {
                continue;
            }

            // note: Notable object/item rumors are canon first; item instantiation remains a separate gameplay system.
            world.AppendCanon(
                "Notable " +
                objectType +
                " " +
                objectId +
                (string.IsNullOrWhiteSpace(
                    locationId)
                    ? string.Empty
                    : " near " + locationId) +
                ": " +
                summary,
                96);

            UpsertNotableObjectRecord(
                world.generatedWorldPlan,
                objectId,
                locationId,
                objectType,
                summary,
                item);

            applied++;
        }

        return applied;
    }

    private static void UpsertPointOfInterestRecord(
        GeneratedWorldPlanRecord plan,
        string poiId,
        string regionId,
        string displayName,
        JObject source)
    {
        if (plan == null ||
            string.IsNullOrWhiteSpace(
                poiId))
        {
            return;
        }

        plan.EnsureCollections();

        GeneratedPointOfInterestRecord record =
            null;

        for (int i = 0;
             i < plan.pointsOfInterest.Count;
             i++)
        {
            GeneratedPointOfInterestRecord candidate =
                plan.pointsOfInterest[i];

            if (candidate != null &&
                string.Equals(
                    candidate.poiId,
                    poiId,
                    StringComparison.OrdinalIgnoreCase))
            {
                record =
                    candidate;

                break;
            }
        }

        if (record == null)
        {
            record =
                new GeneratedPointOfInterestRecord();

            plan.pointsOfInterest.Add(
                record);
        }

        record.poiId =
            poiId;

        record.regionId =
            regionId;

        record.displayName =
            displayName;

        record.kind =
            NormalizeKey(
                TokenString(
                    source["state"],
                    "poi"));

        record.lore =
            TrimTo(
                TokenString(
                    source["description"],
                    record.lore),
                320);

        record.gameplayHook =
            TrimTo(
                TokenString(
                    source["text"],
                    record.gameplayHook),
                180);

        record.EnsureCollections();
    }

    private static void UpsertWorldQuestHookRecord(
        GeneratedWorldPlanRecord plan,
        string hookId,
        string locationId,
        string summary,
        JObject source)
    {
        if (plan == null ||
            string.IsNullOrWhiteSpace(
                hookId))
        {
            return;
        }

        plan.EnsureCollections();

        GeneratedWorldQuestHookRecord record =
            null;

        for (int i = 0;
             i < plan.worldQuestHooks.Count;
             i++)
        {
            GeneratedWorldQuestHookRecord candidate =
                plan.worldQuestHooks[i];

            if (candidate != null &&
                string.Equals(
                    candidate.hookId,
                    hookId,
                    StringComparison.OrdinalIgnoreCase))
            {
                record =
                    candidate;

                break;
            }
        }

        if (record == null)
        {
            record =
                new GeneratedWorldQuestHookRecord();

            plan.worldQuestHooks.Add(
                record);
        }

        record.hookId =
            hookId;

        record.locationId =
            locationId;

        record.displayName =
            TrimTo(
                TokenString(
                    source["displayName"],
                    hookId),
                72);

        record.premise =
            summary;

        record.objectiveIntent =
            TrimTo(
                TokenString(
                    source["objectiveIntent"],
                    record.objectiveIntent),
                120);

        record.rewardIntent =
            TrimTo(
                TokenString(
                    source["rewardIntent"],
                    record.rewardIntent),
                120);

        record.EnsureCollections();
    }

    private static void UpsertNotableObjectRecord(
        GeneratedWorldPlanRecord plan,
        string objectId,
        string locationId,
        string objectType,
        string summary,
        JObject source)
    {
        if (plan == null ||
            string.IsNullOrWhiteSpace(
                objectId))
        {
            return;
        }

        plan.EnsureCollections();

        GeneratedNotableWorldObjectRecord record =
            null;

        for (int i = 0;
             i < plan.notableObjects.Count;
             i++)
        {
            GeneratedNotableWorldObjectRecord candidate =
                plan.notableObjects[i];

            if (candidate != null &&
                string.Equals(
                    candidate.objectId,
                    objectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                record =
                    candidate;

                break;
            }
        }

        if (record == null)
        {
            record =
                new GeneratedNotableWorldObjectRecord();

            plan.notableObjects.Add(
                record);
        }

        record.objectId =
            objectId;

        record.locationId =
            locationId;

        record.displayName =
            TrimTo(
                TokenString(
                    source["displayName"],
                    objectId),
                72);

        record.objectType =
            objectType;

        record.itemType =
            NormalizeKey(
                TokenString(
                    source["itemType"],
                    record.itemType));

        record.rarity =
            TrimTo(
                TokenString(
                    source["rarity"],
                    record.rarity),
                40);

        record.visualFamily =
            TrimTo(
                TokenString(
                    source["visualFamily"],
                    record.visualFamily),
                80);

        record.lore =
            summary;

        record.EnsureCollections();
    }

    private static string ResolveExistingRegionId(
        GeneratedWorldPlanRecord plan,
        string requestedRegionId)
    {
        if (plan == null ||
            plan.regions == null ||
            plan.regions.Count == 0)
        {
            return string.Empty;
        }

        string normalized =
            NormalizeId(
                requestedRegionId);

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region != null &&
                string.Equals(
                    NormalizeId(
                        region.regionId),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return region.regionId;
            }
        }

        // note: Invalid region anchors are rejected rather than remapped to a random place.
        return string.Empty;
    }

    private static void AddLandmarksToRegion(
        GeneratedWorldPlanRecord plan,
        string regionId,
        JToken token)
    {
        GeneratedRegionRecord region =
            FindRegion(
                plan,
                regionId);

        JArray ids =
            token as JArray;

        if (region == null ||
            ids == null)
        {
            return;
        }

        for (int i = 0;
             i < ids.Count &&
             i < 4;
             i++)
        {
            AddUnique(
                region.landmarkIds,
                NormalizeId(
                    TokenString(
                        ids[i],
                        string.Empty)));
        }
    }

    private static void AddLandmarksToRegion(
        GeneratedWorldPlanRecord plan,
        string regionId,
        List<string> ids)
    {
        GeneratedRegionRecord region =
            FindRegion(
                plan,
                regionId);

        if (region == null ||
            ids == null)
        {
            return;
        }

        for (int i = 0;
             i < ids.Count &&
             i < 6;
             i++)
        {
            // note: Typed plan records and JSON refresh records share the same landmark-linking behavior.
            AddUnique(
                region.landmarkIds,
                NormalizeId(
                    ids[i]));
        }
    }

    private static void AddWorldQuestHooksToLocation(
        GeneratedWorldPlanRecord plan,
        string locationId,
        JToken token)
    {
        JArray ids =
            token as JArray;

        if (ids == null)
            return;

        for (int i = 0;
             i < ids.Count &&
             i < 4;
             i++)
        {
            AddWorldQuestHookToPlan(
                plan,
                locationId,
                NormalizeId(
                    TokenString(
                        ids[i],
                        string.Empty)));
        }
    }

    private static void AddWorldQuestHooksToLocation(
        GeneratedWorldPlanRecord plan,
        string locationId,
        List<string> ids)
    {
        if (ids == null)
            return;

        for (int i = 0;
             i < ids.Count &&
             i < 6;
             i++)
        {
            // note: Initial plan records and background lore records attach quest hooks through one path.
            AddWorldQuestHookToPlan(
                plan,
                locationId,
                NormalizeId(
                    ids[i]));
        }
    }

    private static void AddWorldQuestHookToPlan(
        GeneratedWorldPlanRecord plan,
        string locationId,
        string hookId)
    {
        if (plan == null ||
            string.IsNullOrWhiteSpace(
                locationId) ||
            string.IsNullOrWhiteSpace(
                hookId))
        {
            return;
        }

        string normalizedLocation =
            NormalizeId(
                locationId);

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement != null &&
                string.Equals(
                    NormalizeId(
                        settlement.settlementId),
                    normalizedLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(
                    settlement.questHookIds,
                    hookId);

                return;
            }
        }

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment != null &&
                string.Equals(
                    NormalizeId(
                        encampment.encampmentId),
                    normalizedLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(
                    encampment.questHookIds,
                    hookId);

                return;
            }
        }
    }

    private static string TokenString(
        JToken token,
        string fallback)
    {
        return token != null &&
               token.Type != JTokenType.Null
            ? Safe(
                token.ToString(),
                fallback)
            : fallback;
    }

    private static float TokenFloat(
        JToken token,
        float fallback)
    {
        if (token == null ||
            token.Type == JTokenType.Null)
        {
            return fallback;
        }

        return float.TryParse(
            token.ToString(),
            out float value)
            ? value
            : fallback;
    }

    private static void UpsertLocation(WorldState world, string locationId, string regionId, string name, string description, string state, float importance, string text)
    {
        if (world == null || string.IsNullOrWhiteSpace(locationId))
            return;

        world.EnsureCollections();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < world.locations.Count; i++)
        {
            WorldState.LocationRecord record = world.locations[i];
            if (record == null || !string.Equals(record.locationId, locationId, StringComparison.OrdinalIgnoreCase))
                continue;

            record.regionId = Safe(regionId, record.regionId);
            record.name = Safe(name, record.name);
            record.description = Safe(description, record.description);
            record.state = Safe(state, record.state);
            record.importance = Mathf.Max(record.importance, importance);
            record.text = Safe(text, record.text);
            record.updatedUnix = now;
            world.locationStates[locationId] = record.state;
            world.locationImportance[locationId] = record.importance;
            return;
        }

        world.locations.Add(new WorldState.LocationRecord
        {
            locationId = locationId,
            regionId = Safe(regionId, world.currentRegionId),
            name = Safe(name, locationId),
            description = Safe(description, string.Empty),
            state = Safe(state, "generated"),
            importance = Mathf.Max(0f, importance),
            text = Safe(text, string.Empty),
            createdUnix = now,
            updatedUnix = now
        });
        world.locationStates[locationId] = Safe(state, "generated");
        world.locationImportance[locationId] = Mathf.Max(0f, importance);
    }

    private static void UpsertFaction(WorldState world, string factionId, string name, string description, string status, float attitude)
    {
        if (world == null || string.IsNullOrWhiteSpace(factionId))
            return;

        world.EnsureCollections();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < world.factions.Count; i++)
        {
            WorldState.FactionRecord record = world.factions[i];
            if (record == null || !string.Equals(record.factionId, factionId, StringComparison.OrdinalIgnoreCase))
                continue;

            record.name = Safe(name, record.name);
            record.description = Safe(description, record.description);
            record.status = Safe(status, record.status);
            record.attitudeToPlayer = Mathf.Clamp(attitude, -1f, 1f);
            record.updatedUnix = now;
            world.factionAttitudes[factionId] = record.attitudeToPlayer;
            return;
        }

        world.factions.Add(new WorldState.FactionRecord
        {
            factionId = factionId,
            name = Safe(name, factionId),
            description = Safe(description, string.Empty),
            status = Safe(status, "generated"),
            attitudeToPlayer = Mathf.Clamp(attitude, -1f, 1f),
            createdUnix = now,
            updatedUnix = now
        });
        world.factionAttitudes[factionId] = Mathf.Clamp(attitude, -1f, 1f);
    }

    private string BuildPrompt(PlayerState state, WorldState world, string seed)
    {
        StringBuilder recent = new StringBuilder();
        int requiredRegions =
            GetStartupLlmRegionCount();

        int requiredSettlements =
            GetStartupLlmSettlementCount(
                requiredRegions);

        int requiredEncampments =
            GetStartupLlmEncampmentCount(
                requiredRegions);

        recent.AppendLine("WORLD_PLAN_SEED: " + seed);
        recent.AppendLine("PLAYABLE_RUNTIME_TARGET: " + Mathf.Min(targetPlayableHoursMin, targetPlayableHoursMax) + "-" + Mathf.Max(targetPlayableHoursMin, targetPlayableHoursMax) + " hours before old details must roll into summaries.");
        recent.AppendLine(
    "REQUIRED_EXACT_WORLD_COUNTS");


        recent.AppendLine(
            "- regions: EXACTLY " +
            requiredRegions);

        recent.AppendLine(
            "- settlements: EXACTLY " +
            requiredSettlements);

        recent.AppendLine(
            "- enemy encampments: EXACTLY " +
            requiredEncampments);

        recent.AppendLine("- Exact counts are mandatory; complete every array before adding descriptive detail.");
        recent.AppendLine("PLAYER_ORIGIN");
        recent.AppendLine(BuildOriginBlock(state));
        // note: Raw questionnaire evidence is used only to enrich Goddess presentation, never world-plan authority.
        recent.AppendLine(
            YQGoddessLoadingVoice
                .BuildQuestionnaireContextForPrompt(
                    state));
        // note: The model sees only the current reviewed semantic allow-list; deferred packs and Unity paths can never enter generated canon.
        recent.AppendLine(BuildRuntimeSiteCapabilitiesForPrompt());
        recent.AppendLine("WORLD_RULES: unique seeded identities; one coherent style key per region; settlements have useful services and resident roles; hostile sites match their faction/monster/theme; output semantic intent, never Unity paths.");

        string task =
            // note: Startup asks the model only for identity-bearing world facts; deterministic normalization supplies mechanical boilerplate after acceptance.
            "Generate the deterministic canonical startup world for this save: exactly " +
            requiredRegions + " unique regions, " +
            requiredSettlements + " unique settlements, and " +
            requiredEncampments + " unique hostile sites. " +
            "Complete regions, settlements, encampments, and routes before optional goddessVoice. " +
            "Every settlement/site references one generated regionId; every settlement chooses one approved siteStyleIntent compatible with its region; every site faction matches its monster family and chosen regional style. " +
            "Each region needs distinct grid coordinates, terrain, lore, and one approved style key. " +
            "Generate all names and IDs from WORLD_PLAN_SEED and PLAYER_ORIGIN; never copy examples, fallbacks, or another world. " +
            "Omit unspecified boilerplate; deterministic normalization supplies it. Names are 1-4 words and every prose value is at most 8 words. " +
            "Do not emit POIs, quest hooks, Unity paths, markdown, commentary, or text outside the single JSON object.";

        string schema =
            PromptContextBuilder.WrapJsonSchema(
                "{" +
                "\"schemaVersion\":\"world_plan_v1\"," +
                "\"source\":\"llm_world_plan_v1\"," +
                "\"worldSeed\":\"" + seed + "\"," +
                "\"summary\":\"short world identity\"," +

                "\"regions\":[{" +
                "\"regionId\":\"stable_id\"," +
                "\"displayName\":\"generated name\"," +
                "\"gridX\":0," +
                "\"gridY\":0," +
                "\"terrainProfile\":\"terrain phrase\"," +
                "\"lore\":\"short lore\"," +
                "\"assetStyleKey\":\"approved_style_key\"" +
                "}]," +

                "\"settlements\":[{" +
                "\"settlementId\":\"stable_id\"," +
                "\"regionId\":\"stable_region_id\"," +
                "\"displayName\":\"generated name\"," +
                "\"kind\":\"town\"," +
                "\"gridX\":0," +
                "\"gridY\":0," +
                "\"siteStyleIntent\":\"approved semantic style key\"," +
                "\"marketBias\":\"short market\"," +
                "\"serviceSlots\":[\"service\",\"service\"]," +
                "\"residentRoles\":[\"merchant\",\"guard\",\"notable\"]" +
                "}]," +

                "\"encampments\":[{" +
                "\"encampmentId\":\"stable_id\"," +
                "\"regionId\":\"stable_region_id\"," +
                "\"displayName\":\"generated name\"," +
                "\"kind\":\"camp\"," +
                "\"gridX\":0," +
                "\"gridY\":0," +
                "\"siteStyleIntent\":\"approved semantic style key\"," +
                "\"inhabitantFactionId\":\"stable_faction_id\"," +
                "\"monsterFamily\":\"short enemy\"," +
                "\"layoutIntent\":\"short layout\"" +
                "}]," +

                "\"routes\":[{" +
                "\"routeId\":\"stable_id\"," +
                "\"fromRegionId\":\"stable_region_id\"," +
                "\"toRegionId\":\"stable_region_id\"," +
                "\"routeKind\":\"road\"," +
                "\"travelHook\":\"short travel\"" +
                "}]," +
                "\"goddessVoice\":{" +
                "\"completion\":\"reaction to the accepted world\"," +
                "\"terrain\":\"terrain materialization line\"," +
                "\"environment\":\"environment binding line\"," +
                "\"populationPrelude\":\"transition to inhabitants\"," +
                "\"populationMaterialization\":\"inhabitant placement line\"," +
                "\"reveal\":\"final handoff line\"," +
                "\"ambientLines\":[\"short grounded world-materialization thought\"]," +
                "\"locations\":[]" +
                "}" +
"}");
        return PromptContextBuilder.BuildContext(
            task +
            // note: Optional voice is transient and deliberately last; one shared contract prevents the world request from bypassing the established Goddess character and timing rules.
            YQGoddessGenerationDialogue.BuildWorldVoiceContract(
                requiredSettlements),
            schema,
            recent.ToString(),
            BuildLedger(state));
    }

    private static string BuildRuntimeSiteCapabilitiesForPrompt()
    {
        YQRuntimeWorldSiteCatalog catalog =
            Resources.Load<YQRuntimeWorldSiteCatalog>(
                "YQRuntimeWorldSiteCatalog");

        if (catalog == null || catalog.Sites.Count == 0)
        {
            return "APPROVED_WORLD_SITE_STYLES: unavailable; use the regional semantic style and let deterministic validation defer physical binding.";
        }

        StringBuilder capabilities = new StringBuilder(
            "APPROVED_WORLD_SITE_STYLES: ");

        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord site = catalog.Sites[index];

            if (site == null)
                continue;

            if (capabilities[capabilities.Length - 1] != ' ')
                capabilities.Append(", ");

            capabilities
                .Append(site.semanticStyleKey)
                .Append('|')
                .Append(site.siteKind)
                .Append('|')
                .Append(site.topology);
        }

        capabilities.Append(
            ". Choose semantic style keys only; never output kit IDs, resource keys, prefab names, or asset paths.");
        return capabilities.ToString();
    }

    private int GetStartupLlmRegionCount()
    {
        // note: The opening LLM transaction must fit a local 8GB-card budget; larger worlds can be enriched after play begins.
        return Mathf.Clamp(
            Mathf.Min(
                targetRegionCount,
                StartupLlmRegionCount),
            3,
            6);
    }

    private int GetStartupLlmSettlementCount(
        int requiredRegions)
    {
        // note: One or two settlements per opening region is enough to prove the world without overstuffing JSON.
        return Mathf.Clamp(
            Mathf.Min(
                targetSettlementCount,
                StartupLlmSettlementCount),
            2,
            Mathf.Max(
                2,
                requiredRegions + 2));
    }

    private int GetStartupLlmEncampmentCount(
        int requiredRegions)
    {
        // note: Hostile sites stay present at startup, but the full danger web belongs in staged generation.
        return Mathf.Clamp(
            Mathf.Min(
                targetEncampmentCount,
                StartupLlmEncampmentCount),
            2,
            Mathf.Max(
                2,
                requiredRegions + 3));
    }

    private static void SeparateHostileSitesFromSettlements(
        GeneratedWorldPlanRecord plan,
        string seed)
    {
        if (plan == null ||
            plan.encampments == null ||
            plan.settlements == null)
        {
            return;
        }

        const int minimumGridDistance = 6;
        int minimumDistanceSquared =
            minimumGridDistance *
            minimumGridDistance;

        for (int encampmentIndex = 0;
             encampmentIndex < plan.encampments.Count;
             encampmentIndex++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[encampmentIndex];

            if (encampment == null)
                continue;

            for (int settlementIndex = 0;
                 settlementIndex < plan.settlements.Count;
                 settlementIndex++)
            {
                GeneratedSettlementRecord settlement =
                    plan.settlements[settlementIndex];

                if (settlement == null)
                    continue;

                int deltaX =
                    encampment.gridX -
                    settlement.gridX;

                int deltaY =
                    encampment.gridY -
                    settlement.gridY;

                if (deltaX * deltaX + deltaY * deltaY >=
                    minimumDistanceSquared)
                {
                    continue;
                }

                int hash =
                    PositiveHash(
                        seed +
                        "|encampment_separation|" +
                        encampment.encampmentId +
                        "|" +
                        settlement.settlementId);

                int directionX =
                    (hash & 1) == 0 ? -1 : 1;

                int directionY =
                    (hash & 2) == 0 ? -1 : 1;

                // note: Grid-space separation maps to a substantial world-space buffer before runtime terrain placement.
                encampment.gridX =
                    settlement.gridX +
                    directionX *
                    minimumGridDistance;

                encampment.gridY =
                    settlement.gridY +
                    directionY *
                    minimumGridDistance;
            }
        }
    }

    private static bool IsUsablePlan(GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return false;
        plan.EnsureCollections();
        return !string.IsNullOrWhiteSpace(plan.worldSeed) &&
               plan.regions.Count >= 3 &&
               plan.settlements.Count >= 2 &&
               plan.encampments.Count >= 2;
    }

    private static bool IsLlmPlan(GeneratedWorldPlanRecord plan)
    {
        return plan != null && !string.IsNullOrWhiteSpace(plan.source) && plan.source.IndexOf("llm", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool ShouldRequestLlmPlan(GeneratedWorldPlanRecord plan)
    {
        if (!enableLlmWorldGeneration || LLMClient.Instance == null || _requestInFlight)
            return false;
        if (plan == null)
            return true;
        if (IsLlmPlan(plan))
            return false;
        return replaceDeterministicFallbackWithLlm;
    }
    private static string ResolveCanonicalDirectionForWorld(
    PlayerState state)
    {
        if (state == null ||
            state.generatedOrigin == null)
        {
            return string.Empty;
        }

        GeneratedOriginRecord origin =
            state.generatedOrigin;

        string rawDirection =
            Safe(
                origin.directionKey,
                string.Empty);

        string normalizedDirection =
            NormalizeKey(
                rawDirection);

        /*
         * Normal valid case.
         */
        if (IsAllowedOriginDirectionKey(
                normalizedDirection))
        {
            return normalizedDirection;
        }

        /*
         * Small models sometimes copy the schema alternatives literally:
         *
         * wanderer|stillness|custom
         *
         * That is NOT a valid canonical direction.
         *
         * If the committed stimulus itself names one valid direction,
         * prefer that because it is specific player evidence rather than
         * arbitrarily choosing the first schema alternative.
         */
        string normalizedStimulus =
            NormalizeKey(
                origin.stimulus);

        if (rawDirection.IndexOf(
                '|') >= 0)
        {
            if (IsAllowedOriginDirectionKey(
                    normalizedStimulus))
            {
                return normalizedStimulus;
            }

            /*
             * The value is ambiguous and the stimulus does not resolve it.
             * Do not silently choose one of several alternatives.
             */
            return "custom";
        }

        /*
         * Unknown single values remain custom rather than becoming part
         * of the deterministic world seed as malformed free text.
         */
        return "custom";
    }

    private static bool IsAllowedOriginDirectionKey(
        string value)
    {
        switch (NormalizeKey(
                    value))
        {
            case "merchant":
            case "lumberjack":
            case "hero":
            case "demonlord":
            case "arcanist":
            case "warden":
            case "wanderer":
            case "stillness":
            case "custom":
                return true;

            default:
                return false;
        }
    }
    private static string BuildWorldSeed(PlayerState state, WorldState world)
    {
        string player = state != null ? state.playerId : "player";
        string originSeed = state != null && state.generatedOrigin != null ? state.generatedOrigin.seed : string.Empty;
        string direction =
    ResolveCanonicalDirectionForWorld(
        state);
        string answers = state != null && state.originQuestionnaireAnswers != null ? string.Join("|", state.originQuestionnaireAnswers) : string.Empty;
        string worldName = world != null ? world.worldName : "YourQuest";
        return StableHex(player + "|" + worldName + "|" + originSeed + "|" + direction + "|" + answers);
    }

    private static string BuildOriginBlock(PlayerState state)
    {
        if (state == null)
            return "<missing player state>";

        state.EnsureCollections();
        GeneratedOriginRecord origin = state.generatedOrigin;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("playerId=" + Safe(state.playerId, "player"));
        sb.AppendLine("displayName=" + Safe(state.displayName, "The Player"));
        if (origin != null && !string.IsNullOrWhiteSpace(origin.directionKey))
        {
            sb.AppendLine("originSource=" + Safe(origin.source, "unknown"));
            sb.AppendLine("originSeed=" + Safe(origin.seed, "none"));
            sb.AppendLine(
    "directionKey=" +
    ResolveCanonicalDirectionForWorld(
        state));
            sb.AppendLine("stimulus=" + Safe(origin.stimulus, "unspecified"));
            sb.AppendLine("class=" + Safe(origin.className, "unspecified"));
            sb.AppendLine("title=" + Safe(origin.titleName, "unspecified"));
            sb.AppendLine("ability=" + Safe(origin.abilityName, "unspecified") + " (" + Safe(origin.abilityKind, "skill") + ")");
            sb.AppendLine("quest=" + Safe(origin.questName, "unspecified"));
        }
        else
        {
            sb.AppendLine("origin=<not committed yet; build neutral starter-adjacent world scaffold>");
        }

        return sb.ToString();
    }

    private static string BuildLedger(PlayerState state)
    {
        if (state == null || state.behaviorLedger == null || state.behaviorLedger.Count == 0)
            return "No behavior recorded for world planning yet.";

        int start = Mathf.Max(0, state.behaviorLedger.Count - 18);
        StringBuilder sb = new StringBuilder();
        for (int i = start; i < state.behaviorLedger.Count; i++)
            sb.AppendLine(state.behaviorLedger[i]);
        return sb.ToString();
    }

    private static string ExtractFirstJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string text = raw.Trim();
        int start = text.IndexOf('{');
        if (start < 0)
            return string.Empty;

        int end = FindRootObjectEnd(text, start);
        // note: Preserve a token-limited root through the response end so completed canon can survive a truncated final presentation field.
        return end > start
            ? text.Substring(start, end - start + 1)
            : text.Substring(start);
    }

    private static int FindRootObjectEnd(
        string text,
        int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < text.Length; i++)
        {
            char character = text[i];

            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (character == '\\')
                    escaped = true;
                else if (character == '"')
                    inString = false;

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{')
                depth++;
            else if (character == '}' && --depth == 0)
                return i;
        }

        return -1;
    }

    private static bool TryParseRootIgnoringBrokenGoddessVoice(
        string json,
        out JObject root,
        out string error)
    {
        root =
            null;

        error =
            string.Empty;

        try
        {
            root =
                JObject.Parse(
                    json);

            return true;
        }
        catch (Exception firstException)
        {
            // note: A malformed generated Goddess aside must not throw away a complete world plan.
            if (!TryRemoveJsonProperty(
                    json,
                    "goddessVoice",
                    out string repairedJson))
            {
                error =
                    firstException.Message;

                return false;
            }

            try
            {
                root =
                    JObject.Parse(
                        repairedJson);

                return true;
            }
            catch (Exception secondException)
            {
                error =
                    secondException.Message;

                return false;
            }
        }
    }

    private static void NormalizeGeneratedStringField(
        JArray records,
        string fieldName)
    {
        if (records == null || string.IsNullOrWhiteSpace(fieldName))
            return;

        for (int i = 0; i < records.Count; i++)
        {
            if (!(records[i] is JObject record) || !(record[fieldName] is JArray values))
                continue;

            string firstValue = string.Empty;
            for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (values[valueIndex].Type != JTokenType.String)
                    continue;

                firstValue = values[valueIndex].Value<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(firstValue))
                    break;
            }

            // note: Empty malformed arrays become an empty scalar and are repaired later by canonical ID normalization.
            record[fieldName] = firstValue;
        }
    }

    private static bool TryRemoveJsonProperty(
        string json,
        string propertyName,
        out string repairedJson)
    {
        repairedJson =
            json;

        if (string.IsNullOrWhiteSpace(
                json) ||
            string.IsNullOrWhiteSpace(
                propertyName))
        {
            return false;
        }

        string marker =
            "\"" +
            propertyName +
            "\"";

        int nameIndex =
            json.IndexOf(
                marker,
                StringComparison.Ordinal);

        if (nameIndex < 0)
            return false;

        int colonIndex =
            json.IndexOf(
                ':',
                nameIndex + marker.Length);

        if (colonIndex < 0)
            return false;

        int valueStart =
            colonIndex + 1;

        while (valueStart < json.Length &&
               char.IsWhiteSpace(
                   json[valueStart]))
        {
            valueStart++;
        }

        int valueEnd =
            FindJsonValueEnd(
                json,
                valueStart);

        int removeStart =
            nameIndex;

        while (removeStart > 0 &&
               char.IsWhiteSpace(
                   json[removeStart - 1]))
        {
            removeStart--;
        }

        if (removeStart > 0 &&
            json[removeStart - 1] == ',')
        {
            removeStart--;
        }

        if (valueEnd <= valueStart)
        {
            // note: Presentation is the final optional world property, so a token-limited voice tail is removed while completed regions, settlements, sites, routes, and factions remain authoritative.
            repairedJson =
                json.Substring(
                    0,
                    removeStart).TrimEnd();

            if (repairedJson.EndsWith(
                    ",",
                    StringComparison.Ordinal))
            {
                repairedJson =
                    repairedJson.Substring(
                        0,
                        repairedJson.Length - 1);
            }

            repairedJson +=
                "}";

            return true;
        }

        int removeEnd =
            valueEnd;

        while (removeEnd < json.Length &&
               char.IsWhiteSpace(
                   json[removeEnd]))
        {
            removeEnd++;
        }

        if (removeEnd < json.Length &&
            json[removeEnd] == ',')
        {
            removeEnd++;
        }

        // note: Remove only the optional presentation property, leaving canonical world fields intact.
        repairedJson =
            json.Remove(
                removeStart,
                removeEnd - removeStart);

        return true;
    }

    private static int FindJsonValueEnd(
        string json,
        int start)
    {
        if (start < 0 ||
            start >= json.Length)
        {
            return -1;
        }

        char open =
            json[start];

        if (open != '{' &&
            open != '[')
        {
            int simpleEnd =
                start;

            while (simpleEnd < json.Length &&
                   json[simpleEnd] != ',' &&
                   json[simpleEnd] != '}')
            {
                simpleEnd++;
            }

            return simpleEnd;
        }

        char close =
            open == '{'
                ? '}'
                : ']';

        bool inString =
            false;

        bool escaped =
            false;

        int depth =
            0;

        for (int i = start;
             i < json.Length;
             i++)
        {
            char c =
                json[i];

            if (escaped)
            {
                escaped =
                    false;

                continue;
            }

            if (c == '\\')
            {
                escaped =
                    inString;

                continue;
            }

            if (c == '"')
            {
                inString =
                    !inString;

                continue;
            }

            if (inString)
                continue;

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;

                if (depth <= 0)
                    return i + 1;
            }
        }

        return -1;
    }

    private static string ResolvePlayerAxis(PlayerState state, System.Random rng)
    {
        string direction =
    ResolveCanonicalDirectionForWorld(
        state);
        string stimulus = state != null && state.generatedOrigin != null ? NormalizeKey(state.generatedOrigin.stimulus) : string.Empty;
        string combined = direction + " " + stimulus;
        if (combined.Contains("merchant") || combined.Contains("trade")) return "trade";
        if (combined.Contains("lumber") || combined.Contains("craft") || combined.Contains("wood")) return "craft";
        if (combined.Contains("hero") || combined.Contains("blade") || combined.Contains("fight")) return "advance";
        if (combined.Contains("demon") || combined.Contains("dominion")) return "dominion";
        if (combined.Contains("spell") || combined.Contains("arcane") || combined.Contains("mage")) return "spellwork";
        if (combined.Contains("guard") || combined.Contains("protect")) return "guard";
        if (combined.Contains("still") || combined.Contains("wait") || combined.Contains("lazy")) return "stillness";
        if (combined.Contains("road") || combined.Contains("dash") || combined.Contains("wander")) return "mobility";
        if (combined.Contains("survive") || combined.Contains("forest") || combined.Contains("nature")) return "survival";
        return Pick(PressureAxes, rng);
    }

    private static string BuildOriginPhrase(PlayerState state, string fallbackAxis)
    {
        if (state != null && state.generatedOrigin != null && !string.IsNullOrWhiteSpace(state.generatedOrigin.stimulus))
            return TrimTo(state.generatedOrigin.stimulus, 120);
        return fallbackAxis;
    }

    private static string BuildFallbackName(System.Random rng)
    {
        return Pick(FallbackSyllablesA, rng) + Pick(FallbackSyllablesB, rng);
    }

    private static string BuildClimatePhrase(string a, string b, System.Random rng)
    {
        return Pick(new[] { "cold", "mild", "wet", "dry", "wind-cut", "misty", "storm-prone", "seasonal" }, rng) + " " + a + "/" + b + " weather";
    }

    private static string BuildSettlementSuffix(string kind, System.Random rng)
    {
        switch (NormalizeKey(kind))
        {
            case "city": return Pick(new[] { "Crown", "Gate", "Concourse", "Seat" }, rng);
            case "town": return Pick(new[] { "Crossing", "Market", "Rest", "Hold" }, rng);
            case "outpost": return Pick(new[] { "Watch", "Post", "Picket", "Station" }, rng);
            case "hamlet": return Pick(new[] { "Hearth", "Fold", "Croft", "Nook" }, rng);
            default: return Pick(new[] { "Haven", "Bridge", "Hollow", "Yard" }, rng);
        }
    }

    private static string BuildEncampmentSuffix(string kind, System.Random rng)
    {
        switch (NormalizeKey(kind))
        {
            case "cave": return Pick(new[] { "Mouth", "Depths", "Grotto", "Cleft" }, rng);
            case "ruin": return Pick(new[] { "Ruin", "Remnant", "Broken Court", "Old Wall" }, rng);
            case "camp": return Pick(new[] { "Camp", "Redoubt", "Tents", "Claim" }, rng);
            case "mine": return Pick(new[] { "Mine", "Shaft", "Delve", "Cut" }, rng);
            case "crypt": return Pick(new[] { "Crypt", "Ossuary", "Sepulcher", "Vault" }, rng);
            default: return Pick(new[] { "Site", "Lair", "Nest", "Hold" }, rng);
        }
    }

    private static int ResolveServiceCount(string kind, System.Random rng)
    {
        switch (NormalizeKey(kind))
        {
            case "city": return rng.Next(6, 10);
            case "town": return rng.Next(3, 6);
            case "village": return rng.Next(2, 4);
            case "outpost": return rng.Next(1, 4);
            case "hamlet": return rng.Next(1, 3);
            default: return rng.Next(2, 5);
        }
    }

    private static int ResolvePopulation(string kind, System.Random rng)
    {
        switch (NormalizeKey(kind))
        {
            case "city": return rng.Next(50, 121);
            case "town": return rng.Next(18, 35);
            case "village": return rng.Next(12, 25);
            case "outpost": return rng.Next(8, 21);
            case "hamlet": return rng.Next(6, 15);
            default: return rng.Next(12, 40);
        }
    }

    private static int DefaultPopulation(string kind)
    {
        switch (NormalizeKey(kind))
        {
            case "city": return 64;
            case "town": return 24;
            case "village": return 16;
            case "outpost": return 12;
            case "hamlet": return 8;
            default: return 20;
        }
    }

    private static string BuildPopulationBand(string kind)
    {
        switch (NormalizeKey(kind))
        {
            case "city": return "larger localized population with many vendors and faction pressure";
            case "town": return "small localized population with several services and a local authority";
            case "village": return "small community with a few essentials and rumors";
            case "outpost": return "functional frontier crew with limited services";
            case "hamlet": return "tiny resident cluster with one or two useful services";
            default: return "localized generated population";
        }
    }

    private static void FillServices(List<string> services, int count, System.Random rng)
    {
        if (services == null)
            return;
        services.Clear();
        for (int i = 0; i < count; i++)
            AddUnique(services, Pick(ServiceKinds, rng));
    }

    private static void FillResidentRoles(List<string> roles, string kind, System.Random rng)
    {
        if (roles == null)
            return;
        roles.Clear();
        AddUnique(roles, Pick(new[] { "chief", "reeve", "speaker", "quartermaster", "mayor", "elder", "captain" }, rng));
        AddUnique(roles, "innkeeper");
        AddUnique(roles, "blacksmith");
        if (NormalizeKey(kind) == "city")
        {
            AddUnique(roles, "magistrate");
            AddUnique(roles, "guild broker");
            AddUnique(roles, "archive notary");
        }
        AddUnique(roles, Pick(new[] { "hunter", "healer", "merchant", "scout", "mason", "farmer", "locksmith", "scribe" }, rng));
    }

    private static void LinkSettlement(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        GeneratedRegionRecord region = FindRegion(plan, settlement.regionId);
        if (region != null)
            AddUnique(region.settlementIds, settlement.settlementId);
    }

    private static void LinkEncampment(GeneratedWorldPlanRecord plan, GeneratedEncampmentRecord encampment)
    {
        GeneratedRegionRecord region = FindRegion(plan, encampment.regionId);
        if (region != null)
            AddUnique(region.encampmentIds, encampment.encampmentId);
    }

    private static GeneratedRegionRecord FindRegion(GeneratedWorldPlanRecord plan, string regionId)
    {
        if (plan == null || string.IsNullOrWhiteSpace(regionId) || plan.regions == null)
            return null;
        for (int i = 0; i < plan.regions.Count; i++)
        {
            GeneratedRegionRecord region = plan.regions[i];
            if (region != null && string.Equals(region.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return region;
        }
        return null;
    }

    private static string SafeRegionId(GeneratedWorldPlanRecord plan, string regionId, int index)
    {
        if (FindRegion(plan, regionId) != null)
            return regionId.Trim();
        if (plan != null && plan.regions != null && plan.regions.Count > 0)
            return plan.regions[Mathf.Abs(index) % plan.regions.Count].regionId;
        return "region_unknown";
    }

    private static string SafeId(string value, string prefix, string displayName, string seed)
    {
        return string.IsNullOrWhiteSpace(value) ? MakeId(prefix, displayName, seed) : NormalizeId(value);
    }

    private static string MakeId(string prefix, string displayName, string seed)
    {
        return NormalizeId(Safe(prefix, "id") + "_" + Safe(displayName, "generated") + "_" + StableHex(seed).Substring(0, 8));
    }

    private static string NormalizeId(string value)
    {
        string normalized = NormalizeKey(value);
        return string.IsNullOrWhiteSpace(normalized) ? "generated_" + StableHex(value).Substring(0, 8) : normalized;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string lower = value.Trim().ToLowerInvariant();
        StringBuilder sb = new StringBuilder(lower.Length);
        bool lastUnderscore = false;
        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            bool ok = char.IsLetterOrDigit(c);
            if (ok)
            {
                sb.Append(c);
                lastUnderscore = false;
            }
            else if (!lastUnderscore)
            {
                sb.Append('_');
                lastUnderscore = true;
            }
        }
        return sb.ToString().Trim('_');
    }

    private static string StableHex(string text)
    {
        return PositiveHash(text).ToString("x8");
    }

    private static int PositiveHash(string text)
    {
        unchecked
        {
            int hash = 23;
            string value = text ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash & 0x7fffffff;
        }
    }

    private static T Pick<T>(IReadOnlyList<T> values, System.Random rng)
    {
        if (values == null || values.Count == 0)
            return default;
        return values[Mathf.Abs(rng.Next()) % values.Count];
    }

    private static string PickDistinct(IReadOnlyList<string> values, string first, System.Random rng)
    {
        if (values == null || values.Count == 0)
            return string.Empty;
        for (int i = 0; i < 8; i++)
        {
            string value = Pick(values, rng);
            if (!string.Equals(value, first, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return Pick(values, rng);
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value))
            return;
        string clean = value.Trim();
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], clean, StringComparison.OrdinalIgnoreCase))
                return;
        }
        list.Add(clean);
    }

    private static void TrimList(List<string> values, int maxCount, int maxChars)
    {
        if (values == null)
            return;
        for (int i = values.Count - 1; i >= 0; i--)
        {
            string value = TrimTo(values[i], maxChars);
            if (string.IsNullOrWhiteSpace(value))
                values.RemoveAt(i);
            else
                values[i] = value;
        }
        while (maxCount > 0 && values.Count > maxCount)
            values.RemoveAt(values.Count - 1);
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string TrimTo(string value, int max)
    {
        string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (max <= 0 || clean.Length <= max)
            return clean;
        return clean.Substring(0, max).TrimEnd();
    }

    private static string TruncateForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        const int maxChars = 1600;
        if (value.Length <= maxChars)
            return value;

        // note: Keep rejection diagnostics bounded so world generation cannot lock the editor with log volume.
        return value.Substring(0, maxChars) +
               "\n... <truncated " +
               (value.Length - maxChars) +
               " chars>";
    }
}
