using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public readonly struct LLMCompiledPrompt
{
    public readonly string prompt;
    public readonly int estimatedInputTokens;
    public readonly int reservedOutputTokens;
    public readonly int contextLimitTokens;
    public readonly bool reduced;
    public readonly string diagnostic;

    public LLMCompiledPrompt(
        string prompt,
        int estimatedInputTokens,
        int reservedOutputTokens,
        int contextLimitTokens,
        bool reduced,
        string diagnostic)
    {
        this.prompt = prompt;
        this.estimatedInputTokens = estimatedInputTokens;
        this.reservedOutputTokens = reservedOutputTokens;
        this.contextLimitTokens = contextLimitTokens;
        this.reduced = reduced;
        this.diagnostic = diagnostic ?? string.Empty;
    }
}

public static class LLMContextCompiler
{
    private static readonly string[] ReducibleMarkers =
    {
        "RETRIEVED_LONG_TERM_MEMORY:",
        "RECENT_DIALOGUE:",
        "SITUATION_SNAPSHOT:",
        "GENERATED_WORLD_PLAN (compact)",
        "WORLD_NOTE:",
        "RECENT_INTERACTION_HISTORY:",
        "OPTIONAL_CONTEXT:"
    };

    private static readonly string[] CriticalMarkers =
    {
        "PLAYER_MESSAGE:",
        "QUESTIONNAIRE_ANSWERS",
        "CHARACTER_CREATION",
        "OUTPUT CONTRACT",
        "Return JSON",
        "JSON ONLY",
        "SCHEMA",
        "NPC:",
        "CURRENT_OBJECTIVE:"
    };

    public static bool TryCompile(
        string rawPrompt,
        LLMRuntimeConfig config,
        LLMGenerationProfile profile,
        int reservedOutputTokens,
        out LLMCompiledPrompt compiled,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPrompt))
        {
            compiled = default;
            error = "Prompt was empty.";
            return false;
        }

        LLMRuntimeConfig activeConfig = config != null ? config : LLMRuntimeConfig.CreateRuntimeDefault();
        int contextLimit = Mathf.Clamp(activeConfig.contextSizeTokens, 2048, 32768);
        int reserve = Mathf.Clamp(reservedOutputTokens, 64, Mathf.Max(64, contextLimit / 2));
        int safety = Mathf.Clamp(activeConfig.contextSafetyTokens, 64, 2048);
        int inputBudget = Mathf.Max(256, contextLimit - reserve - safety);
        string prompt = NormalizeLineEndings(rawPrompt);
        bool reduced = false;
        StringBuilder diagnostic = new StringBuilder(128);

        prompt = ApplyDirectModePrefix(prompt, activeConfig, profile, ref reduced);
        int hardCharacterLimit = Mathf.Clamp(activeConfig.hardPromptCharacterLimit, 5000, 50000);
        if (prompt.Length > hardCharacterLimit)
        {
            // note: Enforce the explicit payload ceiling before token reduction so local HTTP requests stay bounded and debuggable.
            prompt = CompactMiddle(prompt, hardCharacterLimit);
            reduced = true;
            diagnostic.Append("applied hard prompt character limit; ");
        }

        int estimate = EstimateTokens(prompt);
        if (estimate <= inputBudget)
        {
            compiled = new LLMCompiledPrompt(prompt, estimate, reserve, contextLimit, reduced, diagnostic.ToString());
            return true;
        }

        string deduped = RemoveDuplicateNonCriticalLines(prompt);
        if (!string.Equals(deduped, prompt, StringComparison.Ordinal))
        {
            prompt = deduped;
            reduced = true;
            diagnostic.Append("removed duplicate noncritical lines; ");
        }

        estimate = EstimateTokens(prompt);
        if (estimate > inputBudget)
        {
            for (int i = 0; i < ReducibleMarkers.Length && EstimateTokens(prompt) > inputBudget; i++)
            {
                // note: Older/retrieved sections are reduced before any current player input or output contract is touched.
                prompt = ReduceMarkedSection(prompt, ReducibleMarkers[i], BudgetToCharacters(inputBudget / 8));
                reduced = true;
            }
        }

        estimate = EstimateTokens(prompt);
        if (estimate > inputBudget)
        {
            prompt = CompactMiddle(prompt, BudgetToCharacters(inputBudget));
            reduced = true;
            diagnostic.Append("compacted optional middle context; ");
        }

        estimate = EstimateTokens(prompt);
        if (estimate + reserve + safety > contextLimit)
        {
            compiled = default;
            error =
                "Prompt still exceeds context budget after reduction. estimatedInputTokens=" +
                estimate +
                ", reservedOutputTokens=" +
                reserve +
                ", contextLimitTokens=" +
                contextLimit +
                ".";
            return false;
        }

        compiled = new LLMCompiledPrompt(prompt, estimate, reserve, contextLimit, reduced, diagnostic.ToString());
        return true;
    }

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // note: Conservative English/code-ish estimate for GGUF backends when tokenizer HTTP support is unavailable.
        return Mathf.CeilToInt(text.Length / 3f);
    }

    private static string ApplyDirectModePrefix(
        string prompt,
        LLMRuntimeConfig config,
        LLMGenerationProfile profile,
        ref bool reduced)
    {
        if (config == null ||
            profile == null ||
            !profile.directMode ||
            profile.reasoningMode ||
            !config.emitQwenDirectModeToken ||
            string.IsNullOrWhiteSpace(config.qwenDirectModeToken))
        {
            return prompt;
        }

        string token = config.qwenDirectModeToken.Trim();
        if (prompt.StartsWith(token, StringComparison.Ordinal))
            return prompt;

        reduced = true;
        return token + "\n" + prompt;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string RemoveDuplicateNonCriticalLines(string prompt)
    {
        string[] lines = prompt.Split('\n');
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        StringBuilder sb = new StringBuilder(prompt.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string normalized = line.Trim();
            bool critical = IsCriticalLine(normalized);

            if (!critical && normalized.Length > 12 && !seen.Add(normalized))
                continue;

            sb.Append(line);
            if (i < lines.Length - 1)
                sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool IsCriticalLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        for (int i = 0; i < CriticalMarkers.Length; i++)
        {
            if (line.IndexOf(CriticalMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string ReduceMarkedSection(string prompt, string marker, int keepCharacters)
    {
        int markerIndex = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return prompt;

        int sectionStart = prompt.IndexOf('\n', markerIndex);
        if (sectionStart < 0)
            return prompt;

        sectionStart++;
        int sectionEnd = FindNextMarker(prompt, sectionStart);
        if (sectionEnd <= sectionStart)
            sectionEnd = prompt.Length;

        int sectionLength = sectionEnd - sectionStart;
        if (sectionLength <= keepCharacters)
            return prompt;

        string keptTail = prompt.Substring(sectionEnd - keepCharacters, keepCharacters).TrimStart();
        string replacement =
            "[older optional context compacted to fit local model budget]\n" +
            keptTail;

        return prompt.Substring(0, sectionStart) + replacement + prompt.Substring(sectionEnd);
    }

    private static int FindNextMarker(string prompt, int start)
    {
        int best = -1;
        for (int i = 0; i < ReducibleMarkers.Length; i++)
        {
            int found = prompt.IndexOf("\n" + ReducibleMarkers[i], start, StringComparison.OrdinalIgnoreCase);
            if (found >= 0 && (best < 0 || found < best))
                best = found + 1;
        }

        for (int i = 0; i < CriticalMarkers.Length; i++)
        {
            int found = prompt.IndexOf("\n" + CriticalMarkers[i], start, StringComparison.OrdinalIgnoreCase);
            if (found >= 0 && (best < 0 || found < best))
                best = found + 1;
        }

        return best;
    }

    private static string CompactMiddle(string prompt, int targetCharacters)
    {
        if (prompt.Length <= targetCharacters)
            return prompt;

        int headChars = Mathf.Clamp(Mathf.RoundToInt(targetCharacters * 0.42f), 256, targetCharacters);
        int tailChars = Mathf.Clamp(targetCharacters - headChars - 96, 256, targetCharacters);
        if (headChars + tailChars >= prompt.Length)
            return prompt;

        string head = prompt.Substring(0, headChars).TrimEnd();
        string tail = prompt.Substring(prompt.Length - tailChars, tailChars).TrimStart();
        return head +
               "\n[optional middle context compacted; canonical Unity state remains authoritative]\n" +
               tail;
    }

    private static int BudgetToCharacters(int tokens)
    {
        return Mathf.Max(512, tokens * 3);
    }
}
