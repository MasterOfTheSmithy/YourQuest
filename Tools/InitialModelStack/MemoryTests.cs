using System;

internal static class MemoryTests
{
    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    private static void Main()
    {
        var reports = new[] { "player: The sword is in the chapel.", "npc: The ferry crosses near the mill.", "player: The guard needs bandages." };
        Require(YQDialogueMemorySelection.Render(reports, new[] { 2, 0 }, new[] { .8f, .9f }) == reports[0] + Environment.NewLine + reports[2] + Environment.NewLine,
            "retrieved reports retain original text and chronological order");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 0, 0 }, new[] { .9f, .9f }) == "", "duplicate references rejected");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 3 }, new[] { .9f }) == "", "foreign or invented reference rejected");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 0 }, new[] { float.NaN }) == "", "non-finite scores rejected");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 0 }, new[] { 1.1f }) == "", "invalid cosine scores rejected");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 0 }, new[] { .1f }) == "", "weak association contributes no old report");
        Require(YQDialogueMemorySelection.Render(reports, new[] { 0 }, new float[0]) == "", "malformed envelope rejected");
        Require(YQDialogueMemorySelection.Render(null, new[] { 0 }, new[] { .9f }) == "", "unavailable retrieval preserves recent-dialogue fallback");
        Require(YQDialogueMemorySelection.Compose("", "npc: Recent words") == "npc: Recent words", "no retrieval preserves recent context exactly");
        Require(YQDialogueMemorySelection.Render(new[] { new string('x', 1801) }, new[] { 0 }, new[] { .9f }) == "", "old-report context budget enforced");
    }
}
