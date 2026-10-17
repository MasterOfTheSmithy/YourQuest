using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

// note: Exercise the compiled production route splice with synthetic immutable layouts; reflection belongs only to this detached verifier.
internal static class SharedRouteChecks
{
    private static readonly Type Owner = typeof(YQContinuousWorldFeatureMaterializer);
    private static readonly Type Span = Owner.GetNestedType("AcceptedCrossingSpan", BindingFlags.NonPublic);
    private static readonly Type Layout = Owner.GetNestedType("AcceptedBridgeLayout", BindingFlags.NonPublic);
    private static readonly MethodInfo Apply = Owner.GetMethod("ApplySharedBridgeRoute", BindingFlags.NonPublic | BindingFlags.Static);
    private static int failures;
    private static Vector3 P(float x, float z = 0f) => new Vector3(x, 0f, z);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static YQPreparedSpatialMaterializationV2 Prepared(int version)
    {
        var basin = (YQPreparedConnectedBasinV2)RuntimeHelpers.GetUninitializedObject(typeof(YQPreparedConnectedBasinV2));
        Set(basin, "domainVersion", version);
        var prepared = (YQPreparedSpatialMaterializationV2)RuntimeHelpers.GetUninitializedObject(typeof(YQPreparedSpatialMaterializationV2));
        Set(prepared, "waters", new[] { new YQSpatialMaterializationWaterV2 { connectedBasin = basin } });
        return prepared;
    }
    private static object Crossing(float start, float end, bool sameBank = false)
    {
        var bridge = Activator.CreateInstance(Span, true);
        Set(bridge, "points", new List<Vector3> { P(start, 4f), P((start + end) * .5f, 4f), P(end, 4f) });
        var span = Activator.CreateInstance(Span, true);
        Set(span, "crossingId", "synthetic:" + start); Set(span, "routeIndex", 0); Set(span, "bridge", bridge);
        Set(span, "points", new List<Vector3> { P(start), P(end) });
        var banks = (IList)Span.GetField("bankConnections").GetValue(span);
        banks.Add(new List<Vector3> { P(start), P(start, 2f), P(start, 4f) });
        banks.Add(new List<Vector3> { P(end), P(end, 2f), P(sameBank ? start : end, 4f) });
        return span;
    }
    private static void Install(YQPreparedSpatialMaterializationV2 prepared, params object[] spans)
    {
        var layout = Activator.CreateInstance(Layout, true); Set(layout, "ready", true);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Span));
        foreach (var span in spans) list.Add(span);
        Set(layout, "spans", list);
        object cache = Owner.GetField("BridgeLayouts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        cache.GetType().GetMethod("Add").Invoke(cache, new[] { (object)prepared, layout });
    }
    private static List<Vector2> Line() => new List<Vector2> { new Vector2(0f,0f), new Vector2(5f,0f), new Vector2(10f,0f) };
    private static void Run(YQPreparedSpatialMaterializationV2 prepared, List<Vector2> line) => Apply.Invoke(null, new object[] { prepared, 0, line });
    private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Reject(Action action)
    {
        try { action(); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
        throw new Exception("Unsafe layout was not rejected.");
    }
    private static void Check(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    public static int Main()
    {
        Check("SHARED-ROUTE-COMPLETE-BANKS-DECK-AND-SEMANTIC-ENDPOINTS", () => {
            var prepared = Prepared(8); object span = Crossing(2f,8f); Install(prepared,span); var line = Line(); Run(prepared,line);
            Require(line[0] == new Vector2(0f,0f) && line[line.Count-1] == new Vector2(10f,0f), "Endpoint identity changed.");
            foreach (var point in new[] { new Vector2(2f,2f), new Vector2(2f,4f), new Vector2(5f,4f), new Vector2(8f,4f), new Vector2(8f,2f) })
                Require(line.Contains(point), "A published bank/deck leg was omitted: " + point);
            Require(!line.Contains(new Vector2(5f,0f)), "Obsolete wet centre remained the validation corridor.");
            Require(((List<Vector3>)Span.GetField("points").GetValue(span)).Count == 2, "Cached geometry mutated.");
        });
        Check("SHARED-ROUTE-SAME-CONNECTED-BANK", () => {
            var prepared=Prepared(8); Install(prepared,Crossing(2f,8f,true)); var line=Line(); Run(prepared,line);
            Require(line.Contains(new Vector2(8f,2f)) && !line.Contains(new Vector2(5f,4f)), "Same-bank path acquired an unrelated crossing.");
        });
        Check("SHARED-ROUTE-MULTIPLE-INTERVALS-KEEP-MIDDLE-ROAD", () => {
            var prepared=Prepared(8); Install(prepared,Crossing(6f,8f),Crossing(2f,4f)); var line=Line(); Run(prepared,line);
            Require(line.Contains(new Vector2(5f,0f)) && line.Contains(new Vector2(3f,4f)) && line.Contains(new Vector2(7f,4f)), "Required route sections omitted.");
        });
        Check("SHARED-ROUTE-OVERLAP-REJECTS", () => { var prepared=Prepared(8); Install(prepared,Crossing(2f,7f),Crossing(6f,8f)); Reject(()=>Run(prepared,Line())); });
        Check("SHARED-ROUTE-MISSING-LAYOUT-REJECTS", () => Reject(()=>Run(Prepared(8),Line())));
        Check("SHARED-ROUTE-ARCHIVED-PROJECTION-PRESERVED", () => { var line=Line(); Run(Prepared(7),line); Require(line.Count==3 && line[1]==new Vector2(5f,0f), "Archived route changed."); });
        return failures == 0 ? 0 : 1;
    }
}
