using System;
using System.Collections.Generic;
using System.IO;

public enum YQProfileCommitPoint
{
    PlayerStaged,
    WorldStaged,
    AuxiliaryStaged,
    RevisionPublished,
    PlayerProjectionPublished,
    WorldProjectionPublished,
    BeforePointerPublication
}

[Serializable]
public sealed class YQProfileCommitRecord
{
    public string profileId;
    public string commitId;
    public int revision;
    public string previousCommitId;
    public string playerChecksum;
    public string worldChecksum;
    public string status;
    public long committedUnix;
    public List<YQProfileAuxiliaryDocumentRecord> auxiliaryDocuments = new List<YQProfileAuxiliaryDocumentRecord>();
}

[Serializable]
public sealed class YQProfileAuxiliaryDocumentRecord
{
    public string documentId;
    public string checksum;
    public string relativePath;
}

public static class YQProfileCommitStore
{
    public static bool TryCommit(
        string profileFolder,
        string profileId,
        string activePlayerPath,
        string activeWorldPath,
        int revision,
        string playerJson,
        string worldJson,
        Action<YQProfileCommitPoint> faultInjector,
        out YQProfileTransactionReceipt receipt)
    {
        return TryCommit(profileFolder, profileId, activePlayerPath, activeWorldPath, revision, playerJson, worldJson, null, faultInjector, out receipt);
    }

    public static bool TryCommit(
        string profileFolder,
        string profileId,
        string activePlayerPath,
        string activeWorldPath,
        int revision,
        string playerJson,
        string worldJson,
        IReadOnlyDictionary<string, string> auxiliaryDocuments,
        Action<YQProfileCommitPoint> faultInjector,
        out YQProfileTransactionReceipt receipt)
    {
        string commitId = Guid.NewGuid().ToString("N");
        receipt = new YQProfileTransactionReceipt
        {
            profileId = profileId ?? string.Empty,
            commitId = commitId,
            revision = revision,
            previousCommitId = string.Empty,
            committedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        string staging = Path.Combine(profileFolder, "revisions", ".staging-" + commitId);
        string revisionFolder = Path.Combine(profileFolder, "revisions", "r" + revision.ToString("D8") + "-" + commitId);
        try
        {
            // note: Stage both canonical documents under one disposable directory before any pointer or projection changes.
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(activePlayerPath) || string.IsNullOrWhiteSpace(activeWorldPath))
                throw new InvalidOperationException("Profile transaction is missing an identity or projection path.");
            if (playerJson == null || worldJson == null)
                throw new InvalidOperationException("Profile transaction received a null canonical document.");
            Directory.CreateDirectory(Path.Combine(profileFolder, "revisions"));
            if (Directory.Exists(revisionFolder))
                throw new IOException("Profile revision already exists: " + revision);
            Directory.CreateDirectory(staging);
            string stagedPlayer = Path.Combine(staging, "player_state.json");
            string stagedWorld = Path.Combine(staging, "world_state.json");
            WriteText(stagedPlayer, playerJson);
            faultInjector?.Invoke(YQProfileCommitPoint.PlayerStaged);
            WriteText(stagedWorld, worldJson);
            faultInjector?.Invoke(YQProfileCommitPoint.WorldStaged);
            if (auxiliaryDocuments != null && auxiliaryDocuments.Count > 0)
            {
                HashSet<string> auxiliaryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> auxiliary in auxiliaryDocuments)
                {
                    if (string.IsNullOrWhiteSpace(auxiliary.Key) || auxiliary.Value == null)
                        throw new InvalidOperationException("Profile auxiliary document is missing an id or payload.");
                    string documentId = auxiliary.Key.Trim();
                    string relativePath = Path.Combine("auxiliary", SafeFileName(documentId) + ".json");
                    if (!auxiliaryPaths.Add(relativePath))
                        throw new InvalidOperationException("Profile auxiliary document IDs collide after filename normalization: " + documentId);
                    string stagedAuxiliary = Path.Combine(staging, relativePath);
                    WriteText(stagedAuxiliary, auxiliary.Value);
                    receipt.auxiliaryDocuments.Add(new YQProfileAuxiliaryDocumentRecord
                    {
                        documentId = documentId,
                        checksum = YQStateContract.Sha256Hex(auxiliary.Value),
                        relativePath = relativePath.Replace('\\', '/')
                    });
                }
            }
            faultInjector?.Invoke(YQProfileCommitPoint.AuxiliaryStaged);
            receipt.playerChecksum = YQStateContract.Sha256Hex(File.ReadAllText(stagedPlayer));
            receipt.worldChecksum = YQStateContract.Sha256Hex(File.ReadAllText(stagedWorld));

            // note: Publishing the complete revision directory makes the paired snapshot recoverable even if projection work stops.
            Directory.Move(staging, revisionFolder);
            faultInjector?.Invoke(YQProfileCommitPoint.RevisionPublished);

            // note: Shared files are only a working projection; each is atomically replaced from the already-complete revision.
            CopyAtomically(Path.Combine(revisionFolder, "player_state.json"), activePlayerPath, activePlayerPath + ".bak");
            faultInjector?.Invoke(YQProfileCommitPoint.PlayerProjectionPublished);
            CopyAtomically(Path.Combine(revisionFolder, "world_state.json"), activeWorldPath, activeWorldPath + ".bak");
            faultInjector?.Invoke(YQProfileCommitPoint.WorldProjectionPublished);
            faultInjector?.Invoke(YQProfileCommitPoint.BeforePointerPublication);
            receipt.published = true;
            return true;
        }
        catch (Exception exception)
        {
            receipt.failure = exception.Message;
            return false;
        }
        finally
        {
            // note: Abandoned staging data is disposable; published revisions and their prior complete siblings are retained.
            if (Directory.Exists(staging))
                try { Directory.Delete(staging, true); } catch { }
        }
    }

    public static bool TryReadCommit(string profileFolder, string commitId, string expectedPlayerChecksum, string expectedWorldChecksum, out string playerPath, out string worldPath, out string failure)
    {
        playerPath = worldPath = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(profileFolder) || string.IsNullOrWhiteSpace(commitId))
        {
            failure = "Missing profile commit identity.";
            return false;
        }
        string revisionRoot = Path.Combine(profileFolder, "revisions");
        if (!Directory.Exists(revisionRoot)) { failure = "Profile revision store is missing."; return false; }
        string[] candidates = Directory.GetDirectories(revisionRoot, "r*");
        for (int index = 0; index < candidates.Length; index++)
        {
            string candidateId = Path.GetFileName(candidates[index]);
            if (candidateId.IndexOf(commitId, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            string candidatePlayer = Path.Combine(candidates[index], "player_state.json");
            string candidateWorld = Path.Combine(candidates[index], "world_state.json");
            if (!File.Exists(candidatePlayer) || !File.Exists(candidateWorld)) continue;
            if (!string.IsNullOrWhiteSpace(expectedPlayerChecksum) && YQStateContract.Sha256Hex(File.ReadAllText(candidatePlayer)) != expectedPlayerChecksum) continue;
            if (!string.IsNullOrWhiteSpace(expectedWorldChecksum) && YQStateContract.Sha256Hex(File.ReadAllText(candidateWorld)) != expectedWorldChecksum) continue;
            playerPath = candidatePlayer;
            worldPath = candidateWorld;
            return true;
        }
        failure = "Profile commit is missing or checksum validation failed.";
        return false;
    }

    public static bool TryReadAuxiliaryDocument(string profileFolder, string commitId, string documentId, string expectedChecksum, out string documentPath, out string failure)
    {
        documentPath = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(documentId)) { failure = "Auxiliary document identity is missing."; return false; }
        string revisionRoot = Path.Combine(profileFolder ?? string.Empty, "revisions");
        if (!Directory.Exists(revisionRoot)) { failure = "Profile revision store is missing."; return false; }
        string[] candidates = Directory.GetDirectories(revisionRoot, "r*");
        for (int index = 0; index < candidates.Length; index++)
        {
            if (Path.GetFileName(candidates[index]).IndexOf(commitId ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0) continue;
            string candidate = Path.Combine(candidates[index], "auxiliary", SafeFileName(documentId) + ".json");
            if (!File.Exists(candidate)) continue;
            if (!string.IsNullOrWhiteSpace(expectedChecksum) && YQStateContract.Sha256Hex(File.ReadAllText(candidate)) != expectedChecksum) continue;
            documentPath = candidate;
            return true;
        }
        failure = "Auxiliary document is missing or checksum validation failed.";
        return false;
    }

    private static void WriteText(string path, string content)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    private static string SafeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = value.Trim();
        for (int index = 0; index < invalid.Length; index++) clean = clean.Replace(invalid[index], '_');
        return clean.Length > 80 ? clean.Substring(0, 80) : clean;
    }

    private static void CopyAtomically(string source, string destination, string backup)
    {
        string directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temporary = destination + ".tmp";
        File.Copy(source, temporary, true);
        try
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, backup, true);
            else File.Move(temporary, destination);
        }
        catch (PlatformNotSupportedException)
        {
            // note: A projection cannot be made safe by falling back to a non-atomic overwrite.
            throw new IOException("This filesystem does not support atomic profile projection replacement.");
        }
    }
}
