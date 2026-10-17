using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// note: History belongs to the existing player document. Hashes grow with delivered speech; only prompt context is bounded.
public static class YQGoddessSpeechHistory
{
    public static string Normalize(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;
        var result = new StringBuilder(line.Length);
        bool spacing = false;
        foreach (char character in line.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(character)) { result.Append(char.ToLowerInvariant(character)); spacing = false; }
            else if (!spacing && result.Length > 0) { result.Append(' '); spacing = true; }
        }
        return result.ToString().Trim();
    }

    public static IEnumerable<string> Fingerprints(string line)
    {
        string normalized = Normalize(line);
        if (normalized.Length == 0) yield break;
        yield return YQRepairEpisode.Hash("dialogue-v1|" + normalized);
        // note: Sentence hashes catch a familiar paragraph with a new tail; very short shared acknowledgements do not exhaust normal vocabulary.
        foreach (string sentence in Regex.Split(line, @"[.!?]+"))
        {
            string text = Normalize(sentence);
            if (text.Split(' ').Length >= 5) yield return YQRepairEpisode.Hash("sentence-v1|" + text);
        }
    }

    public static bool IsNovel(PlayerState state, string line, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(line) || Normalize(line).Length == 0) { reason = "Speech contains no words."; return false; }
        if (state == null) { reason = "No selected player owns speech history."; return false; }
        var legacyKeys = new HashSet<string>(StringComparer.Ordinal);
        // note: Legacy saves retain a few spoken lines. Reject their substantial sentences even before the first history commit seeds them.
        if (state.goddessVoiceMemory != null)
            foreach (string previous in state.goddessVoiceMemory)
                foreach (string key in Fingerprints(previous)) legacyKeys.Add(key);
        foreach (string key in Fingerprints(line))
            if (state.goddessDeliveredSpeechHashes != null && state.goddessDeliveredSpeechHashes.Contains(key))
            { reason = "A previously delivered line or substantial sentence was repeated."; return false; }
            else if (legacyKeys.Contains(key))
            { reason = "A legacy spoken line or substantial sentence was repeated."; return false; }
        if (state.goddessVoiceMemory != null)
            foreach (string previous in state.goddessVoiceMemory)
                if (IsNearDuplicate(previous, line)) { reason = "Recent speech was repeated with only small wording changes."; return false; }
        return true;
    }

    public static bool IsNearDuplicate(string first, string second)
    {
        string left = Normalize(first), right = Normalize(second);
        if (left.Length == 0 || right.Length == 0) return false;
        if (left == right) return true;
        var a = new HashSet<string>(left.Split(' '), StringComparer.Ordinal);
        var b = new HashSet<string>(right.Split(' '), StringComparer.Ordinal);
        int common = 0; foreach (string word in a) if (b.Contains(word)) common++;
        return a.Count >= 5 && b.Count >= 5 && common >= 0.8 * (a.Count + b.Count - common);
    }

    public static bool TryRecord(PlayerState state, string line, Func<bool> persist, out string reason)
    {
        if (!IsNovel(state, line, out reason)) return false;
        state.EnsureCollections();
        var added = new List<string>();
        var previousMemory = new List<string>(state.goddessVoiceMemory);
        // note: Seed all available legacy memory once. Previously discarded historical lines cannot be reconstructed.
        foreach (string previous in previousMemory)
            foreach (string key in Fingerprints(previous)) if (state.goddessDeliveredSpeechHashes.Add(key)) added.Add(key);
        foreach (string key in Fingerprints(line)) if (state.goddessDeliveredSpeechHashes.Add(key)) added.Add(key);
        state.goddessVoiceMemory.Add(line);
        while (state.goddessVoiceMemory.Count > 8) state.goddessVoiceMemory.RemoveAt(0);
        try { if (persist != null && persist()) return true; }
        catch (Exception exception) { reason = exception.Message; }
        // note: A failed paired commit cannot authorize visible speech or poison a future retry's in-memory history.
        foreach (string key in added) state.goddessDeliveredSpeechHashes.Remove(key);
        state.goddessVoiceMemory.Clear(); state.goddessVoiceMemory.AddRange(previousMemory);
        if (reason.Length == 0) reason = "Speech history could not be persisted.";
        return false;
    }
}
