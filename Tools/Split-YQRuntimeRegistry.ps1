param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

# note: This migrates only YourQuest's generated registry assets; imported source assets are never edited.
$registryPath = Join-Path $ProjectRoot 'Assets/Assets/Resources/YQRuntimeWorldAssetRegistry.asset'
$shardFolder = Join-Path $ProjectRoot 'Assets/Assets/Resources/YQWorldAssetShards'
$backupFolder = Join-Path $ProjectRoot 'Logs/RegistryMigration'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
    throw "Runtime registry not found: $registryPath"
}

$lines = [System.IO.File]::ReadAllLines($registryPath)
$entriesIndex = [Array]::FindIndex($lines, [Predicate[string]] { param($line) $line -eq '  entries:' })
if ($entriesIndex -lt 0) {
    throw 'The registry does not contain a serialized entries list.'
}

$entryStarts = [System.Collections.Generic.List[int]]::new()
for ($index = $entriesIndex + 1; $index -lt $lines.Length; $index++) {
    if ($lines[$index] -match '^  - assetPath:') {
        $entryStarts.Add($index)
    }
}

if ($entryStarts.Count -lt 1) {
    throw 'The registry is already empty/lazy or has an unsupported serialization shape.'
}

function Get-EntryAssetPath {
    param([string[]]$Block)

    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add(($Block[0] -replace '^  - assetPath:\s*', '').Trim())

    for ($lineIndex = 1; $lineIndex -lt $Block.Length; $lineIndex++) {
        if ($Block[$lineIndex] -match '^    prefab:') {
            break
        }

        $parts.Add($Block[$lineIndex].Trim())
    }

    return (($parts -join ' ') -replace '\s+', ' ').Trim()
}

function Get-ShardKey {
    param([string]$AssetPath)

    $segments = ($AssetPath -replace '\\', '/') -split '/'
    if ($segments.Length -lt 2) {
        throw "Cannot derive a shard from asset path: $AssetPath"
    }

    $publisherIndex = if ($segments[0] -ieq 'Assets') { 1 } else { 0 }
    $packIndex = [Math]::Min($publisherIndex + 1, $segments.Length - 1)
    $rawKey = $segments[$publisherIndex] + '_' + $segments[$packIndex]
    return (($rawKey -replace '[^a-zA-Z0-9]+', '_').Trim('_').ToLowerInvariant())
}

function Get-DeterministicGuid {
    param([string]$Value)

    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('YourQuest.RegistryShard.v1|' + $Value)
        return ([System.BitConverter]::ToString($md5.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $md5.Dispose()
    }
}

$groups = [System.Collections.Generic.SortedDictionary[string, System.Collections.Generic.List[string[]]]]::new([System.StringComparer]::OrdinalIgnoreCase)

for ($entryIndex = 0; $entryIndex -lt $entryStarts.Count; $entryIndex++) {
    $start = $entryStarts[$entryIndex]
    $end = if ($entryIndex + 1 -lt $entryStarts.Count) { $entryStarts[$entryIndex + 1] - 1 } else { $lines.Length - 1 }
    $block = [string[]]$lines[$start..$end]
    $assetPath = Get-EntryAssetPath -Block $block
    $key = Get-ShardKey -AssetPath $assetPath

    if (-not $groups.ContainsKey($key)) {
        $groups.Add($key, [System.Collections.Generic.List[string[]]]::new())
    }

    $groups[$key].Add($block)
}

$countedEntries = 0
foreach ($pair in $groups.GetEnumerator()) {
    $countedEntries += $pair.Value.Count
}

if ($countedEntries -ne $entryStarts.Count) {
    throw "Shard validation failed: parsed $($entryStarts.Count), grouped $countedEntries."
}

# note: Preserve a complete recovery copy outside Assets before the generated root is reduced.
[System.IO.Directory]::CreateDirectory($backupFolder) | Out-Null
$backupPath = Join-Path $backupFolder 'YQRuntimeWorldAssetRegistry.asset.pre-shard'
if (-not (Test-Path -LiteralPath $backupPath)) {
    [System.IO.File]::Copy($registryPath, $backupPath, $false)
}

[System.IO.Directory]::CreateDirectory($shardFolder) | Out-Null

$folderMetaPath = $shardFolder + '.meta'
if (-not (Test-Path -LiteralPath $folderMetaPath)) {
    $folderGuid = Get-DeterministicGuid -Value 'YQWorldAssetShards.folder'
    $folderMeta = @(
        'fileFormatVersion: 2',
        "guid: $folderGuid",
        'folderAsset: yes',
        'DefaultImporter:',
        '  externalObjects: {}',
        '  userData: ',
        '  assetBundleName: ',
        '  assetBundleVariant: '
    )
    [System.IO.File]::WriteAllLines($folderMetaPath, $folderMeta, $utf8NoBom)
}

$header = [string[]]$lines[0..($entriesIndex - 1)]

foreach ($pair in $groups.GetEnumerator()) {
    $assetName = 'YQWorldAssets_' + $pair.Key
    $assetPath = Join-Path $shardFolder ($assetName + '.asset')
    $assetLines = [System.Collections.Generic.List[string]]::new()

    foreach ($headerLine in $header) {
        if ($headerLine -match '^  m_Name:') {
            $assetLines.Add('  m_Name: ' + $assetName)
        }
        else {
            $assetLines.Add($headerLine)
        }
    }

    $assetLines.Add('  entries:')
    foreach ($block in $pair.Value) {
        $assetLines.AddRange($block)
    }
    $assetLines.Add('  useLazyResourceShards: 0')

    [System.IO.File]::WriteAllLines($assetPath, $assetLines, $utf8NoBom)

    $assetGuid = Get-DeterministicGuid -Value $assetName
    $metaLines = @(
        'fileFormatVersion: 2',
        "guid: $assetGuid",
        'NativeFormatImporter:',
        '  externalObjects: {}',
        '  mainObjectFileID: 11400000',
        '  userData: ',
        '  assetBundleName: ',
        '  assetBundleVariant: '
    )
    [System.IO.File]::WriteAllLines($assetPath + '.meta', $metaLines, $utf8NoBom)
}

# note: Write the tiny router last so any earlier failure leaves the original monolithic registry authoritative.
$rootLines = [System.Collections.Generic.List[string]]::new()
$rootLines.AddRange($header)
$rootLines.Add('  entries: []')
$rootLines.Add('  useLazyResourceShards: 1')
[System.IO.File]::WriteAllLines($registryPath, $rootLines, $utf8NoBom)

Write-Output ("Lazy registry migration complete. Entries={0}; shards={1}; backup={2}" -f $entryStarts.Count, $groups.Count, $backupPath)
