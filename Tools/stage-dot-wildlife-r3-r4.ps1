param([string]$Archive = 'C:/Users/Garri/Downloads/YourQuest_Horse_Cow_Stag_Astra_Approved_Corrective_Patch_R3_R4_2026-10-03 (1).zip')
$ErrorActionPreference = 'Stop'
$root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets'
$output = 'outputs/DOT_Integration_20261003/Wildlife_R3_R4'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    # note: Inspect the exact delivered release; stage bytes outside Unity before any installed file is changed.
    $entry = $zip.GetEntry('Wildlife/Release/ungulate_motion_R3_stag_skin_R4_manifest.json')
    $reader = [IO.StreamReader]::new($entry.Open())
    $manifestText = $reader.ReadToEnd(); $reader.Dispose()
    $manifest = $manifestText | ConvertFrom-Json
    $layout = Get-Content "$root/Catalogs/DOT_ASSET_LAYOUT.json" -Raw | ConvertFrom-Json
    $changes = @(); $additions = @()
    foreach ($file in $manifest.files) {
        $entry = $zip.GetEntry($file.archive_path)
        if (!$entry) { throw "Missing archive entry: $($file.archive_path)" }
        if ($file.archive_path -match '(^/|(^|/)\.\.(/|$)|:)') { throw 'Unsafe archive path' }
        $staged = "$output/Staged/$($file.archive_path)"
        New-Item -ItemType Directory -Force -Path (Split-Path $staged) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, [IO.Path]::GetFullPath($staged), $true)
        $actual = (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash.ToLowerInvariant()
        $expected = if ($file.new_sha256) { $file.new_sha256 } else { $file.sha256 }
        if ($actual -ne $expected -or (Get-Item -LiteralPath $staged).Length -ne $file.bytes) { throw "Incoming integrity failed: $staged" }
        if ($file.role -eq 'changed_existing_file') {
            $matches = @($layout.files | Where-Object { $_.origin.EndsWith('/' + $file.installed_logical_path) })
            if ($matches.Count -ne 1) { throw "Ambiguous source: $($file.archive_path)" }
            $destination = $matches[0].path
            $oldHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($oldHash -ne $file.old_sha256) { throw "Installed baseline mismatch: $destination" }
            $changes += [ordered]@{ destination=$destination; staged=$staged; oldSha256=$oldHash; newSha256=$actual; guid=$matches[0].guid; archivePath=$file.archive_path; species=$file.species }
        } else {
            # note: The isolated LOD1 native override is authoring data, never a second automatic runtime import.
            $destination = if ($file.archive_path.StartsWith('SourceOnly/')) { 'SourceAssets/DOT/Wildlife/Stag/R4/' + $entry.Name } else { "$root/Documentation/Patches/Wildlife/UNGULATE-MOTION-R3-STAG-SKIN-R4-20261003/" + $file.archive_path }
            if (Test-Path -LiteralPath $destination) { throw "Addition already exists: $destination" }
            $additions += [ordered]@{ destination=$destination; staged=$staged; newSha256=$actual; archivePath=$file.archive_path }
        }
    }
    $manifestStage = "$output/Staged/$($manifest.manifest_archive_path)"
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($manifestStage), $manifestText, [Text.UTF8Encoding]::new($false))
    $additions += [ordered]@{ destination="$root/Documentation/Patches/Wildlife/UNGULATE-MOTION-R3-STAG-SKIN-R4-20261003/$($manifest.manifest_archive_path)"; staged=$manifestStage; newSha256=(Get-FileHash $manifestStage).Hash.ToLowerInvariant(); archivePath=$manifest.manifest_archive_path }
    if ($changes.Count -ne 15) { throw 'Expected fifteen existing source replacements' }
    [ordered]@{ status='PREFLIGHT_PASS'; utc=[DateTime]::UtcNow.ToString('O'); archive=$Archive; archiveSha256=(Get-FileHash -LiteralPath $Archive).Hash.ToLowerInvariant(); revision=$manifest.revision; changes=$changes; additions=$additions } | ConvertTo-Json -Depth 12 | Set-Content "$output/plan.json"
    "PREFLIGHT_PASS: $($changes.Count) exact R2 replacements; $($additions.Count) separately categorized additions."
} finally { $zip.Dispose() }
