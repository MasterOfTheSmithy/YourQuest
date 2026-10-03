using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// note: Explicit, detached art-direction study. This never reads a profile or supplies terrain to production generation.
public static class YQTerrainCohesionPreview
{
    private const float Size = 512f;
    private const float Height = 260f;
    private const string TreeFolder = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/";
    private const string HousePath = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/Buildings/assembly_viking_house4_01.prefab";
    private const string RockPath = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_RockSmall02.prefab";
    private static float RiverX(float z) => 249f + 22f * Mathf.Sin(z * 0.013f) + 7f * Mathf.Sin(z * 0.031f);
    private static float WaterY(float z) => 18f + z * 0.023f;
    private static float TrailX(float z) => RiverX(z) + 20f + 4f * Mathf.Sin(z * 0.021f);
    private static Vector2 HousePoint => new Vector2(TrailX(190f) + 25f, 190f);

    [MenuItem("YourQuest/Preview/Build Separate Terrain Cohesion Study")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Build the terrain study only in idle Edit Mode.");
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string folder = "Assets/YourQuestPreviews/TerrainCohesion_" + stamp;
        string output = "outputs/Terrain_Cohesion_Preview_" + stamp;
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(output);
        AssetDatabase.Refresh();
        Scene original = SceneManager.GetActiveScene();
        Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var report = new StringBuilder("# Terrain cohesion design preview\n\nEvidence: detached Editor scene; not production, streaming, or saved-world acceptance.\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Runtime MVID: " + typeof(YQGeneratedWorldTerrain).Assembly.ManifestModule.ModuleVersionId);
        report.AppendLine("Deterministic preview seed: 10402. No canonical random stream or profile is accessed.");
        try
        {
            SceneManager.SetActiveScene(preview);
            var root = new GameObject("DESIGN STUDY — separate from accepted world");
            var data = new TerrainData { name = "CohesiveValley", heightmapResolution = 513,
                alphamapResolution = 512, baseMapResolution = 512, size = new Vector3(Size, Height, Size) };
            var heights = new float[513, 513];
            for (int z = 0; z <= 512; z++)
                for (int x = 0; x <= 512; x++) heights[z, x] = Surface(x, z) / Height;
            data.SetHeights(0, 0, heights);
            // note: The shared labels do not describe their current textures reliably. Bind this study by observed surface appearance, using private copies.
            data.terrainLayers = new[] {
                PreviewLayer("PackedTrail","ForestFloor",new Color(0.68f,0.73f,0.61f),folder),
                PreviewLayer("DarkSoil","ScreeSoil",new Color(0.72f,0.70f,0.67f),folder),
                PreviewLayer("ForestMoss","RockFace",new Color(0.77f,0.79f,0.78f),folder),
                PreviewLayer("DarkSoil","DirtTrail",new Color(0.88f,0.79f,0.66f),folder),
                PreviewLayer("ForestMoss","DampRiverStone",new Color(0.47f,0.53f,0.49f),folder) };
            AssetDatabase.CreateAsset(data, folder + "/Valley.asset");
            GameObject ground = Terrain.CreateTerrainGameObject(data);
            ground.name = "Valley — foothills, river bed and continuous trail";
            ground.transform.SetParent(root.transform);
            Terrain terrain = ground.GetComponent<Terrain>();
            var terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Preview terrain" };
            AssetDatabase.CreateAsset(terrainMaterial, folder + "/Terrain.mat");
            terrainMaterial.SetFloat("_NumLayersCount", 5f);
            terrain.materialTemplate = terrainMaterial;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1000f;
            terrain.drawInstanced = true;

            BuildWater(root.transform, folder);
            int trees = ScatterTrees(root.transform, terrain);
            int rocks = ScatterRocks(root.transform, terrain, folder);
            GameObject house = InstantiateAsset(HousePath, root.transform);
            house.name = "Settlement scale and approach study — entrance acceptance pending";
            house.transform.position = new Vector3(HousePoint.x, 0f, HousePoint.y);
            house.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            GroundBounds(house, terrain, 0.12f);
            PersistTransientMaterials(root, folder);
            AddGrass(data);
            // note: Paint only after asset/terrain initialization, and explicitly persist the owned alphamap subassets.
            Paint(data);
            float paintedTrail = data.GetAlphamaps((int)TrailX(190),190,1,1)[0,0,3];
            report.AppendLine("Paint before save: " + paintedTrail.ToString("F3"));
            if(paintedTrail < 0.5f)throw new InvalidOperationException("Preview trail painting failed before save");
            EditorUtility.SetDirty(data);
            foreach(var texture in data.alphamapTextures) { EditorUtility.SetDirty(texture); AssetDatabase.SaveAssetIfDirty(texture); }

            // note: Lighting belongs to this new scene only; restore the user's active scene in finally.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.69f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.39f, 0.45f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0010f;
            RenderSettings.fogColor = new Color(0.56f, 0.66f, 0.71f);
            var sun = new GameObject("Preview sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.94f, 0.83f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(34f, -32f, 0f);
            RenderSettings.sun = sun;
            var camera = new GameObject("Review camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 1500f;
            camera.fieldOfView = 60f;
            camera.allowHDR = true;
            camera.enabled = false;
            terrain.Flush();
            Physics.SyncTransforms();
            Validate(data, terrain, trees, rocks, report);
            camera.transform.SetPositionAndRotation(AtGround(terrain, TrailX(80f) + 4f, 80f, 4f), Quaternion.LookRotation(new Vector3(252f, 63f, 355f) - AtGround(terrain, TrailX(80f) + 4f, 80f, 4f)));
            // note: The saved scene is a review artifact, excluded from build settings, with no runtime bootstrap or player/save owners.
            string scenePath = folder + "/TerrainCohesionPreview.unity";
            if (!EditorSceneManager.SaveScene(preview, scenePath)) throw new IOException("Preview scene could not be saved.");
            // note: Save only assets owned by this preview; global SaveAssets could flush unrelated pre-existing dirty assets.
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
            report.AppendLine("\nScene: " + scenePath);
            report.AppendLine("Images: " + output);
            report.AppendLine("\nRemaining: visual approval, authored entrance connection, production authority integration, cell seams, streaming and traversal verification. These preview formulas are not a replacement generator.");
            File.WriteAllText(output + "/Review.md", report.ToString());
            File.WriteAllText("outputs/Terrain_Cohesion_Preview_Latest.txt", output);
            EditorApplication.delayCall += () => CaptureSaved(scenePath, output);
            Debug.Log("[YQTerrainCohesionPreview] Built separate review scene: " + scenePath + "; report=" + output + "/Review.md");
        }
        catch (Exception exception)
        {
            report.AppendLine("\nBUILD FAILED: " + exception);
            File.WriteAllText(output + "/Review.md", report.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
        }
    }

    private static TerrainLayer Layer(string name)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Assets/Terrain/PlaySafe/" + name + ".terrainlayer");
        return layer != null ? layer : throw new InvalidOperationException("Missing curated terrain layer " + name);
    }

    private static TerrainLayer PreviewLayer(string source,string name,Color tint,string folder)
    {
        // note: Palette tuning is confined to newly created layers; source texture, normal and import settings stay intact.
        var layer=UnityEngine.Object.Instantiate(Layer(source));layer.name=name;
        layer.diffuseRemapMax=new Vector4(tint.r,tint.g,tint.b,1f);
        layer.tileSize=new Vector2(name=="RockFace"?15f:6f,name=="RockFace"?15f:6f);
        AssetDatabase.CreateAsset(layer,folder+"/"+name+".terrainlayer");return layer;
    }

    private static float Mass(float x, float z, float cx, float cz, float rx, float rz, float elevation)
    {
        float dx = (x - cx) / rx, dz = (z - cz) / rz;
        return elevation * Mathf.Exp(-(dx * dx + dz * dz) * 1.6f);
    }

    private static float Surface(float x, float z)
    {
        // note: Broad overlapping mountain masses produce shoulders and saddles. Small detail is suppressed along the hydraulic corridor and settlement reserve.
        float dx = Mathf.Abs(x - RiverX(z));
        float baseY = WaterY(z) + 2.2f;
        float mass = Mass(x,z,38,315,128,195,106) + Mass(x,z,104,475,78,112,120) +
                     Mass(x,z,478,310,151,205,90) + Mass(x,z,438,482,82,103,122);
        float corridor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dx - 18f) / 130f));
        float detail = (Mathf.PerlinNoise(x * 0.022f + 9f, z * 0.022f + 4f) - 0.5f) * 9f +
                       (Mathf.PerlinNoise(x * 0.065f + 4f, z * 0.065f + 1f) - 0.5f) * 2.4f;
        float y = baseY + mass * corridor + detail * corridor;
        float channel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dx - 3.5f) / 6f));
        y = Mathf.Lerp(WaterY(z) - 1.7f, y, channel);
        float reserve = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Vector2.Distance(new Vector2(x,z), HousePoint) - 15f) / 17f));
        return Mathf.Lerp(y, WaterY(190f) + 2.2f, reserve);
    }

    private static float PathDistance(float x, float z)
    {
        float main = Mathf.Abs(x - TrailX(z));
        Vector2 a = new Vector2(TrailX(190f), 190f), b = HousePoint;
        Vector2 point = new Vector2(x,z);
        float t = Mathf.Clamp01(Vector2.Dot(point-a,b-a) / (b-a).sqrMagnitude);
        return Mathf.Min(main, Vector2.Distance(point, Vector2.Lerp(a,b,t)));
    }

    private static void Paint(TerrainData data)
    {
        // note: The path is terrain paint, so it cannot float above the sampled surface. Bank and slope masks use the same final heightfield.
        var alpha = new float[512,512,5];
        for (int z=0;z<512;z++) for(int x=0;x<512;x++)
        {
            float wx=x*Size/511f, wz=z*Size/511f;
            float slope=data.GetSteepness(x/511f,z/511f);
            float path=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((PathDistance(wx,wz)-1.3f)/1.5f));
            float bank=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((Mathf.Abs(wx-RiverX(wz))-4f)/6f));
            float cliff=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((slope-24f)/21f));
            float scree=Mathf.Clamp01((data.GetInterpolatedHeight(x/511f,z/511f)-65f)/70f)*0.65f;
            alpha[z,x,3]=path;
            alpha[z,x,4]=bank*(1f-path);
            float rest=(1f-path)*(1f-bank);
            alpha[z,x,2]=rest*cliff;
            alpha[z,x,1]=rest*(1f-cliff)*scree;
            alpha[z,x,0]=rest*(1f-cliff)*(1f-scree);
        }
        data.SetAlphamaps(0,0,alpha);
    }

    private static void BuildWater(Transform parent, string folder)
    {
        var vertices=new Vector3[514]; var uv=new Vector2[514]; var triangles=new int[256*6];
        for(int i=0;i<=256;i++)
        {
            float z=i*2f; float x=RiverX(z);
            vertices[i*2]=new Vector3(x-4.4f,WaterY(z),z);
            vertices[i*2+1]=new Vector3(x+4.4f,WaterY(z),z);
            uv[i*2]=new Vector2(0,z/8f);uv[i*2+1]=new Vector2(1,z/8f);
            if(i==256)continue;
            int k=i*6,a=i*2;
            triangles[k]=a;triangles[k+1]=a+2;triangles[k+2]=a+1;
            triangles[k+3]=a+1;triangles[k+4]=a+2;triangles[k+5]=a+3;
        }
        var mesh=new Mesh {name="Contained river ribbon",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,folder+"/River.asset");
        var go=new GameObject("River — shared bed and surface profile",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets/GeneratedAssets/WorldAssemblies/AllPacks/town_smith/MaterialAdapters/M_Water_1a270bd5_URP.mat");
        if(source==null)throw new InvalidOperationException("Missing project water material");
        var water = new Material(source) { name = "Preview river — muted blue green" };
        water.SetColor("_BaseColor",new Color(0.075f,0.18f,0.18f,1f));
        water.SetFloat("_Smoothness",0.78f);
        AssetDatabase.CreateAsset(water,folder+"/River.mat");
        go.GetComponent<MeshRenderer>().sharedMaterial=water;
    }

    private static GameObject InstantiateAsset(string path, Transform parent)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null)throw new InvalidOperationException("Missing preview asset: "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.gameObject.scene);
        instance.transform.SetParent(parent,false);
        if (!path.StartsWith(TreeFolder,StringComparison.Ordinal) && path != RockPath)
            YQRuntimeWorldAssetRegistry.Instance?.ApplyMaterialOverrides(path,instance);
        // note: Imported presentation scripts do not become runtime authorities in the isolated static study.
        foreach(var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
        return instance;
    }

    private static int ScatterTrees(Transform parent, Terrain terrain)
    {
        var random=new System.Random(10402);int placed=0;
        for(int attempt=0;attempt<3800 && placed<650;attempt++)
        {
            float x=14f+(float)random.NextDouble()*484f,z=14f+(float)random.NextDouble()*484f;
            float cluster=Mathf.PerlinNoise(x*0.018f+17f,z*0.018f+3f);
            if(cluster<0.40f || PathDistance(x,z)<5f || Mathf.Abs(x-RiverX(z))<10f ||
               Vector2.Distance(new Vector2(x,z),HousePoint)<24f || terrain.terrainData.GetSteepness(x/Size,z/Size)>32f ||
               terrain.SampleHeight(new Vector3(x,0,z))>114f)continue;
            string kind=placed%5==0?"Small":placed%3==0?"Tall":"Medium";
            var tree=InstantiateAsset(TreeFolder+"PF Conifer "+kind+" BOTD URP.prefab",parent);
            tree.transform.position=new Vector3(x,0,z);tree.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            tree.transform.localScale*=0.75f+(float)random.NextDouble()*0.5f;
            if(!YQGeneratedWorldTerrain.TryPlaceGroundedObject(tree,terrain,YQGeneratedWorldPlacementCategory.Tree,0.05f,out _))
            { UnityEngine.Object.DestroyImmediate(tree);continue; }
            placed++;
        }
        return placed;
    }

    private static int ScatterRocks(Transform parent,Terrain terrain,string folder)
    {
        // note: Use the Nordic rock family together with its existing project-owned URP adapter.
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets/GeneratedAssets/WorldAssemblies/AllPacks/nordic_village/MaterialAdapters/M_Rocks_72f5568f_URP.mat");
        if(source==null)throw new InvalidOperationException("Missing Nordic rock adapter");
        var stone=new Material(source) {name="Preview Nordic stone"};
        stone.SetFloat("_Smoothness",0.12f);
        AssetDatabase.CreateAsset(stone,folder+"/Stone.mat");
        var random=new System.Random(10403);int placed=0;
        for(int attempt=0;attempt<600 && placed<85;attempt++)
        {
            float x=12+(float)random.NextDouble()*488,z=12+(float)random.NextDouble()*488;
            if(PathDistance(x,z)<4 || Mathf.Abs(x-RiverX(z))<7 || Vector2.Distance(new Vector2(x,z),HousePoint)<23)continue;
            var rock=InstantiateAsset(RockPath,parent);
            foreach(var renderer in rock.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=stone;
            rock.transform.position=new Vector3(x,0,z);rock.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            rock.transform.localScale*=1.1f+(float)random.NextDouble()*2.7f;
            GroundBounds(rock,terrain,0.35f);placed++;
        }
        return placed;
    }

    private static void GroundBounds(GameObject instance,Terrain terrain,float embed)
    {
        if(!YQGeneratedWorldTerrain.TryGetStableContactGeometry(instance,out _,out float bottom))
            throw new InvalidOperationException("No support geometry: "+instance.name);
        Vector3 p=instance.transform.position;p.y+=terrain.SampleHeight(p)-bottom-embed;instance.transform.position=p;
    }

    private static void PersistTransientMaterials(GameObject root,string folder)
    {
        // note: Save only temporary adapter results referenced by this new scene; never edit imported or shared source materials.
        int count=0;var saved=new HashSet<Material>();
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) foreach(var material in renderer.sharedMaterials)
            if(material!=null && !EditorUtility.IsPersistent(material) && saved.Add(material))
            { material.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(material,folder+"/AdaptedMaterial_"+(count++)+".mat"); }
    }

    private static void AddGrass(TerrainData data)
    {
        Texture2D grass=YQRuntimeWorldAssetRegistry.Instance?.TerrainGrassTexture;
        if(grass==null)return;
        data.SetDetailResolution(256,16);
        data.detailPrototypes=new[] {new DetailPrototype {prototypeTexture=grass,renderMode=DetailRenderMode.GrassBillboard,
            minWidth=0.35f,maxWidth=0.7f,minHeight=0.35f,maxHeight=0.8f,healthyColor=new Color(0.58f,0.64f,0.39f),dryColor=new Color(0.5f,0.49f,0.31f)}};
        var detail=new int[256,256];
        for(int z=0;z<256;z++)for(int x=0;x<256;x++)
            if(PathDistance(x*2,z*2)>3 && Mathf.Abs(x*2-RiverX(z*2))>9 && data.GetSteepness(x/255f,z/255f)<28 && data.GetInterpolatedHeight(x/255f,z/255f)<90)detail[z,x]=2;
        data.SetDetailLayer(0,0,0,detail);
    }

    private static Vector3 AtGround(Terrain t,float x,float z,float lift)=>new Vector3(x,t.SampleHeight(new Vector3(x,0,z))+lift,z);

    private static void Validate(TerrainData data,Terrain terrain,int trees,int rocks,StringBuilder report)
    {
        float maxGrade=0f,minDepth=float.MaxValue,minBank=float.MaxValue,maxRootError=0f;
        int measuredRoots=0;
        foreach(Transform child in terrain.transform.parent)
            if(child.name.StartsWith("PF Conifer",StringComparison.Ordinal) && YQGeneratedWorldTerrain.TryGetStableContactGeometry(child.gameObject,out _,out float bottom))
            {measuredRoots++;maxRootError=Mathf.Max(maxRootError,Mathf.Abs(bottom-(terrain.SampleHeight(child.position)-0.05f)));}
        for(int z=2;z<510;z+=2)
        {
            float y=terrain.SampleHeight(new Vector3(TrailX(z),0,z));
            float next=terrain.SampleHeight(new Vector3(TrailX(z+2),0,z+2));
            float run=Vector2.Distance(new Vector2(TrailX(z),z),new Vector2(TrailX(z+2),z+2));
            maxGrade=Mathf.Max(maxGrade,Mathf.Abs(next-y)/run);
            minDepth=Mathf.Min(minDepth,WaterY(z)-terrain.SampleHeight(new Vector3(RiverX(z),0,z)));
            foreach(float side in new[]{-1f,1f})minBank=Mathf.Min(minBank,terrain.SampleHeight(new Vector3(RiverX(z)+side*10f,0,z))-WaterY(z));
        }
        report.AppendLine("\nPreview checks (sampled final TerrainData, not runtime certification):");
        report.AppendLine("Trees="+trees+"; rocks="+rocks+"; minimum river centre depth="+minDepth.ToString("F3")+"m; minimum bank freeboard="+minBank.ToString("F3")+"m; maximum main-trail grade="+(maxGrade*100).ToString("F2")+"%.");
        report.AppendLine("Measured root contacts="+measuredRoots+"; maximum bounds-to-ground contact error="+maxRootError.ToString("F4")+"m (geometry measurement; close visual approval still required).");
        report.AppendLine("Sampled preview geometry: "+(trees>0 && measuredRoots==trees && maxRootError<0.01f && minDepth>1f && minBank>0.5f && maxGrade<0.20f?"PASS":"FAIL"));
    }

    private static void Capture(Camera camera,Vector3 eye,Vector3 target,string path)
    {
        var previous=RenderTexture.active;
        var targetTexture=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);
        Texture2D pixels=null;
        try
        {
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.targetTexture=targetTexture;
            camera.Render();RenderTexture.active=targetTexture;
            pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally
        { camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(targetTexture);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static void CaptureSaved(string path,string output)
    {
        // note: Native terrain textures and imported tree shaders need Editor frames after scene loading. This bounded callback always removes itself and restores scene ownership.
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene original=SceneManager.GetActiveScene();
        Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        double started=EditorApplication.timeSinceStartup;
        void Finish()
        {
            if(EditorApplication.timeSinceStartup-started<4d && !EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Finish;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture interrupted by Play Mode");
                Terrain terrain=null;Camera camera=null;
                foreach(var root in scene.GetRootGameObjects())
                { if(terrain==null)terrain=root.GetComponentInChildren<Terrain>();if(camera==null)camera=root.GetComponentInChildren<Camera>(); }
                camera.scene=scene;
                float[,,] alpha=terrain.terrainData.GetAlphamaps((int)TrailX(190),190,1,1);
                File.AppendAllText(output+"/Review.md","\nReloaded trail paint weight: "+alpha[0,0,3].ToString("F3")+".\n");
                if(alpha[0,0,3]<0.5f)throw new InvalidOperationException("Saved terrain trail paint did not survive reload");
                Capture(camera,AtGround(terrain,TrailX(80)+4,80,3),new Vector3(252,57,355),output+"/01_valley.png");
                Capture(camera,AtGround(terrain,RiverX(150)+10,150,3),new Vector3(TrailX(235),34,235),output+"/02_river_trail.png");
                Capture(camera,new Vector3(390,280,30),new Vector3(254,55,260),output+"/03_overview.png");
                File.AppendAllText(output+"/Review.md","Saved-scene reload and three rendered views: CAPTURED.\n");
            }
            catch(Exception error){File.AppendAllText(output+"/Review.md","\nCapture failed: "+error);Debug.LogException(error);}
            finally
            {if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
        }
        EditorApplication.update+=Finish;
    }
}
