using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// note: Exercise the production collision owner on an approved prefab in a disposable native terrain fixture; no authored asset or profile is modified.
internal static class YQReviewedMeshLodOwnershipChecks
{
    private const string Prefab = "Assets/Assets/GeneratedAssets/WorldAssemblies/NordicVillage/QualifiedShrine/yq_nordic_shrine.prefab";
    private const string Manifest = "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles/qualified_nordic_shrine/YQ_qualified_nordic_shrine_ReviewedSemanticSite.asset";
    [MenuItem("YourQuest/Terrain Refactor/Verify Reviewed Mesh LOD Ownership (Disposable)")]
    private static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Reviewed mesh ownership checks require idle Edit Mode.");
        string output = "outputs/TerrainRefactor_20261012/site_followup/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_mesh_ownership";
        Directory.CreateDirectory(output);
        var results = new List<object>(); int failures = 0;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject staging = null, native = null; TerrainData data = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Manifest);
            if (prefab == null || manifest == null) throw new InvalidOperationException("Approved shrine source is unavailable.");
            var source = manifest.Zones[0].cellContractsV2[0];
            var contract = JsonUtility.FromJson<YQReviewedCellFunctionContractV2>(JsonUtility.ToJson(source));
            var collect = typeof(YQGeneratedWorldIntegrityValidator).GetMethod("CollectReviewedMeshLodSupports", BindingFlags.NonPublic | BindingFlags.Static);
            var validate = typeof(YQGeneratedWorldIntegrityValidator).GetMethod("ValidateWalkableSurface", BindingFlags.NonPublic | BindingFlags.Static);
            staging = new GameObject("DISPOSABLE_StagedReviewedShrine");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging, scene);
            var cell = UnityEngine.Object.Instantiate(prefab, staging.transform);
            cell.transform.position = new Vector3(0f, 10f, 0f);
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(16f, 20f, 16f), hideFlags = HideFlags.HideAndDontSave };
            var heights = new float[33,33]; for (int z=0;z<33;z++) for(int x=0;x<33;x++) heights[z,x]=.5f;
            data.SetHeights(0,0,heights);
            native = new GameObject("DISPOSABLE_ReviewedContactTerrain");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(native, scene);
            native.transform.position = new Vector3(-8f,0f,-8f);
            var terrain = native.AddComponent<Terrain>(); terrain.terrainData = data;
            native.AddComponent<TerrainCollider>().terrainData = data;
            var cells = new Dictionary<GameObject,YQReviewedCellFunctionContractV2> { [cell] = contract };
            YQTerrainApproachV2.SampleHeight sample = (Vector3 p,out float h)=>YQTerrainApproachV2.TrySampleTerrain(terrain,p,out h);
            var approach = cell.transform.Find("Approach");
            var lods = approach.GetComponent<LODGroup>().GetLODs();
            var authored = approach.GetComponentInChildren<MeshCollider>();
            staging.SetActive(false); Physics.SyncTransforms();
            Func<Dictionary<Renderer,MeshCollider>> resolve = ()=> (Dictionary<Renderer,MeshCollider>)collect.Invoke(null,new object[]{staging,cells,sample});
            Action<string,Action> check=(name,test)=>{try{test();results.Add(new{id=name,passed=true});}catch(Exception e){failures++;results.Add(new{id=name,passed=false,error=e.ToString()});}};
            check("APPROVED-NATIVE-SOURCE-AND-STAGED-LOD-SHARING",()=>{
                Require(AssetDatabase.GetAssetDependencyHash(Prefab).ToString()==contract.sourceSignature,"Approved source signature is stale.");
                var owners=resolve(); var details=new List<string>(); int memberCount=0;
                foreach(var lod in lods) foreach(var renderer in lod.renderers)
                {details.Add(renderer==null?"<null>":renderer.name);if(renderer!=null)memberCount++;}
                Require(memberCount==4 && owners.Count==memberCount,"Not every actual visual LOD has the one reviewed owner. Owners="+owners.Count+"; members="+string.Join(",",details));
                foreach(var lod in lods) foreach(var renderer in lod.renderers) if(renderer!=null) Require(owners[renderer]==authored,"Collision owner changed.");
            });
            check("STALE-SIGNATURE-REJECTED",()=>{string saved=contract.independentAssembly.sourceSignature;try{contract.independentAssembly.sourceSignature="stale";Require(resolve().Count==0,"Stale support shared.");}finally{contract.independentAssembly.sourceSignature=saved;}});
            check("MISSING-TERRAIN-REJECTED",()=>{var saved=sample;try{sample=(Vector3 p,out float h)=>{h=0;return false;};Require(resolve().Count==0,"Unsupported foundation shared.");}finally{sample=saved;}});
            check("DISABLED-MESH-REJECTED",()=>{authored.enabled=false;try{Require(resolve().Count==0,"Disabled support shared.");}finally{authored.enabled=true;}});
            check("ADDITIONAL-AUTHORED-COLLISION-RETAINED",()=>{var extra=lods[1].renderers[0].gameObject.AddComponent<BoxCollider>();try{Require(resolve().Count==0,"Multiple collision ownership silently replaced.");}finally{UnityEngine.Object.DestroyImmediate(extra);}});
            check("LOWER-LODS-DO-NOT-INSTALL-NEW-WALK-PLANES",()=>{
                var owners=resolve(); var report=new YQGeneratedWorldIntegrityValidator.Report();
                foreach(var lod in lods) foreach(var renderer in lod.renderers)
                    if(renderer!=null)validate.Invoke(null,new object[]{renderer,report,new HashSet<BoxCollider>(),owners});
                Require(report.missingColliders==0 && report.repairedColliders==0,"Reviewed LODs gained substitute collision.");
                Require(approach.GetComponentsInChildren<Collider>(true).Length==1,"Duplicate LOD collision was introduced.");
            });
            check("UNREVIEWED-FLOOR-KEEPS-CONSERVATIVE-REPAIR",()=>{
                var unrelated=GameObject.CreatePrimitive(PrimitiveType.Cube); unrelated.transform.SetParent(staging.transform,false);
                var old=unrelated.GetComponent<Collider>(); UnityEngine.Object.DestroyImmediate(old);
                var report=new YQGeneratedWorldIntegrityValidator.Report();
                validate.Invoke(null,new object[]{unrelated.GetComponent<Renderer>(),report,new HashSet<BoxCollider>(),resolve()});
                Require(report.repairedColliders==1 && unrelated.GetComponent<Collider>()!=null,"Unreviewed collision requirement weakened.");
            });
        }
        catch(Exception e){failures++;results.Add(new{id="FIXTURE",passed=false,error=e.ToString()});}
        finally
        {
            // note: Release only this preview's terrain and clone, including rejected fixture paths.
            if(staging!=null) UnityEngine.Object.DestroyImmediate(staging);
            if(native!=null) UnityEngine.Object.DestroyImmediate(native);
            if(data!=null) UnityEngine.Object.DestroyImmediate(data);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        File.WriteAllText(Path.Combine(output,"Receipt.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {
            utc=DateTime.UtcNow.ToString("O"),passed=failures==0,failures,checks=results.Count,results,
            source=YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs")),
            contractSource=YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/YQSiteFunctionContractsV2.cs")),
            prefabSource=YQStateContract.Sha256Hex(File.ReadAllText(Prefab)),manifestSource=YQStateContract.Sha256Hex(File.ReadAllText(Manifest)),
            runtimeMvid=typeof(YQGeneratedWorldIntegrityValidator).Assembly.ManifestModule.ModuleVersionId.ToString(),
            evidence="COMPILED_EDITOR_NATIVE_TERRAIN_AND_APPROVED_PREFAB_FIXTURE; NOT_PLAYER_TRAVERSAL"
        },Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[YQReviewedMeshLOD] "+(failures==0?"PASS":"FAIL")+"; "+output);
    }
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
}
