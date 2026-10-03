using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Explicit incremental intake builds project-owned shells and publishes into the existing lazy registry. Source GLBs, metas and accepted saves are untouched.
[InitializeOnLoad]
public static class YQDotCreatureIntake
{
    private const string Root = "Assets/Assets/GeneratedAssets/DOT Generated Assets";
    private const string PrefabRoot = "Assets/YourQuest DOT Creatures";
    private const string CatalogPath = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Request = "Assets/Assets/EditorBuildRequests/BuildDotCreatures.request";
    private const string VerifyRequest = "Assets/Assets/EditorBuildRequests/VerifyDotCreatures.request";
    private const string Receipt = "outputs/DOT_Integration_20261003/creature-intake.json";
    private const string NpcRequest = "Assets/Assets/EditorBuildRequests/BuildDotNpcs.request";
    private const string OrcRoot = Root + "/NPCs/Races/YourQuest_Orc_Modular_Race_Kit_2026-10-03/NPCs/OrcKit";
    private const string NpcOutput = "outputs/DOT_Integration_20261003/NPCs";
    private static string _receiptPath = Receipt;
    private static bool _npcOnly;
    private static List<YQDotCreatureEntry> _jobs;
    private static int _index;
    private static double _next;
    static YQDotCreatureIntake()
    {
        EditorApplication.update += Tick;
        // note: Source compilation can interrupt a bounded intake. Requeue its explicit operation across reload instead of leaving a stale RUNNING receipt.
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (_jobs == null) return;
            WriteReceipt("INTERRUPTED", "Editor assembly reload; explicit intake will resume with stable prefab GUIDs.");
            File.WriteAllText(_npcOnly ? NpcRequest : Request, "Resume the explicitly requested DOT creature intake after assembly reload.");
        };
    }

    [MenuItem("YourQuest/DOT Assets/Build Races Monsters and Wildlife")]
    public static void Begin()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("DOT creature intake requires stable, idle Edit Mode.");
        if (!YQRuntimeWorldAssetRegistry.Instance.UsesLazyResourceShards) throw new InvalidOperationException("The existing lazy registry is required.");
        _jobs = ReadJobs(); _index = 0;
        _receiptPath = Receipt; _npcOnly = false;
        if (_jobs.Count == 0) throw new InvalidDataException("No declared DOT creatures.");
        WriteReceipt("RUNNING", null);
    }

    [MenuItem("YourQuest/DOT Assets/Build Newly Added NPC Assets")]
    public static void BeginNpcs()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("NPC intake requires stable, idle Edit Mode.");
        if (!YQRuntimeWorldAssetRegistry.Instance.UsesLazyResourceShards) throw new InvalidOperationException("The existing lazy registry is required.");
        var jobs = ReadOrcJobs(); SetPrefabPaths(jobs);
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Build the existing creature catalog first.");
        Directory.CreateDirectory(NpcOutput);
        var ids = new HashSet<string>(jobs.Select(j => j.assetId), StringComparer.Ordinal);
        // note: Snapshot the existing catalog and prefab GUIDs; the bounded NPC operation leaves all previous creature wrappers and crafting/equipment assets intact.
        File.WriteAllText(NpcOutput + "/preserved-bindings.json", JsonConvert.SerializeObject(catalog.entries.Where(e => !ids.Contains(e.assetId)).Select(e => new {
            e.assetId, json = JsonConvert.SerializeObject(e), guid = AssetDatabase.AssetPathToGUID(e.prefabPath)
        }), Formatting.Indented));
        _jobs = jobs.OrderBy(j => j.assetId, StringComparer.Ordinal).ToList(); _index = 0; _npcOnly = true; _receiptPath = NpcOutput + "/intake.json";
        WriteReceipt("RUNNING", null);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        _next = EditorApplication.timeSinceStartup + .1d;
        try
        {
            if (_jobs == null)
            {
                if (File.Exists(NpcRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
                { ConsumeRequest(NpcRequest); BeginNpcs(); return; }
                if (File.Exists(VerifyRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ConsumeRequest(VerifyRequest); RepairOwnedClipNames(); Verify(); YQDotCreaturePreview.Run();
                    var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
                    File.WriteAllText(Receipt, JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, processed = catalog.entries.Count, total = catalog.entries.Count,
                        entries = catalog.entries, error = (string)null, evidence = "Published intake reverified in Editor fixtures and representative rendered poses; ordinary gameplay remains unverified." }, Formatting.Indented));
                    return;
                }
                if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
                { ConsumeRequest(Request); Begin(); }
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode interrupted creature intake.");
            // note: One assembly/module per tick bounds progress and avoids a long modal batch.
            Build(_jobs[_index++]);
            if (_index % 10 == 0) WriteReceipt("RUNNING", null);
            if (_index == _jobs.Count)
            {
                Publish();
                if (_npcOnly)
                {
                    VerifyEntries("orc", NpcOutput + "/verification.json"); VerifyPreservedNpcs();
                    var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
                    YQDotCreaturePreview.RenderEntries(catalog.entries.Where(e => e.species == "orc" && string.IsNullOrEmpty(e.moduleSlot)), NpcOutput + "/Preview");
                }
                else { YQDotEquipmentIntake.RebindMovedSources(); Verify(); }
                WriteReceipt("PASS", null); _jobs = null;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (_jobs != null) WriteReceipt("FAIL", exception.ToString());
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Receipt));
                File.WriteAllText("outputs/DOT_Integration_20261003/creature-review-error.json", JsonConvert.SerializeObject(new { status = "FAIL", utc = DateTime.UtcNow, error = exception.ToString() }, Formatting.Indented));
            }
            _jobs = null;
        }
    }

    private static void ConsumeRequest(string path)
    {
        // note: Explicit request assets are disposable; remove their corresponding metadata together to avoid orphaned-meta diagnostics.
        if (!AssetDatabase.DeleteAsset(path)) { File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); }
    }

    private static List<YQDotCreatureEntry> ReadJobs()
    {
        var jobs = new List<YQDotCreatureEntry>();
        string elfRoot = Root + "/NPCs/Races/YourQuest_Elf_Modular_Race_Kit_2026-10-03 (1)/NPCs";
        JObject elf = Read(elfRoot + "/npc_manifest_v2.json");
        foreach (JObject row in (JArray)elf["assets"])
            Add(jobs, elfRoot, (string)row["id"], "race", "elf", "elf", (string)row["body_form"],
                "humanoid_gracile_v1_elf_" + (string)row["body_form"], null, .78f, (JArray)row["levels"]);
        foreach (JObject row in (JArray)elf["modules"])
            Add(jobs, elfRoot, (string)row["asset_id"], "module", "elf", "elf", (string)row["body_form"],
                (string)row["compatibility_id"], (string)row["slot"], 0f, new JArray(row));

        string lizardRoot = Root + "/NPCs/Races/YourQuest_Lizardmen_Modular_Race_Kit_2026-10-03/YourQuest_Lizardmen";
        JObject lizard = Read(lizardRoot + "/lizardmen_release_manifest.json");
        AddGrouped(jobs, lizardRoot, (JArray)lizard["files"], "race", "lizardmen", "lizardmen", .79485714f);
        foreach (string manifest in new[] { "lizardman_affinity_manifest.json", "lizardman_broad_affinity_manifest.json" })
            foreach (JObject row in (JArray)Read(lizardRoot + "/Validation/" + manifest)["modules"])
            {
                var copy = (JObject)row.DeepClone(); copy["file"] = "Modules/" + (string)row["file"];
                Add(jobs, lizardRoot, (string)row["id"], "module", "lizardmen", "lizardmen", null,
                    (string)row["compatibility_id"], (string)row["slot"], 0f, new JArray(copy));
            }

        string enemyRoot = Root + "/Monsters/YourQuest_Modular_Enemies_Batch01_2026-10-03/YourQuest_Enemies";
        JObject enemies = Read(enemyRoot + "/RELEASE_SUMMARY.json");
        JObject construction = Read(enemyRoot + "/construction_manifest.json");
        AddGrouped(jobs, enemyRoot, (JArray)enemies["files"], "monster", null, null, .8f);
        foreach (var job in jobs.Where(j => j.kind == "monster")) job.category = (string)construction["species"]?[job.species];
        foreach (JObject row in (JArray)Read(enemyRoot + "/module_manifest.json")["modules"])
            Add(jobs, enemyRoot, (string)row["id"], "module", (string)row["species"], (string)construction["species"]?[((string)row["species"])],
                (string)row["body"], (string)row["compatibility_id"], "affinity_set", 0f, new JArray(row));

        string rabbitRoot = Root + "/Wildlife/YourQuest_Wildlife_Batch01_Rabbit_2026-10-03/YourQuest_Rabbit/Wildlife";
        AddWildlife(jobs, rabbitRoot, "rabbit");
        string herdRoot = Root + "/Wildlife/YourQuest_Wildlife_Batch02_Horse_Cow_Stag_2026-10-03/Wildlife";
        foreach (string species in new[] { "horse", "cow", "deer" }) AddWildlife(jobs, herdRoot, species);

        jobs.AddRange(ReadOrcJobs());
        if (File.Exists(YQDotAssetLayout.Resolve(SatyrRoot + "/kit_manifest.json"))) jobs.AddRange(ReadSatyrJobs());
        SetPrefabPaths(jobs);
        return jobs.OrderBy(j => j.assetId, StringComparer.Ordinal).ToList();
    }

    private static List<YQDotCreatureEntry> ReadOrcJobs()
    {
        var manifest = Read(OrcRoot + "/orc_manifest.json");
        if ((string)manifest["schema"] != "yourquest.orc-kit.v1") throw new InvalidDataException("Unsupported orc NPC contract.");
        float speed = (float?)manifest["motion_contract"]?["steady_speed_m_s"] ?? throw new InvalidDataException("Orc gait speed is missing.");
        var jobs = new List<YQDotCreatureEntry>();
        foreach (JObject row in (JArray)manifest["assets"])
        {
            var levels = new JArray(((JArray)row["levels"]).OrderBy(level => (int)level["level"]));
            if (levels.Count != 4) throw new InvalidDataException("Orc assembly requires four delivered LODs.");
            Add(jobs, OrcRoot, (string)row["name"], "race", "orc", "orc", (string)row["body_form"], (string)row["fit_id"], null, speed, levels);
        }
        foreach (JObject row in (JArray)manifest["modules"])
        {
            Add(jobs, OrcRoot, (string)row["id"], "module", "orc", "orc", (string)row["body_form"], (string)row["fit_id"], (string)row["slot"], 0f, new JArray(row));
            // note: These exports are exact-fit replacement options. Their complete bodies batch skin/materials, so retain the parts without allowing an overlapping additive swap.
            jobs[jobs.Count - 1].requiresSlotReplacement = true;
        }
        if (jobs.Count != 34) throw new InvalidDataException("Incomplete orc kit; expected four assemblies and 30 modules.");
        return jobs;
    }

    private const string SatyrRoot = Root + "/NPCs/Races/YourQuest_Satyr_Modular_Race_Kit_2026-10-03/SatyrKit";
    private static List<YQDotCreatureEntry> ReadSatyrJobs()
    {
        // note: Bind complete authored variants and exact-form replacement horns; the delivered displacement recipes share the existing sampled gait owner.
        var manifest = Read(SatyrRoot + "/kit_manifest.json");
        if ((string)manifest["schema"] != "yourquest.satyr-kit.v1") throw new InvalidDataException("Unsupported satyr kit.");
        var files = ((JArray)manifest["files"]).OfType<JObject>().ToDictionary(f => (string)f["path"], StringComparer.Ordinal);
        var jobs = new List<YQDotCreatureEntry>();
        foreach (string id in new[] { "SatyrV2", "SatyrV2_Broad", "SatyrV2_Darklight", "SatyrV2_Emberstone" })
        {
            bool broad = id == "SatyrV2_Broad" || id == "SatyrV2_Emberstone";
            var motion = Read(SatyrRoot + (broad ? "/broad_motion_build.json" : "/motion_build.json"));
            var clips = ((JArray)motion["clips"]).OfType<JObject>().ToDictionary(c => (string)c["name"]);
            var levels = new JArray(Enumerable.Range(0, 4).Select(l => { string path = "GLB/" + id + "_LOD" + l + ".glb"; return new JObject { ["file"] = path, ["sha256"] = files[path]["sha256"] }; }));
            Add(jobs, SatyrRoot, id, "race", "satyr", "satyr", broad ? "broad" : "standard", "caprine_biped_v2_" + (broad ? "broad" : "standard"), null, (float)motion["speed_m_s"], levels);
            jobs.Last().motionProfile = new YQDotMotionProfile {
                startDuration = (float)clips["walk_start"]["duration_s"], loopDuration = (float)clips["walk_loop"]["duration_s"], stopDuration = (float)clips["walk_stop"]["duration_s"],
                startDistance = clips["walk_start"]["actor_displacement"].Select(s => (float)s["forward_distance_m"]).ToArray(),
                loopDistance = clips["walk_loop"]["actor_displacement"].Select(s => (float)s["forward_distance_m"]).ToArray(),
                stopDistance = clips["walk_stop"]["actor_displacement"].Select(s => (float)s["forward_distance_m"]).ToArray()
            };
            if (!jobs.Last().motionProfile.IsValid) throw new InvalidDataException("Invalid satyr travel samples.");
        }
        foreach (JObject row in (JArray)Read(SatyrRoot + "/affinity_modules.json")["modules"])
        {
            var copy = (JObject)row.DeepClone(); copy["sha256"] = files[(string)row["file"]]["sha256"];
            Add(jobs, SatyrRoot, (string)row["id"], "module", "satyr", "satyr", (string)row["body_form"], (string)row["rig_family"], (string)row["slot"], 0f, new JArray(copy));
            jobs.Last().requiresSlotReplacement = true;
        }
        if (jobs.Count != 28) throw new InvalidDataException("Incomplete satyr kit.");
        return jobs;
    }

    internal static void BuildSatyrs()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Satyr intake requires idle Edit Mode.");
        var jobs = ReadSatyrJobs(); SetPrefabPaths(jobs); _jobs = jobs;
        try { foreach (var job in jobs) Build(job); Publish(); VerifyEntries("satyr", "outputs/DOT_Integration_20261003/Patches/satyr-verification.json"); }
        finally { _jobs = null; }
    }

    private static void SetPrefabPaths(List<YQDotCreatureEntry> jobs)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var job in jobs)
        {
            if (!Regex.IsMatch(job.assetId ?? string.Empty, "^[a-zA-Z0-9_.-]+$") || !ids.Add(job.assetId)) throw new InvalidDataException("Invalid or duplicate creature ID: " + job.assetId);
            if (string.IsNullOrEmpty(job.category)) throw new InvalidDataException("Missing category: " + job.assetId);
            job.prefabPath = PrefabRoot + "/" + job.species + "/" + job.assetId + ".prefab";
        }
    }

    private static void AddWildlife(List<YQDotCreatureEntry> jobs, string root, string species)
    {
        JObject row = Read(root + "/Validation/" + species + "_production_manifest.json");
        Add(jobs, root, (string)row["asset_id"], "wildlife", species, "wildlife", null, (string)row["rig_family"], null,
            (float?)row["contact_gait"]?["loop_controller_speed_m_s"] ?? throw new InvalidDataException("Wildlife gait speed is missing: " + species), (JArray)row["lods"]);
        if ((species == "deer" && File.Exists(YQDotAssetLayout.Resolve(root + "/Release/stag_patch_manifest.json"))) ||
            ((species == "horse" || species == "cow") && File.Exists(YQDotAssetLayout.Resolve(root + "/Release/ungulate_patch_manifest.json"))))
        {
            var profiles = row["contact_gait"]["profiles"];
            var entry = jobs[jobs.Count - 1];
            // note: Import all three repaired travel curves together with their own rest/skin/clip revision; retain the existing movement and save owners.
            entry.motionProfile = new YQDotMotionProfile {
                startDuration = (float)profiles["locomotion_start"]["duration_s"], loopDuration = (float)profiles["locomotion_loop"]["duration_s"], stopDuration = (float)profiles["locomotion_stop"]["duration_s"],
                startDistance = profiles["locomotion_start"]["controller_root_y_m"].Values<float>().Select(value => -value).ToArray(),
                loopDistance = profiles["locomotion_loop"]["controller_root_y_m"].Values<float>().Select(value => -value).ToArray(),
                stopDistance = profiles["locomotion_stop"]["controller_root_y_m"].Values<float>().Select(value => -value).ToArray()
            };
            if (!entry.motionProfile.IsValid) throw new InvalidDataException("Invalid wildlife travel curves: " + species);
        }
    }

    private static void AddGrouped(List<YQDotCreatureEntry> jobs, string root, JArray files, string kind, string species, string category, float speed)
    {
        // note: Only delivered LOD assemblies enter runtime selection; old revisions, source files, previews and standalone parts cannot masquerade as complete bodies.
        foreach (var group in files.OfType<JObject>().Where(f => Regex.IsMatch((string)f["file"] ?? "", "^(Exports|LODs)/.*_LOD[0-3]\\.glb$"))
            .GroupBy(f => Regex.Replace(Path.GetFileName((string)f["file"]), "_LOD[0-3]\\.glb$", "")))
        {
            string name = group.Key;
            string resolvedSpecies = species ?? name.Split('_')[0];
            string body = kind == "monster" ? name.Split('_').Last() : name.Contains("Broad") || name.Contains("Emberstone") ? "broad" : "standard";
            string compatibility = kind == "monster" ? "yq_enemy_v1_" + resolvedSpecies + "_" + body :
                name == "lizardman_Refined" || name == "lizardman_Broad" ? "reptilian_biped_v2_" + body : null;
            Add(jobs, root, name, kind, resolvedSpecies, category, body, compatibility, null, speed,
                new JArray(group.OrderBy(f => (string)f["file"], StringComparer.Ordinal)));
        }
    }

    private static void Add(List<YQDotCreatureEntry> jobs, string root, string id, string kind, string species, string category,
        string body, string compatibility, string slot, float speed, JArray files)
    {
        var paths = new List<string>(); var hashes = new List<string>();
        foreach (JObject file in files)
        {
            string path = Path.GetFullPath(Path.Combine(root, (string)file["file"]));
            // note: Validate the declared pack boundary before resolving its exact GUID-preserving relocation.
            if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Missing or out-of-root declared creature: " + path);
            string relocated = YQDotAssetLayout.Resolve(path);
            if (!File.Exists(relocated)) throw new InvalidDataException("Missing declared creature: " + relocated);
            paths.Add(relocated); hashes.Add((string)file["sha256"]);
        }
        if (paths.Count == 0) throw new InvalidDataException("Empty creature exports: " + id);
        jobs.Add(new YQDotCreatureEntry { assetId = id, kind = kind, species = species, category = category, bodyForm = body,
            compatibilityId = compatibility, moduleSlot = slot, authoredWalkSpeed = speed, sourcePaths = paths.ToArray(), sourceHashes = hashes.ToArray() });
    }

    // note: Delivered manifest contents retain their pack-relative contracts after the source library is organized by type.
    private static JObject Read(string path) => JObject.Parse(File.ReadAllText(YQDotAssetLayout.Resolve(path)));
    internal static int VerifySourcePaths() => ReadJobs().Count;
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; Folder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }

    private static void Build(YQDotCreatureEntry job)
    {
        Folder(Path.GetDirectoryName(job.prefabPath).Replace('\\', '/'));
        Scene scene = EditorSceneManager.NewPreviewScene(); GameObject root = null;
        try
        {
            root = new GameObject(job.assetId); SceneManager.MoveGameObjectToScene(root, scene);
            var lods = new List<LOD>();
            for (int index = 0; index < job.sourcePaths.Length; index++)
            {
                string path = job.sourcePaths[index];
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(job.sourceHashes[index]) && !string.Equals(actual, job.sourceHashes[index], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Creature hash mismatch: " + path);
                    job.sourceHashes[index] = actual;
                }
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport); source = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
                if (source == null) throw new InvalidDataException("GLB did not import: " + path);
                GameObject model = UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(model, scene); model.transform.SetParent(root.transform, false); model.name = "LOD" + index;
                if (job.motionProfile != null && job.motionProfile.IsValid)
                {
                    // note: Measure the imported skeleton's body-to-head direction in wrapper space instead of assuming an exporter/engine forward axis.
                    var bones = model.GetComponentsInChildren<Transform>(true);
                    var head = bones.Single(t => t.name == (job.species == "satyr" ? "toe3-3.L" : "head")); var spine = bones.Single(t => t.name == (job.species == "satyr" ? "foot.L" : "spine"));
                    Vector3 forward = root.transform.InverseTransformVector(head.position - spine.position); forward.y = 0f;
                    if (forward.sqrMagnitude < .01f) throw new InvalidDataException("Cannot verify imported animal forward: " + path);
                    forward.Normalize();
                    if (index > 0 && Vector3.Dot(job.motionProfile.localForward, forward) < .999f) throw new InvalidDataException("Animal LOD forward axes disagree: " + path);
                    job.motionProfile.localForward = forward;
                }
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("Creature has no renderers: " + path);
                foreach (Renderer renderer in renderers)
                    foreach (Material material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name.Contains("InternalError")) throw new InvalidDataException("Unsupported creature material: " + path);
                if (string.IsNullOrEmpty(job.moduleSlot)) ConfigureAnimation(model, path, job.prefabPath.Replace(".prefab", "_LOD" + index + ".controller"), job.motionProfile != null && job.motionProfile.IsValid);
                else foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                lods.Add(new LOD(index == job.sourcePaths.Length - 1 ? .01f : index == 0 ? .55f : index == 1 ? .22f : .08f, renderers));
            }
            if (lods.Count > 1) { var group = root.AddComponent<LODGroup>(); group.SetLODs(lods.ToArray()); group.RecalculateBounds(); }
            if (PrefabUtility.SaveAsPrefabAsset(root, job.prefabPath) == null) throw new InvalidOperationException("Creature prefab publication failed.");
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void ConfigureAnimation(GameObject model, string sourcePath, string controllerPath, bool sampledGait = false)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        AnimationClip idle = clips.FirstOrDefault(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
        AnimationClip walk = clips.FirstOrDefault(c => c.name.IndexOf("walk_loop", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("locomotion_loop", StringComparison.OrdinalIgnoreCase) >= 0);
        if (idle == null || walk == null) throw new InvalidDataException("Missing own idle/locomotion clips: " + sourcePath);
        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidDataException("Imported creature has no Animator: " + sourcePath);
        // note: Build one controller per LOD's own bone paths, never retarget through the Human controller or rewrite imported clips.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        foreach (var layer in controller.layers) foreach (var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "DotMoving", type = AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name = "DotRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
            new AnimatorControllerParameter { name = "Attack", type = AnimatorControllerParameterType.Trigger } };
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("DotIdle"); idleState.motion = LoopClip(idle, controllerPath, "Idle"); machine.defaultState = idleState;
        var walkState = machine.AddState("DotWalk"); walkState.motion = LoopClip(walk, controllerPath, "Walk"); walkState.speedParameterActive = true; walkState.speedParameter = "DotRate";
        AnimationClip startClip = clips.FirstOrDefault(c => c.name.IndexOf("walk_start", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("locomotion_start", StringComparison.OrdinalIgnoreCase) >= 0);
        AnimationClip stopClip = clips.FirstOrDefault(c => c.name.IndexOf("walk_stop", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("locomotion_stop", StringComparison.OrdinalIgnoreCase) >= 0);
        if (startClip == null || stopClip == null) throw new InvalidDataException("Missing supplied start/stop clips: " + sourcePath);
        var startState = machine.AddState("DotStart"); startState.motion = startClip;
        var stopState = machine.AddState("DotStop"); stopState.motion = stopClip;
        var start = idleState.AddTransition(startState); start.hasExitTime = false; start.duration = .08f; start.AddCondition(AnimatorConditionMode.If, 0f, "DotMoving");
        var started = startState.AddTransition(walkState); started.hasExitTime = true; started.exitTime = 1f; started.duration = .02f;
        var cancel = startState.AddTransition(stopState); cancel.hasExitTime = false; cancel.duration = .08f; cancel.AddCondition(AnimatorConditionMode.IfNot, 0f, "DotMoving");
        var stop = walkState.AddTransition(stopState); stop.hasExitTime = false; stop.duration = .08f; stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "DotMoving");
        var stopped = stopState.AddTransition(idleState); stopped.hasExitTime = true; stopped.exitTime = 1f; stopped.duration = .02f;
        var resume = stopState.AddTransition(startState); resume.hasExitTime = false; resume.duration = .08f; resume.AddCondition(AnimatorConditionMode.If, 0f, "DotMoving");
        AnimationClip attack = clips.FirstOrDefault(c => c.name.EndsWith("_Attack", StringComparison.OrdinalIgnoreCase));
        if (attack != null)
        {
            var action = machine.AddState("DotAttack"); action.motion = attack;
            var trigger = machine.AddAnyStateTransition(action); trigger.hasExitTime = false; trigger.duration = .08f; trigger.canTransitionToSelf = false; trigger.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var recovery = action.AddTransition(idleState); recovery.hasExitTime = true; recovery.exitTime = 1f; recovery.duration = .08f;
        }
        AnimationClip greet = clips.FirstOrDefault(c => c.name.IndexOf("greet", StringComparison.OrdinalIgnoreCase) >= 0);
        if (greet != null) { var greeting = machine.AddState("DotGreet"); greeting.motion = greet; }
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.enabled = true;
        // note: The repaired stag's existing wander owner advances the explicit sampled phase; automatic blend transitions would apply a different travel/contact contract.
        if (sampledGait) foreach (var state in machine.states) state.state.transitions = Array.Empty<AnimatorStateTransition>();
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip LoopClip(AnimationClip source, string controllerPath, string suffix)
    {
        string path = controllerPath.Replace(".controller", "_" + suffix + ".anim");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        EditorUtility.CopySerialized(source, clip); clip.name = Path.GetFileNameWithoutExtension(path); clip.legacy = false;
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void RepairOwnedClipNames()
    {
        // note: Owned Unity clip assets use their filename as the object name, preventing name-mismatch diagnostics without editing delivered clips or curves.
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".anim", StringComparison.Ordinal)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip.name == Path.GetFileNameWithoutExtension(path)) continue;
            clip.name = Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(clip);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Publish()
    {
        var paths = new HashSet<string>(_jobs.Select(j => j.prefabPath), StringComparer.OrdinalIgnoreCase);
        foreach (var group in _jobs.GroupBy(j => "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(j.prefabPath) + ".asset"))
        {
            var shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(group.Key);
            if (shard == null) { Folder(Path.GetDirectoryName(group.Key).Replace('\\', '/')); shard = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>(); AssetDatabase.CreateAsset(shard, group.Key); }
            var entries = shard.Entries.Where(e => !paths.Contains(e.assetPath)).ToList();
            entries.AddRange(group.Select(j => new YQRuntimeWorldAssetEntry { assetPath = j.prefabPath, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(j.prefabPath) }));
            shard.SetEntries(entries); EditorUtility.SetDirty(shard);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<YQDotCreatureCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        var ids = new HashSet<string>(_jobs.Select(j => j.assetId), StringComparer.Ordinal);
        catalog.entries = catalog.entries.Where(e => !ids.Contains(e.assetId)).Concat(_jobs).ToList(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }

    [MenuItem("YourQuest/DOT Assets/Verify Races Monsters and Wildlife")]
    public static void Verify()
    { VerifyEntries(null, "outputs/DOT_Integration_20261003/creature-verification.json"); }

    private static void VerifyEntries(string speciesFilter, string output)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Creature catalog is missing.");
        var selected = catalog.entries.Where(e => speciesFilter == null || e.species == speciesFilter).ToArray();
        foreach (var entry in selected)
        {
            GameObject prefab = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath);
            if (prefab == null) throw new InvalidOperationException("Runtime registry cannot resolve: " + entry.assetId);
            string shardPath = "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(entry.prefabPath) + ".asset";
            var shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath);
            if (shard == null || !shard.Entries.Any(e => e.assetPath == entry.prefabPath && e.prefab == prefab)) throw new InvalidOperationException("Serialized NPC build reference missing: " + entry.assetId);
            if (string.IsNullOrEmpty(entry.moduleSlot))
            {
                LODGroup lod = prefab.GetComponent<LODGroup>();
                if (lod == null || lod.GetLODs().Length != entry.sourcePaths.Length) throw new InvalidOperationException("Missing creature LOD: " + entry.assetId);
                foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
                    if (animator.applyRootMotion || animator.runtimeAnimatorController == null) throw new InvalidOperationException("Invalid creature animation owner: " + entry.assetId);
            }
        }
        // note: Disposable body/module fixtures exercise the real shared-skeleton binder, including rejection and cleanup, without touching PlaySafe or saves.
        Scene preview = EditorSceneManager.NewPreviewScene();
        int moduleChecks = 0;
        int replacementChecks = 0, restFrameChecks = 0;
        try
        {
            foreach (var module in selected.Where(e => !string.IsNullOrEmpty(e.moduleSlot)))
            {
                var body = catalog.entries.FirstOrDefault(e => string.IsNullOrEmpty(e.moduleSlot) && !string.IsNullOrEmpty(e.compatibilityId) && e.compatibilityId == module.compatibilityId);
                if (body == null) continue;
                GameObject actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(body.prefabPath));
                SceneManager.MoveGameObjectToScene(actor, preview);
                try
                {
                    if (module.requiresSlotReplacement)
                    {
                        VerifyRestFrames(catalog, body, module); restFrameChecks += body.sourcePaths.Length;
                        if (!catalog.TryValidateModules(body.prefabPath, new[] { module.assetId }, out _) ||
                            catalog.TryAttachModules(actor, body.prefabPath, new[] { module.assetId }, out _, out _) ||
                            catalog.TryValidateModules(body.prefabPath, new[] { module.assetId, module.assetId }, out _))
                            throw new InvalidOperationException("Exact-fit replacement admission failed: " + module.assetId);
                        var other = catalog.entries.First(e => e.species == module.species && e.kind == "race" && e.compatibilityId != module.compatibilityId);
                        if (catalog.TryValidateModules(other.prefabPath, new[] { module.assetId }, out _)) throw new InvalidOperationException("Cross-body orc module was accepted.");
                        replacementChecks++; continue;
                    }
                    if (!catalog.TryAttachModules(actor, body.prefabPath, new[] { module.assetId }, out var attachment, out string reason))
                        throw new InvalidOperationException(module.assetId + ": " + reason);
                    if (catalog.TryValidateModules(body.prefabPath, new[] { module.assetId, module.assetId }, out _)) throw new InvalidOperationException("Duplicate module slot was accepted.");
                    UnityEngine.Object.DestroyImmediate(attachment); moduleChecks++;
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); }
            }
            foreach (var kind in new[] { "race", "monster", "wildlife" })
                foreach (string species in selected.Where(e => e.kind == kind).Select(e => e.species).Distinct())
                {
                    string semantic = species == "mimic" ? "DOT mimic" : species;
                    if (!YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, semantic, kind, "fixed-fixture-76603739", out var first, out _) ||
                        !YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, semantic, kind, "fixed-fixture-76603739", out var second, out _) || first.assetPath != second.assetPath)
                        throw new InvalidOperationException("Deterministic species binding failed: " + species);
                }
            if (YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, "rabbit", "monster", "seed", out _, out _) ||
                YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, "human", "race", "seed", out _, out _)) throw new InvalidOperationException("Legacy/passive semantic boundary failed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Directory.CreateDirectory(Path.GetDirectoryName(Receipt));
        File.WriteAllText(output, JsonConvert.SerializeObject(new {
            status = "PASS", utc = DateTime.UtcNow, entries = selected.Length, moduleChecks, replacementChecks, restFrameChecks,
            evidence = "Editor fixtures: registry/LOD/own-animation references, shared-skeleton attachment/rest frames, duplicate-slot rejection and fixed-seed semantic binding. Ordinary gameplay and live contact remain unverified."
        }, Formatting.Indented));
        Debug.Log("[YQ DOT Creatures] Editor reference/LOD/controller checks PASS: " + catalog.entries.Count + ". Ordinary gameplay and visual motion remain unverified.");
    }

    private static void VerifyRestFrames(YQDotCreatureCatalog catalog, YQDotCreatureEntry body, YQDotCreatureEntry module)
    {
        // note: Check each replacement's inverse-bind skeleton against every delivered LOD without spawning duplicate ears, teeth or armor on the accepted body.
        var source = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(module.prefabPath);
        var target = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(body.prefabPath);
        foreach (string level in Enumerable.Range(0, body.sourcePaths.Length).Select(i => "LOD" + i))
        {
            Transform rest = target.transform.Find(level);
            var bones = rest.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var bone in skin.bones)
            {
                if (bone == null || !bones.TryGetValue(bone.name, out var matching)) throw new InvalidDataException("Replacement bone unavailable: " + module.assetId);
                Matrix4x4 a = source.transform.worldToLocalMatrix * bone.localToWorldMatrix;
                Matrix4x4 b = rest.worldToLocalMatrix * matching.localToWorldMatrix;
                for (int component = 0; component < 16; component++)
                    if (Mathf.Abs(a[component] - b[component]) > .001f) throw new InvalidDataException("Replacement rest frame differs: " + module.assetId + " " + level);
            }
        }
    }

    private static void VerifyPreservedNpcs()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CatalogPath);
        foreach (JObject baseline in JArray.Parse(File.ReadAllText(NpcOutput + "/preserved-bindings.json")))
        {
            var entry = catalog.entries.FirstOrDefault(e => e.assetId == (string)baseline["assetId"]);
            if (entry == null || JsonConvert.SerializeObject(entry) != (string)baseline["json"] || AssetDatabase.AssetPathToGUID(entry.prefabPath) != (string)baseline["guid"])
                throw new InvalidOperationException("Previous creature binding changed: " + (string)baseline["assetId"]);
        }
    }

    internal static void BuildRepairedStag()
    { BuildRepairedWildlife(new[] { "deer" }); }

    internal static void BuildRepairedWildlife(string[] species)
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stag binding requires idle Edit Mode.");
        var jobs = new List<YQDotCreatureEntry>();
        foreach (string animal in species) AddWildlife(jobs, Root + "/Wildlife/YourQuest_Wildlife_Batch02_Horse_Cow_Stag_2026-10-03/Wildlife", animal);
        SetPrefabPaths(jobs); _jobs = jobs;
        try
        {
            // note: Rebuild only the patched stag's project-owned wrapper/controllers around freshly imported source data, keeping their existing GUIDs.
            foreach (var job in jobs) Build(job); Publish();
            foreach (string animal in species) VerifyEntries(animal, "outputs/DOT_Integration_20261003/Patches/" + animal + "-verification.json");
        }
        finally { _jobs = null; }
    }

    private static void WriteReceipt(string status, string error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_receiptPath));
        File.WriteAllText(_receiptPath, JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, processed = _index, total = _jobs?.Count ?? 0,
            entries = _jobs, error, evidence = "Editor GLB integrity, material, prefab, LOD, own-clip controller and registry verification. Runtime contact/terrain/combat/visible-flow acceptance is separate." }, Formatting.Indented));
    }
}
