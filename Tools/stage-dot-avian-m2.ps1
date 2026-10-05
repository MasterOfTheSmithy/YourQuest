$ErrorActionPreference = 'Stop'
$root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets'
$output = 'outputs/DOT_Integration_20261003/Avian_M2'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$files = @()
# note: Stage the required frozen base and additive release together, without changing any existing source or importing rejected historical models.
foreach ($archive in @('C:/Users/Garri/Downloads/YourQuest_Avian_Ordinary_NPC_Base_Astra_Approved_V9b_2026-10-03.zip','C:/Users/Garri/Downloads/YourQuest_Avian_Modular_Elemental_M2_Astra_Approved_ADDON_REQUIRES_V9b_2026-10-03.zip')) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            if (!$entry.Name) { continue }
            if (!$entry.FullName.StartsWith('YourQuest_Avian_V9b/') -or $entry.FullName -match '(^/|(^|/)\.\.(/|$)|:)') { throw 'Unsafe Avian entry' }
            $relative = $entry.FullName.Substring('YourQuest_Avian_V9b/'.Length)
            $staged = "$output/Staged/$relative"
            if (Test-Path -LiteralPath $staged) { throw "Duplicate staged Avian file: $relative" }
            New-Item -ItemType Directory -Force -Path (Split-Path $staged) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, [IO.Path]::GetFullPath($staged))
            $extension = [IO.Path]::GetExtension($relative).ToLowerInvariant()
            $version = if ($relative.StartsWith('ModularExpansion/M2/')) {'M2'} elseif ($relative.StartsWith('ModularExpansion/')) {'M1'} else {'V9b'}
            $owner = "$root/NPCs/Avian"
            if ($extension -eq '.glb') {
                $owner += if ($entry.Name -like '*PartBank*') {'/Modular Parts/Authoring'} elseif ($entry.Name -like '*Clasp_Parts*') {'/Modular Parts/Waist Hardware'} else {'/Body Types/Ordinary'}
                $kind = if ($entry.Name -match '_LOD[12]\.glb$') {'Models/LODs'} else {'Models/Assemblies'}
                $destination = "$owner/$kind/$($entry.Name)"
            } elseif ($extension -eq '.blend') {
                $owner += if ($entry.Name -like '*PartBank*') {'/Modular Parts/Authoring'} elseif ($entry.Name -like '*Clasp_Parts*') {'/Modular Parts/Waist Hardware'} else {'/Body Types/Ordinary'}
                $kind = 'Models/Source'; $destination = "$owner/$kind/$($entry.Name)"
            } elseif ($extension -in @('.mp4','.png')) {
                $kind = 'Previews'; $destination = "$owner/Previews/$version/$($entry.Name)"
            } else {
                $kind = if ($relative -match 'Licenses|PROVENANCE') {'Provenance'} elseif ($relative -match 'Contracts|Contract') {'Contracts'} elseif ($relative -match 'Validation|AUDIT|Cost') {'Validation'} elseif ($extension -eq '.py') {'Supplied Tools'} elseif ($relative -match 'MANIFEST') {'Manifests'} else {'Guides'}
                $destination = "$owner/Documentation/$kind/$version/$($entry.Name)"
            }
            if (Test-Path -LiteralPath $destination) { throw "Avian destination already exists: $destination" }
            $files += [ordered]@{ origin="$root/NPCs/Avian/YourQuest_Avian_V9b/$relative"; destination=$destination; relative=$relative; staged=$staged; sha256=(Get-FileHash -LiteralPath $staged).Hash.ToLowerInvariant(); bytes=$entry.Length; kind=$kind; materialFolder="$owner/Materials" }
        }
    } finally { $zip.Dispose() }
}
[ordered]@{ status='STAGED_PENDING_CONTRACT_CHECKS'; utc=[DateTime]::UtcNow.ToString('O'); files=$files } | ConvertTo-Json -Depth 10 | Set-Content "$output/plan.json"
"Staged $($files.Count) categorized Avian base/add-on files outside Unity Assets."
