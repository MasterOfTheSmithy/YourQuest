using System;
using System.Collections.Generic;
using System.Text;

public static class YQDialogueMemorySelection
{
    public static string Compose(string recalled, string recent) => Compose(recalled, recent, null);

    public static string Compose(string recalled, string recent, string protectedReports)
    {
        // note: Retain original speakers/order; lexical retention is not a factual correction, promise execution or new memory store.
        var context = new StringBuilder(4000);
        if (!string.IsNullOrEmpty(protectedReports))
            context.Append("OLDER_REPORTED_CORRECTIONS_OR_COMMITMENTS (lexically selected attributed turns; not verified facts or fulfilled actions):\n").Append(protectedReports);
        if (!string.IsNullOrEmpty(recalled))
            context.Append("\nRECALLED_OLDER_REPORTS (chronological; attributed speech, not verified world facts):\n").Append(recalled);
        context.Append("\nRECENT_TURNS (chronological):\n").Append(recent ?? string.Empty);
        return context.ToString();
    }

    public static string RenderProtectedReports(IReadOnlyList<DialogueTurn> turns, int recentCount)
    {
        // note: A bounded older quote can retain a literal correction or commitment even when optional semantic retrieval is off/unavailable.
        if (turns == null) return string.Empty;
        int end = Math.Max(0, turns.Count - Math.Max(0, recentCount));
        var selected = new List<string>(4);
        int total = 0;
        for (int index = end - 1; index >= 0 && selected.Count < 4; index--)
        {
            DialogueTurn turn = turns[index];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text) || turn.text.Length > 600 ||
                (turn.speaker != "player" && turn.speaker != "npc")) continue;
            string matchText = turn.text.Replace('\u2019', '\'');
            if (!System.Text.RegularExpressions.Regex.IsMatch(matchText,
                @"\b(?:not|instead|meant|promise[ds]?|until|before)\b|\bI(?:'ll| will)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            string line = "retainedTurn[" + index + "] " + turn.speaker + ": " + turn.text;
            if (total + line.Length + 1 > 1800) continue;
            selected.Add(line);
            total += line.Length + 1;
        }
        selected.Reverse();
        return selected.Count == 0 ? string.Empty : string.Join("\n", selected) + "\n";
    }

    public static string Render(string[] candidates, int[] indices, float[] scores)
    {
        // note: The worker selects references only; original attributed text and chronological order remain canonical.
        if (candidates == null || indices == null || scores == null || indices.Length != scores.Length || indices.Length > 4)
            return string.Empty;
        var selected = new SortedSet<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= candidates.Length || float.IsNaN(scores[i]) || float.IsInfinity(scores[i]) ||
                scores[i] < -1.001f || scores[i] > 1.001f || !selected.Add(indices[i])) return string.Empty;
        }
        var output = new StringBuilder(1800);
        foreach (int index in selected)
        {
            int position = Array.IndexOf(indices, index);
            if (scores[position] < .25f || string.IsNullOrWhiteSpace(candidates[index])) continue;
            string line = "retainedTurn[" + index + "] " + candidates[index];
            if (output.Length + line.Length + 1 > 1800) continue;
            output.AppendLine(line);
        }
        return output.ToString();
    }
}
