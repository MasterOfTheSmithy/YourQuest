using System.Collections.Generic;
using System.Text;

public static class EventSummarizer
{
    public static string Summarize(List<ActionEvent> events)
    {
        if (events == null || events.Count == 0) return string.Empty;

        var counts = new Dictionary<string, int>();
        var weight = new Dictionary<string, float>();

        foreach (var ev in events)
        {
            if (!counts.ContainsKey(ev.Verb))
            {
                counts[ev.Verb] = 0;
                weight[ev.Verb] = 0f;
            }
            counts[ev.Verb]++;
            weight[ev.Verb] += ev.Significance;
        }

        var sb = new StringBuilder();
        foreach (var kv in counts)
            sb.AppendLine($"{kv.Key}: {kv.Value} occurrences, total significance {weight[kv.Key]:0.00}");

        return sb.ToString();
    }
}
