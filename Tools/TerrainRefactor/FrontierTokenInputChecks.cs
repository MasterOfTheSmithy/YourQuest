using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

static class FrontierTokenInputChecks
{
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Type Token = typeof(YQGeneratedWorldRuntimeBuilder).GetNestedType("FrontierConstructionAcceptance", Fields);
    static readonly MethodInfo MatchMethod = Token.GetMethod("CanonicalInputsMatch", Fields);
    static readonly JsonSerializerSettings Settings = (JsonSerializerSettings)typeof(YQGeneratedWorldRuntimeBuilder).GetField("FrontierConstructionJsonSettings", Fields).GetValue(null);
    static int checks, failures;
    static readonly List<string> Trace = new List<string>();
    static Action<string> Callback;
    static void Check(bool ok, string name) { checks++; if (!ok) failures++; Console.WriteLine((ok ? "PASS " : "FAIL ") + name); }
    static GeneratedWorldPlanRecord Fixture() => JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Environment.GetEnvironmentVariable("YQ_TOKEN_FIXTURE")), Settings);
    static object Subset(GeneratedWorldPlanRecord p) => new { p.worldSeed, p.spatialPlan, p.settlements, p.encampments };
    static string Candidate(GeneratedWorldPlanRecord p) => JsonConvert.SerializeObject(new { p.worldSeed, p.spatialPlan, p.settlements, p.encampments, spatialPlanV2 = p.spatialPlanV2 }, Settings);
    static bool Original(GeneratedWorldPlanRecord p, string subset, string parent) {
        string currentParent = JsonConvert.SerializeObject(p.spatialPlanV2, Settings);
        return string.Equals(JsonConvert.SerializeObject(Subset(p), Settings) + "|" + currentParent, subset + "|" + parent, StringComparison.Ordinal);
    }
    static bool Proposed(GeneratedWorldPlanRecord p, string subset, string parent) {
        try { return (bool)MatchMethod.Invoke(null, new object[] { p, p.spatialPlanV2, subset, parent }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    sealed class TraceResolver : DefaultContractResolver {
        protected override JsonContract CreateContract(Type type) {
            var c = base.CreateContract(type);
            string role = type == typeof(GeneratedSpatialWorldPlanV2Record) ? "parent" :
                type.Name.Contains("AnonymousType") && type.GetProperty("worldSeed") != null ? "subset" : null;
            if (role != null) c.OnSerializingCallbacks.Add((o, ctx) => { Trace.Add(role); Callback?.Invoke(role); });
            return c;
        }
    }
    static void Pair(Action<GeneratedWorldPlanRecord> mutate, bool expected, string name) {
        JsonConvert.DefaultSettings = null; var p = Fixture(); string subset = JsonConvert.SerializeObject(Subset(p), Settings), parent = JsonConvert.SerializeObject(p.spatialPlanV2, Settings); mutate?.Invoke(p);
        bool old = Original(p, subset, parent), candidate = Proposed(p, subset, parent);
        Check(old == expected && candidate == old, name);
    }
    static void Main() {
        Pair(null, true, "exact current compiler inputs match captured components");
        Pair(p => p.worldSeed += "changed", false, "same object changed seed rejected");
        Pair(p => p.spatialPlan.semanticFingerprint += "changed", false, "same semantic object deep fingerprint mutation rejected");
        Pair(p => p.spatialPlanV2.blueprint.routes[0].controlPoints[0].x += .125f, false, "deep geometry mutation rejected despite unchanged claimed hashes");
        Pair(p => p.spatialPlanV2.blueprint.hydrology[0].controlPoints[0].z += .125f, false, "deep water mutation rejected despite same parent reference");
        Pair(p => p.spatialPlanV2.contentHash += "changed", false, "parent identity bytes rejected");
        Pair(p => p.settlements.Clear(), false, "canonical settlement list contents rejected");
        Pair(p => p.encampments.Clear(), false, "canonical encampment list contents rejected");
        Pair(p => p.spatialPlanV2 = null, false, "removed parent rejected by exact comparison");
        Pair(p => p.settlements = null, false, "null differs from empty canonical list");
        Pair(p => p.spatialPlanV2 = JsonConvert.DeserializeObject<GeneratedSpatialWorldPlanV2Record>(JsonConvert.SerializeObject(p.spatialPlanV2, Settings), Settings), true, "byte identical detached parent matches numerical proof");
        var special = Fixture(); special.worldSeed = "a|b\\\"c\n\u03bb"; string specialSubset = JsonConvert.SerializeObject(Subset(special), Settings), specialParent = JsonConvert.SerializeObject(special.spatialPlanV2, Settings);
        Check(Original(special, specialSubset, specialParent) && Proposed(special, specialSubset, specialParent), "embedded delimiter escapes Unicode and newline do not require parsing");
        Check(!Proposed(special, specialSubset + " ", specialParent), "trailing component whitespace remains ordinal mismatch");
        Check(!Proposed(special, specialSubset, specialParent + " "), "parent trailing whitespace remains ordinal mismatch");
        // note: Use the serializer's existing DefaultSettings callback boundary to compare full callback order, even when the first component mismatches.
        foreach (bool mismatch in new[] { false, true }) {
            JsonConvert.DefaultSettings = null; var p = Fixture(); string subset = JsonConvert.SerializeObject(Subset(p), Settings), parent = JsonConvert.SerializeObject(p.spatialPlanV2, Settings); if (mismatch) parent += " ";
            Callback = null; JsonConvert.DefaultSettings = () => new JsonSerializerSettings { ContractResolver = new TraceResolver() };
            Trace.Clear(); bool old = Original(p, subset, parent); string trace = string.Join(",", Trace);
            Trace.Clear(); bool current = Proposed(p, subset, parent); string candidateTrace = string.Join(",", Trace);
            Check(current == old && trace == "parent,subset" && candidateTrace == trace, "both full callbacks parent first retained mismatch=" + mismatch);
        }
        JsonConvert.DefaultSettings = null; var callbackPlan = Fixture(); string callbackSubset = JsonConvert.SerializeObject(Subset(callbackPlan), Settings), callbackParent = JsonConvert.SerializeObject(callbackPlan.spatialPlanV2, Settings), initialSeed = callbackPlan.worldSeed;
        Callback = role => { if (role == "parent") callbackPlan.worldSeed = initialSeed + "providerChanged"; }; JsonConvert.DefaultSettings = () => new JsonSerializerSettings { ContractResolver = new TraceResolver() };
        callbackPlan.worldSeed = initialSeed; bool oldDrift = Original(callbackPlan, callbackSubset, callbackParent); callbackPlan.worldSeed = initialSeed; bool candidateDrift = Proposed(callbackPlan, callbackSubset, callbackParent);
        Check(!oldDrift && !candidateDrift, "parent callback mutation of later current subset rejected on both paths");
        Callback = role => { if (role == "subset") throw new InvalidOperationException("late serializer fault"); };
        bool oldThrows = false, candidateThrows = false; try { Original(callbackPlan, callbackSubset, callbackParent + " "); } catch (Exception) { oldThrows = true; }
        try { Proposed(callbackPlan, callbackSubset, callbackParent + " "); } catch (Exception) { candidateThrows = true; }
        Check(oldThrows && candidateThrows, "late callback exception after parent mismatch still observed");
        Callback = null; JsonConvert.DefaultSettings = null;
        // note: Only an inert non-null projection marker is used for this private exact-envelope helper guard; no compiler or publication is simulated.
        var token = new SourceCandidateToken(); var p2 = Fixture(); token.preparedProjection = (YQPreparedSpatialMaterializationV2)RuntimeHelpers.GetUninitializedObject(typeof(YQPreparedSpatialMaterializationV2));
        token.preparedCandidateInputsJson = Candidate(p2);
        Check(token.HasPreparedCandidate(p2), "source-extracted HasPreparedCandidate matches captured complete envelope"); p2.spatialPlanV2.blueprint.routes[0].controlPoints[0].z += 1;
        Check(!token.HasPreparedCandidate(p2), "source-extracted candidate helper rejects deep geometry mutation");
        Check(!token.HasPreparedCandidate(null), "candidate helper retains null guard"); token.preparedProjection = null;
        Check(!token.HasPreparedCandidate(Fixture()), "candidate helper retains absent numeric projection guard");
        // note: This measures allocation only in a managed fixture. The baseline uses the already-captured joined string, exactly as production does; no extra expected-string allocation is charged to it.
        var measured = Fixture(); string measuredSubset = JsonConvert.SerializeObject(Subset(measured), Settings), measuredParent = JsonConvert.SerializeObject(measured.spatialPlanV2, Settings), capturedJoined = measuredSubset + "|" + measuredParent;
        bool BaselineProof() { string parent = JsonConvert.SerializeObject(measured.spatialPlanV2, Settings); return string.Equals(JsonConvert.SerializeObject(Subset(measured), Settings) + "|" + parent, capturedJoined, StringComparison.Ordinal); }
        Check(BaselineProof() && Proposed(measured, measuredSubset, measuredParent), "allocation fixture compares the same already-captured canonical envelope");
        int iterations = 5; long allocated = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < iterations; i++) BaselineProof(); long originalBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        allocated = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < iterations; i++) Proposed(measured, measuredSubset, measuredParent); long candidateBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        File.WriteAllText(Environment.GetEnvironmentVariable("YQ_TOKEN_ALLOCATION"), JsonConvert.SerializeObject(new { iterations, parentCharacters = measuredParent.Length, subsetCharacters = measuredSubset.Length, originalBytes, candidateBytes, bytesSavedPerCall = (originalBytes - candidateBytes) / iterations, evidence = "MANAGED_FIXTURE_ALLOCATION_ONLY; NO_UNITY_TIME_OR_HITCH_ESTIMATE" }, Formatting.Indented));
        Console.WriteLine("RESULT checks=" + checks + " failures=" + failures); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
