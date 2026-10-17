using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

public static class BetaFixtureProbe
{
    public static int Main(string[] args)
    {
        // note: Reuse the exact Editor regression's semantic input against the identity-checked production assembly; no Unity scene, save or native object is touched.
        var tests = Assembly.LoadFrom(args[1]).GetType("YQSpatialBlueprintV2Tests", true);
        var factory = tests.GetMethod("BuildSemanticPlan", BindingFlags.NonPublic | BindingFlags.Static);
        var plan = (GeneratedWorldPlanRecord)factory.Invoke(null, new object[] { false });
        plan.worldSeed = YQBetaDevelopmentFixture.CanonicalWorldSeed;
        plan.spatialPlan.worldSeed = plan.worldSeed;
        bool accepted = YQSpatialBlueprintCompilerV2.TryCompile(plan, out var compiled, out string failure);
        bool reproduced = !accepted && failure != null && failure.Contains(
            "OverlappingSiteReserve:site:continuation:poi:east:near|site:settlement:settlement:north_crossing",
            StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(args[0], "beta-fixture-reproduction.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow.ToString("O"), evidence = "DETACHED_COMPILED_EXACT_EDITOR_INPUT_REPRODUCTION",
            generationVersion = GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
            validationVersion = GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
            seed = plan.worldSeed, accepted, failure, reproduced, runtimeVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((reproduced ? "REPRODUCED " : "UNEXPECTED ") + failure);
        return reproduced ? 0 : 1;
    }
}
