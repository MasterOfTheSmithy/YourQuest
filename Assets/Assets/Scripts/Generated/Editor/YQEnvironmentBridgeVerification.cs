using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Disposable Editor objects reproduce integrity repair over the existing bridge's fitted collision mesh; no scene or save is changed.
public static class YQEnvironmentBridgeVerification
{
    private const string RecordedBridgeCensus = "outputs/G08_R3_Physical_20261003/20261004_024355_404_session/BridgeColliders.json";
    private const string RecordedDisposableCensus = "outputs/G08_R3_Physical_20261003/20261004_032515_874_session/BridgeColliders.json";
    private const string RecordedFootprintProfile = "outputs/G08_EnvironmentCohesion_20261003/20261004_buffered_footprint_profile.json";
    [MenuItem("YourQuest/Verification/Run Bridge Collision Ownership Contracts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Bridge contracts require stable Edit Mode.");
        var checks = new List<object>();
        var geometryObservations = new List<object>();
        int failures = 0;
        Action<string, bool> check = (name, passed) =>
        {
            checks.Add(new { name, verdict = passed ? "PASS" : "FAIL" });
            if (!passed) failures++;
        };
        GameObject root = null;
        Mesh mesh = null;
        Mesh shoulderMesh = null;
        try
        {
            // note: A lowered wet datum used to replace an otherwise continuous finished road elevation; test the actual owner's pure boundary.
            MethodInfo resolveElevation = typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveRiverCrossingDeckElevation", BindingFlags.Static | BindingFlags.NonPublic);
            Func<float, float, bool, float> elevation = (terrain, water, wet) =>
                (float)resolveElevation.Invoke(null, new object[] { terrain, water, wet });
            check("dry crossing preserves finished road offset", Mathf.Abs(elevation(62.5f, 60.6f, false) - 62.68f) < .00001f);
            check("lower wet surface cannot replace the finished road with a downhill cliff", Mathf.Abs(elevation(62.5f, 60.6f, true) - 62.68f) < .00001f);
            check("wet-mask toggle preserves road height when water is lower", elevation(62.5f, 60.6f, true) == elevation(62.5f, 60.6f, false));
            check("higher water retains accepted clearance above both water and road", Mathf.Abs(elevation(62.5f, 64f, true) - 64.32f) < .00001f);
            check("negative world elevations retain the same crossing lower bounds", Mathf.Abs(elevation(-12.5f, -20f, true) - -12.32f) < .00001f);
            root = new GameObject("BridgeOwnershipContractFixture");
            root.AddComponent<YQGeneratedRiverBridge>();
            var road = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, .8f, 4f), new Vector3(0f, .9f, 8f) };
            MethodInfo buildMesh = typeof(YQGeneratedRiverBridge).GetMethod("BuildWalkSurfaceMesh", BindingFlags.Static | BindingFlags.NonPublic);
            mesh = (Mesh)buildMesh.Invoke(null, new object[] { road, 3f });
            var support = new GameObject("StoneBridgeWalkSurface");
            support.transform.SetParent(root.transform, false);
            MeshCollider fitted = support.AddComponent<MeshCollider>();
            fitted.sharedMesh = mesh;
            // note: Check the actual continuous support builder independently of presentation classification and retain its topology evidence.
            MethodInfo buildShoulder = typeof(YQGeneratedRiverBridge).GetMethod("BuildSupportSurfaceMesh", BindingFlags.Static | BindingFlags.NonPublic);
            shoulderMesh = (Mesh)buildShoulder.Invoke(null, new object[] { road, 3f });
            Vector3[] shoulderVertices = shoulderMesh.vertices;
            Vector3[] deckVertices = mesh.vertices;
            bool heights = shoulderVertices.Length == road.Count * 4;
            bool thickness = heights;
            for (int section = 0; section < road.Count && heights; section++)
            {
                int first = section * 4;
                heights &= Mathf.Abs(shoulderVertices[first].y - (deckVertices[first].y - .01f)) < .00001f &&
                    Mathf.Abs(shoulderVertices[first + 1].y - (deckVertices[first + 1].y - .01f)) < .00001f;
                thickness &= Mathf.Abs(shoulderVertices[first].y - shoulderVertices[first + 2].y - .5f) < .00001f;
            }
            check("continuous support stays below the unchanged fitted top at every joint", heights);
            check("continuous support retains half-metre depth and one-metre shoulders", thickness && Mathf.Abs(shoulderMesh.bounds.size.x - 5f) < .00001f);
            check("continuous support remains bounded by the accepted bank contacts", Mathf.Abs(shoulderMesh.bounds.min.z) < .00001f && Mathf.Abs(shoulderMesh.bounds.max.z - 8f) < .00001f);
            int[] indices = shoulderMesh.triangles;
            var edges = new Dictionary<string, int>();
            int interiorCaps = 0;
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                int section = indices[triangle] / 4;
                if (indices[triangle + 1] / 4 == section && indices[triangle + 2] / 4 == section && section > 0 && section < road.Count - 1)
                    interiorCaps++;
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = indices[triangle + edge], b = indices[triangle + (edge + 1) % 3];
                    string key = Math.Min(a, b) + ":" + Math.Max(a, b);
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                }
            }
            bool closed = true;
            foreach (int count in edges.Values) closed &= count == 2;
            check("continuous support closes terminal banks without open mesh edges", closed && indices.Length == (road.Count - 1) * 24 + 12);
            check("continuous support has no internal segment end caps", interiorCaps == 0);
            var shoulder = new GameObject("StoneBridgeWalkSurface");
            shoulder.transform.SetParent(root.transform, false);
            MeshCollider shoulderCollider = shoulder.AddComponent<MeshCollider>();
            shoulderCollider.sharedMesh = shoulderMesh;
            // note: Collider rays are one-sided. Closed edges alone did not reveal inverted top/bottom faces, which admitted the underside as the walking datum.
            check("fitted top bottom and side faces point outward", FacesPointOutward(mesh, road, false));
            check("support top bottom side and terminal faces point outward", FacesPointOutward(shoulderMesh, road, true));
            Physics.SyncTransforms();
            Ray downward = new Ray(new Vector3(0f, 2f, 2f), Vector3.down);
            bool deckHit = fitted.Raycast(downward, out RaycastHit deckContact, 4f);
            bool shoulderHit = shoulderCollider.Raycast(downward, out RaycastHit shoulderContact, 4f);
            geometryObservations.Add(new { name = "direct fitted downward ray", hit = deckHit, deckContact.point, deckContact.normal, deckContact.triangleIndex, expectedY = .62f });
            geometryObservations.Add(new { name = "direct support downward ray", hit = shoulderHit, shoulderContact.point, shoulderContact.normal, shoulderContact.triangleIndex, expectedY = .61f });
            check("downward fitted ray hits the intended top instead of its underside", deckHit && Mathf.Abs(deckContact.point.y - .62f) < .00001f && deckContact.normal.y > 0f);
            check("downward support ray hits the intended inset top instead of its underside", shoulderHit && Mathf.Abs(shoulderContact.point.y - .61f) < .00001f && shoulderContact.normal.y > 0f);
            check("outward collision winding survives four directions curved grades and signed far coordinates", VerifyWindingProfiles(buildMesh, buildShoulder));
            VerifyRepeatedSamples(buildMesh, buildShoulder, check);
            VerifyRecordedProfiles(buildMesh, buildShoulder, check, geometryObservations, RecordedBridgeCensus);
            VerifyRecordedProfiles(buildMesh, buildShoulder, check, geometryObservations, RecordedDisposableCensus);
            VerifySharpBends(buildMesh, buildShoulder, check, geometryObservations);
            VerifyRecordedFootprint(buildMesh, buildShoulder, check, geometryObservations);
            string[] variants = { "StoneBridgeDeck", "WoodBridgeDeck", "DisheveledBridgeDeck", "RepairedBridgeDeck" };
            var decks = new List<GameObject>();
            foreach (string variant in variants)
            {
                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = variant;
                deck.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(deck.GetComponent<Collider>());
                decks.Add(deck);
            }
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "BridgeRailPost";
            rail.transform.SetParent(root.transform, false);
            rail.GetComponent<Collider>().enabled = false;
            YQGeneratedWorldIntegrityValidator.Report report = null;
            Drain(YQGeneratedWorldIntegrityValidator.ValidateAndRepairRoutine(root, "synthetic-bridge", "bridge-ownership-contract", value => report = value));
            foreach (GameObject deck in decks)
                check(deck.name + " retains presentation-only collision", deck.GetComponent<Collider>() == null);
            check("visual rail collider remains disabled after full integrity routine", !rail.GetComponent<Collider>().enabled);
            check("fitted mesh remains the enabled collision owner", fitted.enabled && fitted.sharedMesh == mesh && !fitted.isTrigger);
            check("four decks borrow valid fitted collision without repair", report != null && report.walkableSurfaces == 4 && report.validColliders == 4 && report.repairedColliders == 0 && report.missingColliders == 0);
            MethodInfo classify = typeof(YQGeneratedWorldIntegrityValidator).GetMethod("IsWalkableSurface", BindingFlags.Static | BindingFlags.NonPublic);
            check("owned rail is excluded from floor classification", !(bool)classify.Invoke(null, new object[] { rail }));
            rail.transform.SetParent(null);
            try { check("same name without bridge ownership retains legacy classification", (bool)classify.Invoke(null, new object[] { rail })); }
            finally { UnityEngine.Object.DestroyImmediate(rail); }
            // note: These negative cases deliberately emit WORLDGEN ERROR diagnostics and must remain rejected rather than acquiring block colliders.
            MethodInfo validate = typeof(YQGeneratedWorldIntegrityValidator).GetMethod("ValidateWalkableSurface", BindingFlags.Static | BindingFlags.NonPublic);
            fitted.enabled = false;
            var disabledReport = new YQGeneratedWorldIntegrityValidator.Report();
            validate.Invoke(null, new object[] { decks[1].GetComponent<Renderer>(), disabledReport });
            check("disabled fitted owner is rejected and not enabled by its render deck", disabledReport.missingColliders == 1 && !fitted.enabled && decks[1].GetComponent<Collider>() == null);
            fitted.enabled = true;
            fitted.sharedMesh = null;
            var absentReport = new YQGeneratedWorldIntegrityValidator.Report();
            validate.Invoke(null, new object[] { decks[2].GetComponent<Renderer>(), absentReport });
            check("missing fitted mesh is rejected without synthesizing another owner", absentReport.missingColliders == 1 && decks[2].GetComponent<Collider>() == null);
        }
        catch (Exception exception)
        {
            failures++;
            checks.Add(new { name = "bridge fixture execution", verdict = "FAIL", exception = exception.ToString() });
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (shoulderMesh != null) UnityEngine.Object.DestroyImmediate(shoulderMesh);
        }
        string directory = Path.Combine("outputs/G08_EnvironmentCohesion_20261003", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_bridge_ownership");
        Directory.CreateDirectory(directory);
        var receipt = new
        {
            utc = DateTime.UtcNow.ToString("O"), verifierVersion = "generated-bridge-buffered-footprint-9",
            collisionGeometryVersion = typeof(YQGeneratedRiverBridge).GetField("CollisionCorridorGeometryVersion", BindingFlags.Static | BindingFlags.NonPublic)?.GetRawConstantValue(),
            evidenceLevel = "EDIT_MODE_SYNTHETIC_INTEGRITY_REPAIR", verdict = failures == 0 ? "PASS" : "FAIL", runtimeVerified = false,
            unityVersion = Application.unityVersion, runtimeMvid = typeof(YQGeneratedRiverBridge).Assembly.ManifestModule.ModuleVersionId.ToString(),
            editorMvid = typeof(YQEnvironmentBridgeVerification).Assembly.ManifestModule.ModuleVersionId.ToString(),
            runtimeAssemblySha256 = Hash(typeof(YQGeneratedRiverBridge).Assembly.Location), editorAssemblySha256 = Hash(typeof(YQEnvironmentBridgeVerification).Assembly.Location),
            integritySourceSha256 = Hash("Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs"),
            bridgeSourceSha256 = Hash("Assets/Assets/Scripts/Generated/YQGeneratedRiverBridge.cs"),
            environmentSourceSha256 = Hash("Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs"),
            verifierSourceSha256 = Hash("Assets/Assets/Scripts/Generated/Editor/YQEnvironmentBridgeVerification.cs"),
            recordedGeometryPath = RecordedBridgeCensus, recordedGeometrySha256 = Hash(RecordedBridgeCensus), checks, geometryObservations,
            additionalRecordedGeometryPath = RecordedDisposableCensus, additionalRecordedGeometrySha256 = Hash(RecordedDisposableCensus),
            recordedFootprintProfile = RecordedFootprintProfile, recordedFootprintProfileSha256 = Hash(RecordedFootprintProfile),
            limits = "Synthetic integrity ownership only. Two expected negative-case errors retained. Production bank/deck clearance, both directions, visuals and final affected R1/R2 need separate evidence."
        };
        // note: Preserve numeric geometry with the project's vector converter rather than serializing Unity's recursive convenience properties.
        var jsonSettings = new JsonSerializerSettings { Formatting = Formatting.Indented, Converters = { new Vector3JsonConverter() } };
        File.WriteAllText(Path.Combine(directory, "Receipt.json"), JsonConvert.SerializeObject(receipt, jsonSettings));
        Debug.Log("[YQBridgeOwnership] " + receipt.verdict + "; " + directory);
    }

    private static bool FacesPointOutward(Mesh mesh, IReadOnlyList<Vector3> road, bool capped)
    {
        if (mesh.subMeshCount == 3) return BufferedFacesPointOutward(mesh, road, capped);
        // note: Verify each indexed triangle's geometric normal, including terminal caps; averaged vertex normals can conceal an inverted collision face.
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        int segmentIndices = (road.Count - 1) * 24;
        if (indices.Length != segmentIndices + (capped ? 12 : 0)) return false;
        for (int offset = 0; offset < indices.Length; offset += 3)
        {
            Vector3 normal = Vector3.Cross(vertices[indices[offset + 1]] - vertices[indices[offset]],
                vertices[indices[offset + 2]] - vertices[indices[offset]]);
            Vector3 expected;
            if (offset >= segmentIndices)
                expected = offset < segmentIndices + 6 ? road[0] - road[1] : road[road.Count - 1] - road[road.Count - 2];
            else
            {
                int face = offset % 24;
                Vector3 tangent = road[offset / 24 + 1] - road[offset / 24];
                Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
                expected = face < 6 ? Vector3.up : face < 12 ? Vector3.down : face < 18 ? -side : side;
            }
            if (Vector3.Dot(normal, expected) <= .000001f) return false;
        }
        return true;
    }

    private static bool BufferedFacesPointOutward(Mesh mesh, IReadOnlyList<Vector3> road, bool capped)
    {
        // note: Independently verify the union surface, outward walls and indexed manifold. An open fitted bank is allowed only at an original terminal plane.
        Vector3[] vertices = mesh.vertices;
        var edges = new Dictionary<long, int>();
        for (int submesh = 0; submesh < 3; submesh++)
        {
            int[] triangles = mesh.GetTriangles(submesh);
            if (triangles.Length == 0) return false;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]], b = vertices[triangles[index + 1]], c = vertices[triangles[index + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (submesh < 2)
                {
                    if (normal.y * (submesh == 0 ? 1f : -1f) <= .0000001f) return false;
                }
                else
                {
                    Vector3 center = (a + b + c) / 3f;
                    Vector2 outward = new Vector2(normal.x, normal.z).normalized * .02f;
                    Vector2 planar = new Vector2(center.x, center.z);
                    if (outward.sqrMagnitude < .0001f || !ContainsTop(mesh, planar - outward) || ContainsTop(mesh, planar + outward)) return false;
                }
                for (int edge = 0; edge < 3; edge++)
                {
                    int from = triangles[index + edge], to = triangles[index + (edge + 1) % 3];
                    long key = ((long)Math.Min(from, to) << 32) | (uint)Math.Max(from, to);
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                }
            }
        }
        foreach (var pair in edges)
        {
            if (pair.Value == 2) continue;
            if (capped || pair.Value != 1) return false;
            Vector3 a = vertices[(int)(pair.Key >> 32)], b = vertices[(int)(pair.Key & uint.MaxValue)];
            bool bank = false;
            for (int terminal = 0; terminal < 2; terminal++)
            {
                int index = terminal == 0 ? 0 : road.Count - 1;
                Vector3 tangent = terminal == 0 ? road[1] - road[0] : road[index] - road[index - 1];
                tangent.y = 0f; tangent.Normalize();
                bank |= Mathf.Abs(Vector3.Dot(a - road[index], tangent)) < .005f && Mathf.Abs(Vector3.Dot(b - road[index], tangent)) < .005f;
            }
            if (!bank) return false;
        }
        return true;
    }

    private static bool ContainsTop(Mesh mesh, Vector2 point)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.GetTriangles(0);
        for (int index = 0; index < triangles.Length; index += 3)
        {
            Vector3 av = vertices[triangles[index]], bv = vertices[triangles[index + 1]], cv = vertices[triangles[index + 2]];
            Vector2 a = new Vector2(av.x, av.z), b = new Vector2(bv.x, bv.z), c = new Vector2(cv.x, cv.z);
            Vector2 ab = b - a, ac = c - a, ap = point - a;
            double determinant = (double)ab.x * ac.y - (double)ab.y * ac.x;
            if (Math.Abs(determinant) < .00000001d) continue;
            double v = ((double)ap.x * ac.y - (double)ap.y * ac.x) / determinant;
            double w = ((double)ab.x * ap.y - (double)ab.y * ap.x) / determinant;
            if (v >= -.000001d && w >= -.000001d && v + w <= 1.000001d) return true;
        }
        return false;
    }

    private static void VerifySharpBends(MethodInfo buildMesh, MethodInfo buildShoulder, Action<string, bool> check, List<object> observations)
    {
        // note: Short ninety-degree turns reproduce the distinct strip fold. Mirror, reversal and far coordinates exercise width, retained samples and cooked one-sided physics separately.
        var baseline = new[] { new Vector3(0f, 2f, 0f), new Vector3(0f, 2.4f, 5f), new Vector3(0f, 2.6f, 6f), new Vector3(1f, 2.7f, 6f), new Vector3(2f, 2.8f, 6f) };
        for (int variant = 0; variant < 4; variant++)
        {
            var road = (Vector3[])baseline.Clone();
            if (variant == 1) Array.Reverse(road);
            if (variant == 2) for (int index = 0; index < road.Length; index++) road[index].x = -road[index].x;
            if (variant == 3) for (int index = 0; index < road.Length; index++) road[index] += new Vector3(-10000f, -40f, 12000f);
            foreach (MethodInfo builder in new[] { buildMesh, buildShoulder })
            {
                Mesh mesh = null; GameObject owner = null;
                string name = "sharp bend " + variant + (builder == buildMesh ? " fitted" : " support");
                try
                {
                    mesh = (Mesh)builder.Invoke(null, new object[] { road, 4.8f });
                    check(name + " uses full-width union with outward manifold faces", mesh.subMeshCount == 3 && FacesPointOutward(mesh, road, builder == buildShoulder));
                    bool fullWidth = true, retained = true, rays = true;
                    float maximumCenterlineHeightError = 0f;
                    float offset = builder == buildMesh ? .22f : .21f;
                    float halfWidth = builder == buildMesh ? 2.4f : 3.4f;
                    foreach (Vector3 sample in road)
                    {
                        bool found = false;
                        foreach (Vector3 vertex in mesh.vertices) found |= Vector3.Distance(vertex, sample + Vector3.up * offset) < .002f;
                        retained &= found;
                    }
                    owner = new GameObject("SharpBendDisposableCollider");
                    MeshCollider collider = owner.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                    Physics.SyncTransforms();
                    for (int segment = 0; segment < road.Length - 1; segment++)
                    {
                        Vector3 direction = road[segment + 1] - road[segment]; direction.y = 0f; direction.Normalize();
                        Vector3 side = new Vector3(-direction.z, 0f, direction.x) * halfWidth * .95f;
                        foreach (float fraction in new[] { .25f, .5f, .75f })
                        foreach (float lateral in new[] { -1f, 0f, 1f })
                        {
                            Vector3 p = Vector3.Lerp(road[segment], road[segment + 1], fraction) + side * lateral;
                            fullWidth &= ContainsTop(mesh, new Vector2(p.x, p.z));
                            bool hitTop = collider.Raycast(new Ray(p + Vector3.up * 10f, Vector3.down), out RaycastHit hit, 20f) && hit.normal.y > 0f;
                            rays &= hitTop;
                            if (lateral == 0f)
                                maximumCenterlineHeightError = Mathf.Max(maximumCenterlineHeightError, hitTop ? Mathf.Abs(hit.point.y - p.y - offset) : float.PositiveInfinity);
                        }
                    }
                    check(name + " covers ninety-five percent of full segment widths", fullWidth);
                    check(name + " retains every original centreline height", retained);
                    check(name + " cooked collider supports downward rays across the full corridor", rays);
                    check(name + " cooked tread preserves interpolated centreline grades", maximumCenterlineHeightError < .005f);
                    observations.Add(new { name, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3, fullWidth, retained, rays, maximumCenterlineHeightError });
                }
                catch (Exception exception)
                {
                    check(name + " rebuild executes", false);
                    observations.Add(new { name, exception = exception.ToString() });
                }
                finally
                {
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                    if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            check("sharp bend " + variant + " support stays below fitted tread across shared width", SupportStaysBelowTread(buildMesh, buildShoulder, road, observations));
        }
    }

    private static bool SupportStaysBelowTread(MethodInfo buildMesh, MethodInfo buildShoulder, Vector3[] road, List<object> observations)
    {
        // note: Independently triangulated wider shoulders must not overtake the fitted walking datum; compare actual cooked surfaces at the same shared corridor points.
        Mesh deck = null, support = null; GameObject owner = null;
        float maximumSupportAboveDeck = float.NegativeInfinity;
        try
        {
            deck = (Mesh)buildMesh.Invoke(null, new object[] { road, 4.8f });
            support = (Mesh)buildShoulder.Invoke(null, new object[] { road, 4.8f });
            owner = new GameObject("SharpBendPairedCollisionFixture");
            MeshCollider fitted = owner.AddComponent<MeshCollider>(); fitted.sharedMesh = deck;
            MeshCollider shoulder = owner.AddComponent<MeshCollider>(); shoulder.sharedMesh = support;
            Physics.SyncTransforms();
            for (int segment = 0; segment < road.Length - 1; segment++)
            {
                Vector3 direction = road[segment + 1] - road[segment]; direction.y = 0f; direction.Normalize();
                Vector3 side = new Vector3(-direction.z, 0f, direction.x) * 2.4f * .95f;
                foreach (float fraction in new[] { .25f, .5f, .75f })
                foreach (float lateral in new[] { -1f, 0f, 1f })
                {
                    Vector3 p = Vector3.Lerp(road[segment], road[segment + 1], fraction) + side * lateral;
                    Ray ray = new Ray(p + Vector3.up * 10f, Vector3.down);
                    if (!fitted.Raycast(ray, out RaycastHit a, 20f) || !shoulder.Raycast(ray, out RaycastHit b, 20f)) return false;
                    maximumSupportAboveDeck = Mathf.Max(maximumSupportAboveDeck, b.point.y - a.point.y);
                }
            }
            observations.Add(new { name = "paired sharp bend support clearance", maximumSupportAboveDeck });
            return maximumSupportAboveDeck <= -.008f;
        }
        finally
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (deck != null) UnityEngine.Object.DestroyImmediate(deck);
            if (support != null) UnityEngine.Object.DestroyImmediate(support);
        }
    }

    private static bool VerifyWindingProfiles(MethodInfo buildMesh, MethodInfo buildShoulder)
    {
        // note: Exercise the same owner in every cardinal direction and two bent profiles; this is geometric coverage, not a production traversal or seam verdict.
        var profiles = new List<Vector3[]>();
        for (int direction = 0; direction < 4; direction++)
        {
            Quaternion rotation = Quaternion.Euler(0f, direction * 90f, 0f);
            profiles.Add(new[] { rotation * new Vector3(0f, 0f, 0f), rotation * new Vector3(0f, .8f, 4f), rotation * new Vector3(0f, .9f, 8f) });
        }
        profiles.Add(new[] { new Vector3(-13f, 2f, -18f), new Vector3(-11f, 2.6f, -14f), new Vector3(-9f, 2.1f, -11f) });
        profiles.Add(new[] { new Vector3(-10000f, -40f, 12000f), new Vector3(-9997f, -39f, 12004f), new Vector3(-9993f, -38.5f, 12007f) });
        foreach (Vector3[] profile in profiles)
        {
            Mesh deck = null, support = null;
            try
            {
                deck = (Mesh)buildMesh.Invoke(null, new object[] { profile, 3f });
                support = (Mesh)buildShoulder.Invoke(null, new object[] { profile, 3f });
                if (!FacesPointOutward(deck, profile, false) || !FacesPointOutward(support, profile, true)) return false;
            }
            finally
            {
                if (deck != null) UnityEngine.Object.DestroyImmediate(deck);
                if (support != null) UnityEngine.Object.DestroyImmediate(support);
            }
        }
        return true;
    }

    private static void VerifyRepeatedSamples(MethodInfo buildMesh, MethodInfo buildShoulder, Action<string, bool> check)
    {
        // note: Repeated accepted sampling points must not rotate a terminal cross-section or create zero-length collision strips.
        var clean = new[] { new Vector3(2f, 4f, -7f), new Vector3(6f, 4.6f, -6f), new Vector3(10f, 5f, -5f) };
        var cases = new Dictionary<string, Vector3[]>
        {
            { "repeated first sample", new[] { clean[0], clean[0], clean[1], clean[2] } },
            { "repeated interior sample", new[] { clean[0], clean[1], clean[1], clean[2] } },
            { "repeated final sample", new[] { clean[0], clean[1], clean[2], clean[2] } },
            { "repeated runs at both banks", new[] { clean[0], clean[0], clean[0], clean[1], clean[1], clean[2], clean[2], clean[2] } },
            { "sub tenth-millimetre terminal sample", new[] { clean[0], clean[1], clean[2], clean[2] + new Vector3(.00004f, 0f, .00002f) } }
        };
        foreach (var pair in cases)
            check(pair.Key + " preserves clean fitted and support geometry", RebuildMatchesClean(buildMesh, buildShoulder, pair.Value, clean, 4.8f));
        var distinctShortSegment = new[] { clean[0], clean[0] + new Vector3(.02f, 0f, .005f), clean[1], clean[2] };
        check("distinct short segments retain their original collision samples", RebuildMatchesClean(buildMesh, buildShoulder, distinctShortSegment, distinctShortSegment, 4.8f));
        Mesh collapsedDeck = null, collapsedSupport = null;
        try
        {
            var collapsed = new[] { clean[0], clean[0], clean[0] };
            collapsedDeck = (Mesh)buildMesh.Invoke(null, new object[] { collapsed, 4.8f });
            collapsedSupport = (Mesh)buildShoulder.Invoke(null, new object[] { collapsed, 4.8f });
            check("fully repeated profile cannot manufacture a collision surface", collapsedDeck == null && collapsedSupport == null);
        }
        finally
        {
            if (collapsedDeck != null) UnityEngine.Object.DestroyImmediate(collapsedDeck);
            if (collapsedSupport != null) UnityEngine.Object.DestroyImmediate(collapsedSupport);
        }
    }

    private static void VerifyRecordedProfiles(MethodInfo buildMesh, MethodInfo buildShoulder, Action<string, bool> check, List<object> observations, string censusPath)
    {
        // note: Reconstruct numeric centreline samples from the retained project-generated fitted mesh, not vendor geometry or a newly selected world.
        JObject census = JObject.Parse(File.ReadAllText(censusPath));
        int count = 0;
        foreach (JToken bridge in census["bridges"])
        foreach (JToken collider in bridge["colliders"])
        {
            if ((string)collider["mesh"] != "YQ_StoneBridgeWalkSurface") continue;
            JArray vertices = (JArray)collider["geometry"]["vertices"];
            var road = new List<Vector3>();
            float width = 0f;
            for (int index = 0; index < vertices.Count; index += 4)
            {
                Vector3 a = ReadPoint(vertices[index]), b = ReadPoint(vertices[index + 1]);
                road.Add((a + b) * .5f - Vector3.up * .22f);
                if (index == 0) width = Vector3.Distance(a, b);
            }
            var clean = new List<Vector3>();
            foreach (Vector3 point in road)
                if (clean.Count == 0 || Vector3.Distance(clean[clean.Count - 1], point) > .0001f) clean.Add(point);
            bool passed = false;
            try { passed = RebuildMatchesClean(buildMesh, buildShoulder, road.ToArray(), clean.ToArray(), width); }
            catch (Exception exception) { observations.Add(new { name = "recorded reconstruction exception", bridgeInstanceId = (int)bridge["instanceId"], exception = exception.ToString() }); }
            check("recorded bridge " + (int)bridge["instanceId"] + " rebuild has outward fitted and support faces", passed);
            observations.Add(new { name = "retained centreline reconstruction", sourceCensus = censusPath, bridgeInstanceId = (int)bridge["instanceId"],
                recordedSamples = road.Count, distinctSamples = clean.Count, width, passed });
            count++;
        }
        check("retained census " + Path.GetFileName(Path.GetDirectoryName(censusPath)) + " supplies all twenty fitted bridge profiles", count == 20);
    }

    private static Vector3 ReadPoint(JToken point)
    {
        return new Vector3((float)point["x"], (float)point["y"], (float)point["z"]);
    }

    private static void VerifyRecordedFootprint(MethodInfo buildMesh, MethodInfo buildShoulder,
        Action<string, bool> check, List<object> observations)
    {
        // note: The original centerline fixture missed steep lateral faces. Rebuild the recorded production profile and query the exact four-footprint rays without changing the capsule's slope limit.
        JObject record = JObject.Parse(File.ReadAllText(RecordedFootprintProfile));
        check("recorded footprint provenance hashes match runtime evidence",
            Hash((string)record["census"]) == (string)record["censusSha256"] &&
            Hash((string)record["warningFile"]) == (string)record["warningFileSha256"] &&
            Hash((string)record["discovery"]) == (string)record["discoverySha256"]);
        JArray input = (JArray)record["road"], captured = (JArray)record["capturedVertices"];
        var road = new Vector3[input.Count];
        for (int index = 0; index < input.Count; index++) road[index] = ReadPoint(input[index]);
        float width = (float)record["width"], slopeLimit = (float)record["capsule"]["slope"];
        foreach (MethodInfo builder in new[] { buildMesh, buildShoulder })
        {
            Mesh mesh = null; GameObject owner = null;
            string name = "recorded buffered footprint " + (builder == buildMesh ? "fitted" : "support");
            try
            {
                // note: These isolated construction/cooking measurements diagnose the repair's cost; they cannot certify production frame budgets.
                var construction = System.Diagnostics.Stopwatch.StartNew();
                mesh = (Mesh)builder.Invoke(null, new object[] { road, width });
                construction.Stop();
                Vector3[] vertices = mesh.vertices;
                bool anchors = true;
                float offset = builder == buildMesh ? .22f : .21f;
                foreach (Vector3 point in road)
                {
                    bool recovered = false, retained = false;
                    foreach (JToken v in captured) recovered |= Vector3.Distance(ReadPoint(v), point + Vector3.up * .22f) < .0001f;
                    foreach (Vector3 v in vertices) retained |= Vector3.Distance(v, point + Vector3.up * offset) < .0001f;
                    anchors &= recovered && retained;
                }
                check(name + " retains each uniquely recovered original centerline anchor", anchors);
                float maximumSlope = 0f;
                int[] top = mesh.GetTriangles(0);
                for (int index = 0; index < top.Length; index += 3)
                {
                    Vector3 normal = Vector3.Cross(vertices[top[index + 1]] - vertices[top[index]],
                        vertices[top[index + 2]] - vertices[top[index]]);
                    maximumSlope = Mathf.Max(maximumSlope, Vector3.Angle(normal, Vector3.up));
                }
                check(name + " top faces obey the recorded production capsule slope", maximumSlope <= slopeLimit);
                owner = new GameObject("RecordedBufferedFootprintFixture");
                var cooking = System.Diagnostics.Stopwatch.StartNew();
                MeshCollider collider = owner.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                cooking.Stop();
                Physics.SyncTransforms();
                int misses = 0, steep = 0;
                float maximumRaySlope = 0f;
                foreach (JToken p in record["probes"])
                {
                    bool hit = collider.Raycast(new Ray(ReadPoint(p["origin"]), Vector3.down), out RaycastHit contact, (float)p["distance"]);
                    if (!hit) { misses++; continue; }
                    float slope = Vector3.Angle(contact.normal, Vector3.up);
                    maximumRaySlope = Mathf.Max(maximumRaySlope, slope);
                    if (slope > slopeLimit) steep++;
                }
                check(name + " native rays support the recorded capsule footprint within its slope limit", misses == 0 && steep == 0);
                // note: Rebuild equality is diagnostic baseline evidence; a later causal geometry repair is expected to change triangulation while retaining the pinned road anchors.
                bool matchesCaptured = builder == buildMesh && vertices.Length == captured.Count;
                if (matchesCaptured) for (int index = 0; index < vertices.Length; index++)
                    matchesCaptured &= Vector3.Distance(vertices[index], ReadPoint(captured[index])) < .0001f;
                if (matchesCaptured) for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    int[] triangles = mesh.GetTriangles(submesh);
                    JArray expected = (JArray)record["capturedSubmeshes"][submesh];
                    matchesCaptured &= triangles.Length == expected.Count;
                    if (triangles.Length == expected.Count) for (int index = 0; index < triangles.Length; index++)
                        matchesCaptured &= triangles[index] == (int)expected[index];
                }
                observations.Add(new { name, roadSamples = road.Length, anchors, vertices = vertices.Length,
                    maximumSlope, maximumRaySlope, misses, steep, slopeLimit, matchesCaptured,
                    constructionMilliseconds = construction.Elapsed.TotalMilliseconds,
                    cookingMilliseconds = cooking.Elapsed.TotalMilliseconds });
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }

    private static bool RebuildMatchesClean(MethodInfo buildMesh, MethodInfo buildShoulder, Vector3[] input, Vector3[] clean, float width)
    {
        // note: Compare complete indexed geometry and input preservation; a top-face sign alone could pass after an unrelated width or bank change.
        Vector3[] before = (Vector3[])input.Clone();
        bool passed = true;
        foreach (MethodInfo builder in new[] { buildMesh, buildShoulder })
        {
            Mesh actual = null, expected = null;
            try
            {
                actual = (Mesh)builder.Invoke(null, new object[] { input, width });
                expected = (Mesh)builder.Invoke(null, new object[] { clean, width });
                bool capped = builder == buildShoulder;
                if (actual == null || expected == null || !FacesPointOutward(actual, clean, capped)) { passed = false; continue; }
                Vector3[] a = actual.vertices, b = expected.vertices;
                int[] at = actual.triangles, bt = expected.triangles;
                if (a.Length != b.Length || at.Length != bt.Length) { passed = false; continue; }
                for (int i = 0; i < a.Length; i++) passed &= Vector3.Distance(a[i], b[i]) < .0001f;
                for (int i = 0; i < at.Length; i++) passed &= at[i] == bt[i];
            }
                finally
            {
                if (actual != null) UnityEngine.Object.DestroyImmediate(actual);
                if (expected != null) UnityEngine.Object.DestroyImmediate(expected);
            }
        }
        for (int i = 0; i < input.Length; i++) passed &= input[i].Equals(before[i]);
        return passed;
    }

    private static void Drain(IEnumerator routine)
    {
        // note: Run the existing cooperative routine without skipping its nested LOD pass; a bounded fixture cannot spin indefinitely.
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int iterations = 0;
        while (stack.Count > 0)
        {
            if (++iterations > 10000) throw new InvalidOperationException("Bridge fixture did not terminate.");
            IEnumerator current = stack.Peek();
            if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); }
            else if (current.Current is IEnumerator nested) stack.Push(nested);
        }
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
