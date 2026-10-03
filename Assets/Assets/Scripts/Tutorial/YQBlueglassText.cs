using System;
using System.Text;

// note: Presentation only: accepted records remain unchanged and mechanics always come from typed fields.
public static class YQBlueglassText
{
    public const string Ink = "E8F4FA";
    public const string Quiet = "A9BFCE";
    public const string Positive = "94E0B3";
    public const string Negative = "FFAC9D";
    public const string Mana = "93CFFF";
    public const string Stamina = "E7D397";
    public const string Health = "F0AAA4";

    public static string Escape(string value)
    {
        // note: Basic atlas-safe brackets preserve readable text without allowing generated TMP tags to hide other UI.
        return (value ?? string.Empty).Replace('<', '[').Replace('>', ']');
    }

    public static string Description(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var result = new StringBuilder(value.Length);
        foreach (string line in value.Replace("\r", string.Empty).Split('\n'))
        {
            string trimmed = line.Trim();
            // note: Remove explicit authoring metadata lines, never infer mechanics or strip words from ordinary lore.
            int separator = trimmed.IndexOfAny(new[] { ':', '=' });
            string key = separator > 0 ? trimmed.Substring(0, separator).Trim().ToLowerInvariant() : string.Empty;
            switch (key)
            {
                case "context": case "environment": case "confidence": case "generation source":
                case "generationsource": case "prompt hash": case "generatorprompthash":
                case "effectkey": case "prefabkey": case "iconkey": case "familykey":
                case "run seed": case "level seed": case "payloadjson":
                    continue;
            }
            if (result.Length > 0) result.Append('\n');
            result.Append(Escape(trimmed));
        }
        return result.ToString().Trim();
    }

    public static string Tint(string text, string color) => "<color=#" + color + ">" + text + "</color>";
    public static void Section(StringBuilder body, string label)
    {
        body.Append('\n').Append("<size=85%><b>").Append(Tint(label.ToUpperInvariant(), Quiet)).AppendLine("</b></size>");
    }
    public static void Value(StringBuilder body, string label, string value, string color = Ink)
    {
        body.Append(Tint(Escape(label), Quiet)).Append("  <b>").Append(Tint(Escape(value), color)).AppendLine("</b>");
    }
    public static void Bonus(StringBuilder body, string label, float value)
    {
        if (Math.Abs(value) < .001f) return;
        Value(body, label, value.ToString("+0.##;-0.##;0"), value > 0 ? Positive : Negative);
    }
    public static void Flavour(StringBuilder body, string description)
    {
        string prose = Description(description);
        if (prose.Length == 0) return;
        Section(body, "Description");
        body.Append(Tint(prose, Quiet));
    }
    public static string RarityColor(string rarity)
    {
        switch ((rarity ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "uncommon": return Positive;
            case "rare": return Mana;
            case "epic": return "CCA9EF";
            case "legendary": return "F4C786";
            case "mythic": case "unique": return "F3A9BF";
            default: return Ink;
        }
    }
    public static string ResourceColor(string resource)
    {
        switch((resource ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "health": return Health;
            case "mana": return Mana;
            case "stamina": return Stamina;
            default: return Ink;
        }
    }
}
