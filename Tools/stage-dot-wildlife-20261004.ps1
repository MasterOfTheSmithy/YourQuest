param([string]$ProjectRoot = 'C:/Users/Garri/YourQuest')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
# note: Stage exact supplied members outside Assets; reject traversal and duplicates before any Editor operation.
$stagingRoot = Join-Path $ProjectRoot 'outputs/DOT_Integration_20261004/Wildlife'
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
$packages = @(
    @{ species='goat'; version='G18_RIG08'; zip='YourQuest_Goat_G18_RIG08_Approved.zip' },
    @{ species='dog'; version='B14'; zip='YourQuest_Dog_B14_Approved_Final_2026-10-04.zip' },
    @{ species='bear'; version='B10'; zip='YourQuest_Bear_B10_Placeholder_2026-10-04.zip' },
    @{ species='sheep'; version='S12'; zip='YourQuest_Sheep_S12_Approved_2026-10-04.zip' },
    @{ species='boar'; version='R34'; zip='YourQuest_Boar_R34_2026-10-04.zip' }
)
$inventory = @()
foreach($package in $packages) {
    $archivePath = Join-Path 'C:/Users/Garri/Downloads' $package.zip
    $destinationRoot = [IO.Path]::GetFullPath((Join-Path $stagingRoot ('Staged/' + $package.species)))
    if(Test-Path -LiteralPath $destinationRoot) { throw "Existing staging requires review: $destinationRoot" }
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $files=@();$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($member in $archive.Entries | Where-Object Length -gt 0) {
            $relative=$member.FullName.Substring($member.FullName.IndexOf('/')+1)
            $target=[IO.Path]::GetFullPath((Join-Path $destinationRoot $relative))
            if(!$target.StartsWith($destinationRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or !$names.Add($relative)) { throw "Unsafe member: $relative" }
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($member,$target)
            $files += @{relative=$relative; staged=$target.Replace('\','/'); bytes=$member.Length; sha256=(Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant()}
        }
        $inventory += @{species=$package.species;version=$package.version;archive=$archivePath;archiveSha256=(Get-FileHash -LiteralPath $archivePath).Hash.ToLowerInvariant();files=$files}
    } finally { $archive.Dispose() }
}
$inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stagingRoot 'archive-inventory.json') -Encoding utf8
Write-Output "Staged five exact wildlife archives outside Unity Assets."
