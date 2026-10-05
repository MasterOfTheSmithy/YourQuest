param([string]$Archive = 'C:/Users/Garri/Downloads/YourQuest_Elf_Orc_Scalp_Alpha_Astra_Approved_Patch_R1_2026-10-03.zip')
$ErrorActionPreference = 'Stop'
$root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets'
$output = 'outputs/DOT_Integration_20261003/Scalp_Alpha_R1'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    $reader = [IO.StreamReader]::new($zip.GetEntry('PATCH_MANIFEST.json').Open())
    $manifest = $reader.ReadToEnd() | ConvertFrom-Json; $reader.Dispose()
    $layout = Get-Content "$root/Catalogs/DOT_ASSET_LAYOUT.json" -Raw | ConvertFrom-Json
    $changes = @(); $additions = @()
    # note: Match each exact original pack-relative alias against the organized source library before allowing an in-place replacement.
    foreach ($file in $manifest.files) {
        $matches = @($layout.files | Where-Object { $_.origin.EndsWith('/' + $file.path) })
        if ($matches.Count -ne 1) { throw "Missing or ambiguous installed source: $($file.path)" }
        $destination = $matches[0].path
        $oldHash = (Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant()
        if ($oldHash -ne $file.old_sha256) { throw "Installed baseline mismatch: $destination" }
        $changes += [ordered]@{ destination=$destination; staged="$output/Staged/$($file.path)"; oldSha256=$oldHash; newSha256=$file.new_sha256; guid=$matches[0].guid; archivePath=$file.path; kind=$file.kind }
    }
    foreach ($entry in $zip.Entries) {
        if (!$entry.Name) { continue }
        if ($entry.FullName -match '(^/|(^|/)\.\.(/|$)|:)') { throw 'Unsafe archive path' }
        $staged = "$output/Staged/$($entry.FullName)"
        New-Item -ItemType Directory -Force -Path (Split-Path $staged) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, [IO.Path]::GetFullPath($staged), $true)
        $actual = (Get-FileHash -LiteralPath $staged).Hash.ToLowerInvariant()
        $file = $manifest.files | Where-Object path -eq $entry.FullName
        if ($file) {
            if ($actual -ne $file.new_sha256 -or $entry.Length -ne $file.new_bytes) { throw "Incoming integrity failed: $staged" }
        } else {
            # note: Proofs and provenance are versioned documentation, not a second runtime race kit.
            $destination = "$root/Documentation/Patches/NPCs/$($manifest.revision)/$($entry.FullName)"
            if (Test-Path -LiteralPath $destination) { throw "Addition already exists: $destination" }
            $additions += [ordered]@{ destination=$destination; staged=$staged; newSha256=$actual; archivePath=$entry.FullName }
        }
    }
    if ($changes.Count -ne 41) { throw 'Expected forty-one matching replacements' }
    $payloadRows = [string[]]@($manifest.files | ForEach-Object { $_.path + [char]0 + $_.new_sha256 + "`n" })
    [Array]::Sort($payloadRows, [StringComparer]::Ordinal)
    $payload = $payloadRows -join ''
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $payloadHash = [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))).Replace('-', '').ToLowerInvariant() } finally { $algorithm.Dispose() }
    if ($payloadHash -ne $manifest.payload_sha256) { throw 'Approved payload set mismatch' }
    [ordered]@{ status='PREFLIGHT_PASS'; utc=[DateTime]::UtcNow.ToString('O'); archive=$Archive; archiveSha256=(Get-FileHash -LiteralPath $Archive).Hash.ToLowerInvariant(); revision=$manifest.revision; approvedPayloadSha256=$payloadHash; changes=$changes; additions=$additions } | ConvertTo-Json -Depth 12 | Set-Content "$output/plan.json"
    "PREFLIGHT_PASS: $($changes.Count) exact replacements; $($additions.Count) categorized reference files."
} finally { $zip.Dispose() }
