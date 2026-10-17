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
    internal static bool JsonEqualsCaptured(object value, string capturedJson, Newtonsoft.Json.JsonSerializerSettings settings)
    {
        // note: Preserve complete serializer traversal and exact ordinal output equality without allocating another complete document string.
        var serializer = Newtonsoft.Json.JsonSerializer.CreateDefault(settings);
        using (var sink = new CapturedJsonComparisonWriter(capturedJson))
        {
            using (var writer = new Newtonsoft.Json.JsonTextWriter(sink) { Formatting = serializer.Formatting, CloseOutput = false })
            {
                serializer.Serialize(writer, value);
                writer.Flush();
            }
            return sink.MatchesSnapshot;
        }
    }

    private sealed class CapturedJsonComparisonWriter : System.IO.TextWriter
    {
        private readonly string captured;
        private long position;
        private bool matches;

        internal CapturedJsonComparisonWriter(string captured)
        {
            this.captured = captured;
            matches = captured != null;
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;
        public override IFormatProvider FormatProvider => System.Globalization.CultureInfo.InvariantCulture;
        internal bool MatchesSnapshot => matches && position == captured.Length;

        public override void Write(char value)
        {
            if (matches && (position >= captured.Length || captured[(int)position] != value)) matches = false;
            position++;
        }

        public override void Write(string value)
        {
            if (value == null) return;
            if (matches && (position > captured.Length || value.Length > captured.Length - position ||
                string.CompareOrdinal(value, 0, captured, (int)position, value.Length) != 0)) matches = false;
            position += value.Length;
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - index < count) throw new ArgumentException("The buffer range is invalid.");
            // note: Compare the emitted range directly; even after mismatch the serializer still visits every later field/converter and preserves its errors.
            if (matches && (position > captured.Length || count > captured.Length - position ||
                !System.MemoryExtensions.SequenceEqual<char>(new ReadOnlySpan<char>(buffer, index, count),
                    System.MemoryExtensions.AsSpan(captured, (int)position, count)))) matches = false;
            position += count;
        }
    }

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
        // note: Ordinary paired-save callers retain the original projection order and fault points.
        return TryCommitCore(profileFolder, profileId, activePlayerPath, activeWorldPath, revision,
            playerJson, worldJson, auxiliaryDocuments, faultInjector, true, out receipt);
    }

    internal static bool TryStageContinuationCommit(string profileFolder, string profileId, int revision,
        string playerJson, string worldJson, IReadOnlyDictionary<string, string> auxiliaryDocuments,
        Action<YQProfileCommitPoint> faultInjector, out YQProfileTransactionReceipt receipt)
    {
        // note: A continuation remains detached until its owner commits the manifest pointer; no working projection or backup is touched here.
        return TryStagePairedCommit(profileFolder, profileId, revision, playerJson, worldJson,
            auxiliaryDocuments, faultInjector, out receipt);
    }

    internal static bool TryStagePairedCommit(string profileFolder, string profileId, int revision,
        string playerJson, string worldJson, IReadOnlyDictionary<string, string> auxiliaryDocuments,
        Action<YQProfileCommitPoint> faultInjector, out YQProfileTransactionReceipt receipt)
    {
        // note: Offer decisions also stage an immutable pair before the existing profile owner publishes its pointer.
        return TryCommitCore(profileFolder, profileId, null, null, revision,
            playerJson, worldJson, auxiliaryDocuments, faultInjector, false, out receipt);
    }

    private static bool TryCommitCore(string profileFolder, string profileId, string activePlayerPath,
        string activeWorldPath, int revision, string playerJson, string worldJson,
        IReadOnlyDictionary<string, string> auxiliaryDocuments, Action<YQProfileCommitPoint> faultInjector,
        bool publishProjections, out YQProfileTransactionReceipt receipt)
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
            if (string.IsNullOrWhiteSpace(profileId) || (publishProjections &&
                (string.IsNullOrWhiteSpace(activePlayerPath) || string.IsNullOrWhiteSpace(activeWorldPath))))
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
            receipt.playerChecksum = HashCanonicalTextFile(stagedPlayer);
            receipt.worldChecksum = HashCanonicalTextFile(stagedWorld);

            // note: Publishing the complete revision directory makes the paired snapshot recoverable even if projection work stops.
            Directory.Move(staging, revisionFolder);
            faultInjector?.Invoke(YQProfileCommitPoint.RevisionPublished);

            if (publishProjections)
            {
                // note: The compatibility wrapper retains its existing ordinary-save semantics; continuation staging skips this entire projection boundary.
                CopyAtomically(Path.Combine(revisionFolder, "player_state.json"), activePlayerPath, activePlayerPath + ".bak");
                faultInjector?.Invoke(YQProfileCommitPoint.PlayerProjectionPublished);
                CopyAtomically(Path.Combine(revisionFolder, "world_state.json"), activeWorldPath, activeWorldPath + ".bak");
                faultInjector?.Invoke(YQProfileCommitPoint.WorldProjectionPublished);
                faultInjector?.Invoke(YQProfileCommitPoint.BeforePointerPublication);
                receipt.published = true;
            }
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

    internal static bool TryRefreshPublishedContinuationProjections(string profileFolder,
        string activePlayerPath, string activeWorldPath, YQProfileTransactionReceipt receipt, out string failure)
    {
        return TryRefreshPublishedProjections(profileFolder, activePlayerPath, activeWorldPath, receipt, out failure);
    }

    internal static bool TryRefreshPublishedProjections(string profileFolder,
        string activePlayerPath, string activeWorldPath, YQProfileTransactionReceipt receipt, out string failure)
    {
        failure = string.Empty;
        try
        {
            // note: The profile owner sets published only after the durable manifest pointer; validate the complete immutable pair before either working copy changes.
            if (receipt == null || !receipt.published || string.IsNullOrWhiteSpace(receipt.commitId) ||
                string.IsNullOrWhiteSpace(receipt.playerChecksum) || string.IsNullOrWhiteSpace(receipt.worldChecksum) ||
                string.IsNullOrWhiteSpace(activePlayerPath) || string.IsNullOrWhiteSpace(activeWorldPath))
                throw new InvalidOperationException("Projections require a published paired receipt and explicit paths.");
            if (!TryReadCommit(profileFolder, receipt.commitId, receipt.playerChecksum, receipt.worldChecksum,
                    out string playerPath, out string worldPath, out failure)) return false;
            CopyAtomically(playerPath, activePlayerPath, activePlayerPath + ".bak");
            CopyAtomically(worldPath, activeWorldPath, activeWorldPath + ".bak");
            return true;
        }
        catch (Exception exception)
        {
            // note: A working-copy failure cannot revoke the already accepted manifest revision or relabel its receipt as rejected.
            failure = exception.Message;
            return false;
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
            if (!string.IsNullOrWhiteSpace(expectedPlayerChecksum) && HashCanonicalTextFile(candidatePlayer) != expectedPlayerChecksum) continue;
            if (!string.IsNullOrWhiteSpace(expectedWorldChecksum) && HashCanonicalTextFile(candidateWorld) != expectedWorldChecksum) continue;
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
            if (!string.IsNullOrWhiteSpace(expectedChecksum) && HashCanonicalTextFile(candidate) != expectedChecksum) continue;
            documentPath = candidate;
            return true;
        }
        failure = "Auxiliary document is missing or checksum validation failed.";
        return false;
    }

    internal static string HashCanonicalTextFile(string path)
    {
        // note: Preserve Sha256Hex(ReadAllText(path)) including BOM detection and split Unicode pairs, without allocating a complete UTF-16 document plus UTF-8 copy on every validation.
        using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, true))
        using (var hash = CreateCanonicalTextHash())
        using (var sink = new System.Security.Cryptography.CryptoStream(Stream.Null, hash, System.Security.Cryptography.CryptoStreamMode.Write))
        {
            using (var writer = new StreamWriter(sink, new System.Text.UTF8Encoding(false), 4096, true))
            {
                var characters = new char[4096];
                int count;
                while ((count = reader.Read(characters, 0, characters.Length)) > 0)
                    writer.Write(characters, 0, count);
            }
            sink.FlushFinalBlock();
            return BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static System.Security.Cryptography.SHA256 CreateCanonicalTextHash()
    {
        // note: Unity Mono's managed SHA256 is expensive for repeated full-document proofs. Use invocation-owned Windows CNG with the same digest; every platform/unavailable provider retains the established algorithm.
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            try { return new WindowsCanonicalSha256(); }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException ||
                error is BadImageFormatException || error is NotSupportedException ||
                error is System.Runtime.InteropServices.MarshalDirectiveException || error is System.Security.Cryptography.CryptographicException) { }
        }
        return System.Security.Cryptography.SHA256.Create();
    }

    // note: SafeHandles own the algorithm/hash and finalize abandoned instances; no provider, document or acceptance result is cached across invocations.
    private sealed class WindowsCanonicalSha256 : System.Security.Cryptography.SHA256
    {
        private readonly AlgorithmHandle algorithm;
        private HashHandle hash;
        internal WindowsCanonicalSha256()
        {
            HashSizeValue = 256;
            try
            {
                Check(BCryptOpenAlgorithmProvider(out algorithm, "SHA256", null, 0));
                if (algorithm == null || algorithm.IsInvalid) throw new System.Security.Cryptography.CryptographicException("Windows SHA256 provider returned no handle.");
                Initialize();
            }
            catch { hash?.Dispose(); algorithm?.Dispose(); throw; }
        }
        public override void Initialize()
        {
            hash?.Dispose(); hash = null;
            if (algorithm == null || algorithm.IsClosed) throw new ObjectDisposedException(nameof(WindowsCanonicalSha256));
            HashHandle created;
            int status = BCryptCreateHash(algorithm, out created, IntPtr.Zero, 0, IntPtr.Zero, 0, 0);
            if (status != 0 || created == null || created.IsInvalid)
            {
                created?.Dispose(); Check(status);
                throw new System.Security.Cryptography.CryptographicException("Windows SHA256 returned no hash handle.");
            }
            hash = created;
        }
        protected override void HashCore(byte[] array, int start, int count)
        {
            if (count == 0) return;
            // note: CNG consumes the current exact encoder bytes synchronously; pin only for this call and always release before returning or throwing.
            var pinned = System.Runtime.InteropServices.GCHandle.Alloc(array, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { Check(BCryptHashData(hash, IntPtr.Add(pinned.AddrOfPinnedObject(), start), checked((uint)count), 0)); }
            finally { pinned.Free(); }
        }
        protected override byte[] HashFinal()
        {
            var digest = new byte[32]; Check(BCryptFinishHash(hash, digest, 32, 0)); return digest;
        }
        protected override void Dispose(bool disposing)
        {
            // note: Destroy the hash before its algorithm provider; disposal is idempotent and SafeHandle also covers constructor/exception/finalizer paths.
            try { hash?.Dispose(); hash = null; algorithm?.Dispose(); }
            finally { base.Dispose(disposing); }
        }
        private static void Check(int status)
        {
            if (status != 0) throw new System.Security.Cryptography.CryptographicException("Windows SHA256 failed with status 0x" + status.ToString("X8") + ".");
        }
        private sealed class AlgorithmHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
        {
            public AlgorithmHandle() : base(true) { }
            protected override bool ReleaseHandle() => BCryptCloseAlgorithmProvider(handle, 0) == 0;
        }
        private sealed class HashHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
        {
            public HashHandle() : base(true) { }
            protected override bool ReleaseHandle() => BCryptDestroyHash(handle) == 0;
        }
        [System.Runtime.InteropServices.DllImport("bcrypt.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int BCryptOpenAlgorithmProvider(out AlgorithmHandle algorithm, string id, string implementation, uint flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        private static extern int BCryptCreateHash(AlgorithmHandle algorithm, out HashHandle hash, IntPtr buffer, uint bufferLength, IntPtr secret, uint secretLength, uint flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        private static extern int BCryptHashData(HashHandle hash, IntPtr bytes, uint count, uint flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        private static extern int BCryptFinishHash(HashHandle hash, [System.Runtime.InteropServices.Out] byte[] digest, uint length, uint flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        private static extern int BCryptDestroyHash(IntPtr hash);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm, uint flags);
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
