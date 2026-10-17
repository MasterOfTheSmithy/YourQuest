using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

// note: Exercise production cache-retirement admission; no Unity object, save, asset, model or GPU allocation is changed.
public static class GameCacheBudgetChecks
{
    public static int Main(string[] args)
    {
        var policy = typeof(LLMClient).Assembly.GetType("YQLlmResidencyPolicy");
        var retire = policy.GetMethod("CanRetireGameCache", BindingFlags.Static | BindingFlags.NonPublic);
        var cases = new[] {
            ("just_used", 100f, 100f, 30f, false),
            ("still_warm", 129f, 100f, 30f, false),
            ("cold_boundary", 130f, 100f, 30f, true),
            ("long_dormant", 500f, 100f, 30f, true),
            ("clock_restarted", 2f, 100f, 30f, false),
            ("unknown_now", float.NaN, 100f, 30f, false),
            ("unknown_use", 100f, float.NaN, 30f, false),
            ("infinite_clock", float.PositiveInfinity, 100f, 30f, false),
            ("infinite_use", 100f, float.NegativeInfinity, 30f, false),
            ("negative_window_still_protects_new_load", 100f, 100f, -1f, false)
        };
        var results = new List<object>();
        foreach (var c in cases)
        {
            bool actual = (bool)retire.Invoke(null, new object[] { c.Item2, c.Item3, c.Item4 });
            bool pass = actual == c.Item5;
            results.Add(new { id = c.Item1, pass });
            if (!pass) throw new Exception(c.Item1);
        }
        // note: Exercise the host-pressure boundary and inference protection independently of native collection.
        var reclaim = policy.GetMethod("ShouldReclaimGameSystemMemory", BindingFlags.Static | BindingFlags.NonPublic);
        const long mib = 1024L * 1024L;
        var pressureCases = new[] {
            ("ram_unknown", 0L, 5000L * mib, false, false),
            ("ram_negative", -1L, 5000L * mib, false, false),
            ("ram_above_reserve", 2048L * mib + 1, 5000L * mib, false, false),
            ("ram_pressure_boundary", 2048L * mib, 4096L * mib, false, true),
            ("ram_small_game", 512L * mib, 4096L * mib - 1, false, false),
            ("ram_inference_protected", 512L * mib, 10000L * mib, true, false)
        };
        foreach (var c in pressureCases)
        {
            bool actual = (bool)reclaim.Invoke(null, new object[] { c.Item2, c.Item3, c.Item4 });
            bool pass = actual == c.Item5;
            results.Add(new { id = c.Item1, pass });
            if (!pass) throw new Exception(c.Item1);
        }
        File.WriteAllText(Path.Combine(args[0], "checks.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
        Console.WriteLine("PASS " + results.Count + " cache retirement checks; native reclamation remains a separate runtime gate.");
        return 0;
    }
}
