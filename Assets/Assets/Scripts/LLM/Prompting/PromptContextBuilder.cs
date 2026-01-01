using System.Text;

public static class PromptContextBuilder
{
    public static string BuildContext(
        string taskInstruction,
        string outputSchemaBlock,
        string recentSummary,
        string behaviorLedger
    )
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are the System Director for an offline single-player RPG.");
        sb.AppendLine("Follow the rules strictly. Output must match the schema exactly.");
        sb.AppendLine("No markdown. No explanations outside JSON.");
        sb.AppendLine();

        // World snapshot (always)
        var wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.state != null)
        {
            sb.AppendLine(WorldMemoryRenderer.Render(wsm.state));
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("WORLD_SNAPSHOT\n<missing>");
            sb.AppendLine();
        }

        // Player snapshot (always)
        var psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null)
        {
            sb.AppendLine(PlayerMemoryRenderer.Render(psm.state));
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("PLAYER_SNAPSHOT\n<missing>");
            sb.AppendLine();
        }

        // Observations
        sb.AppendLine("RECENT_ACTIONS_SUMMARY");
        sb.AppendLine(string.IsNullOrWhiteSpace(recentSummary) ? "<none>" : recentSummary);
        sb.AppendLine();

        sb.AppendLine("BEHAVIOR_LEDGER");
        sb.AppendLine(string.IsNullOrWhiteSpace(behaviorLedger) ? "<none>" : behaviorLedger);
        sb.AppendLine();

        // Task
        sb.AppendLine("TASK");
        sb.AppendLine(taskInstruction);
        sb.AppendLine();

        sb.AppendLine("OUTPUT_SCHEMA");
        sb.AppendLine(outputSchemaBlock);

        return sb.ToString();
    }

    public static string WrapJsonSchema(string schemaJson)
    {
        return $"Return ONLY valid JSON.\n{schemaJson}";
    }
}
