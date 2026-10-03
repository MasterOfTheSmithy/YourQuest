param([string]$ProjectRoot = 'C:\Users\Garri\YourQuest', [string]$OutputPath = 'outputs/DOT_Integration_20261002/delivery-receipt.json')
$ErrorActionPreference = 'Stop'
# note: Verify only declared source GLBs inside the two supplied libraries. Write evidence without modifying source files or Unity caches.
$baseRoot = Join-Path $ProjectRoot 'Assets/Assets/GeneratedAssets/DOT Generated Assets/Equipment/Library'
$expansionRoot = Join-Path $ProjectRoot 'Assets/Assets/GeneratedAssets/DOT Generated Assets/Equipment/Modular'
# note: Exact source aliases preserve delivered relative-path/hash contracts when their files are organized by equipment type.
$dotLayoutPath = Join-Path $ProjectRoot 'Assets/Assets/GeneratedAssets/DOT Generated Assets/Catalogs/DOT_ASSET_LAYOUT.json'
$dotRelocations = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
if (Test-Path -LiteralPath $dotLayoutPath) {
    $dotLayout = Get-Content -LiteralPath $dotLayoutPath -Raw | ConvertFrom-Json
    foreach ($dotFile in $dotLayout.files) {
        $dotRelocations.Add($dotFile.origin, $dotFile.path)
        foreach ($dotAlias in $dotFile.aliases) { $dotRelocations.Add($dotAlias, $dotFile.path) }
    }
}
function Resolve-DotSourcePath([string]$Path) {
    $dotAbsolute = [IO.Path]::GetFullPath($Path)
    $dotProjectPrefix = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\') + '\'
    if (!$dotAbsolute.StartsWith($dotProjectPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'DOT path escapes project' }
    $dotRelative = $dotAbsolute.Substring($dotProjectPrefix.Length).Replace('\', '/')
    if ($dotRelocations.ContainsKey($dotRelative)) { return Join-Path $ProjectRoot $dotRelocations[$dotRelative] }
    return $dotAbsolute
}
$catalog = Get-Content -LiteralPath (Resolve-DotSourcePath (Join-Path $baseRoot 'CATALOG.json')) -Raw | ConvertFrom-Json
$checksums = Get-Content -LiteralPath (Resolve-DotSourcePath (Join-Path $expansionRoot 'FILE_CHECKSUMS.json')) -Raw | ConvertFrom-Json
$checks = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()
function Test-DeliveredFile([string]$Root, [string]$Relative, [string]$Expected) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (!$absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add([pscustomobject]@{ file = $Relative; reason = 'Outside supplied root' }); return
    }
    $absolute = Resolve-DotSourcePath $absolute
    if (![IO.File]::Exists($absolute)) {
        $failures.Add([pscustomobject]@{ file = $Relative; reason = 'Missing or outside supplied root' }); return
    }
    $actual = (Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) { $failures.Add([pscustomobject]@{ file = $Relative; reason = 'Delivered hash mismatch'; actual = $actual; expected = $Expected }) }
    $checks.Add([pscustomobject]@{ file = $absolute.Substring($ProjectRoot.Length + 1); sha256 = $actual })
}
foreach ($entry in $catalog) {
    foreach ($export in $entry.exports.PSObject.Properties) {
        if ($export.Value.path -and $export.Value.sha256) { Test-DeliveredFile $baseRoot $export.Value.path $export.Value.sha256 }
    }
}
foreach ($entry in $checksums) {
    if ($entry.file.EndsWith('.glb', [StringComparison]::OrdinalIgnoreCase)) { Test-DeliveredFile $expansionRoot $entry.file $entry.sha256 }
}
$output = Join-Path $ProjectRoot $OutputPath
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
$status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
[ordered]@{ status = $status; utc = [DateTime]::UtcNow.ToString('o'); sourceChecks = $checks.Count; failures = $failures; checks = $checks; evidence = 'Declared source-file integrity only. Unity rendering, outfit fit, and gameplay are separate gates.' } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $output -Encoding utf8
[ordered]@{ status = $status; sourceChecks = $checks.Count; failures = $failures.Count; receipt = $output } | ConvertTo-Json
