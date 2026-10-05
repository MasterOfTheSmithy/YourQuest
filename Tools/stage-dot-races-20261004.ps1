$ErrorActionPreference = 'Stop'
$output = 'outputs/DOT_Integration_20261004/Races'
$root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets'
$files = @()
# note: Categorize only the delivered, separately versioned releases; frozen documents and original source bytes remain intact.
foreach ($race in @('Dwarf','Kitsune','Bramblekin')) {
    $stage = [IO.Path]::GetFullPath("$output/Staged/$race")
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($stage, $file.FullName).Replace('\','/')
        if ($race -eq 'Kitsune') { $relative = $relative.Substring('YourQuest_Kitsune_v20/'.Length) }
        $version = @{Dwarf='RevisionB_R1';Kitsune='v20';Bramblekin='I16'}[$race]
        $form = @{Dwarf='Stonewright';Kitsune='Adult Female';Bramblekin='Ordinary'}[$race]
        $owner = "$root/NPCs/$race"
        $extension = $file.Extension.ToLowerInvariant()
        if ($extension -eq '.glb' -or $extension -eq '.blend') {
            $owner += "/Body Types/$form"
            $kind = if ($extension -eq '.blend') {'Models/Source'} elseif ($file.Name -match 'LOD0') {'Models/Assemblies'} else {'Models/LODs'}
            $destination = "$owner/$kind/$version/$($file.Name)"
        } elseif ($relative.StartsWith('Textures/')) {
            $kind = 'Textures'; $destination = "$owner/Textures/$version/" + $relative.Substring(9)
        } elseif ($relative.StartsWith('Previews/')) {
            $kind = 'Previews'; $destination = "$owner/Previews/$version/" + $relative.Substring(9)
        } else {
            $kind = if ($relative -match '(?i)Licens|Provenance') {'Provenance'} elseif ($relative -match '(?i)Contract|Recipe|Handoff') {'Contracts'} elseif ($relative -match '^(QA|Validation|Approval)/') {'Validation'} elseif ($extension -eq '.py') {'Supplied Tools'} elseif ($relative -match '(?i)Manifest|Identity') {'Manifests'} else {'Guides'}
            $destination = "$owner/Documentation/$kind/$version/$relative"
        }
        if (Test-Path -LiteralPath $destination) { throw "New destination already exists: $destination" }
        $files += [ordered]@{ race=$race; relative=$relative; origin="$root/NPCs/$race/$version/$relative"; destination=$destination; staged=$file.FullName.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant(); bytes=$file.Length; kind=$kind; materialFolder="$owner/Body Types/$form/Materials/$version" }
    }
}
[ordered]@{status='STAGED';files=$files} | ConvertTo-Json -Depth 8 | Set-Content "$output/plan.json"
"Staged $($files.Count) files."
