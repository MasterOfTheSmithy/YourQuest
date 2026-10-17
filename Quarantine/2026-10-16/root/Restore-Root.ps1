param(
    [switch]$Apply,
    [switch]$DryRun,
    [string]$WorkspaceRoot = ([IO.DirectoryInfo]$PSScriptRoot).Parent.Parent.Parent.FullName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Apply -and $DryRun) { throw 'Choose Apply or DryRun, not both.' }
$workspaceFull = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\', '/')
$archiveRelative = 'Quarantine/2026-10-16/root'
$expectedNames = @('start', 'continue', 'testchange.txt', 'Run_Project_Dump.bat')

function Assert-LocalPath([string]$path) {
    # note: Reject lexical escape and every existing reparse point between this workspace and the exact file.
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith($workspaceFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes workspace: $full"
    }
    $cursor = $full
    while ($cursor.Length -ge $workspaceFull.Length) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point refused: $cursor" }
        }
        if ($cursor.Equals($workspaceFull, [StringComparison]::OrdinalIgnoreCase)) { break }
        $cursor = Split-Path -Parent $cursor
    }
    return $full
}

$manifestPath = Assert-LocalPath (Join-Path $workspaceFull "$archiveRelative/manifest.json")
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.status -ne 'QUARANTINED_VERIFIED' -or $manifest.files.Count -ne $expectedNames.Count) { throw 'Manifest is incomplete or not a verified four-file quarantine.' }
if (-not ([IO.Path]::GetFullPath($manifest.workspaceRoot).TrimEnd('\', '/')).Equals($workspaceFull, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest workspace does not match.' }
$seen = @{}
$plan = @()
foreach ($row in $manifest.files) {
    if ($expectedNames -notcontains $row.source -or $seen.ContainsKey([string]$row.source)) { throw 'Unknown or duplicate original path.' }
    $seen[[string]$row.source] = $true
    if ($row.destination -cne "$archiveRelative/$($row.source).quarantined") { throw 'Unexpected quarantine path.' }
    if ($row.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Missing or malformed checksum.' }
    $source = Assert-LocalPath (Join-Path $workspaceFull $row.source)
    $destination = Assert-LocalPath (Join-Path $workspaceFull $row.destination)
    if (Test-Path -LiteralPath $source) { throw "Original already exists; refusing overwrite: $source" }
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) { throw "Quarantined file is missing: $destination" }
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ine $row.sha256) { throw "Quarantined bytes changed: $destination" }
    if ((Get-Item -LiteralPath $destination -Force).Length -ne $row.bytes) { throw "Quarantined length changed: $destination" }
    $plan += [pscustomobject]@{ source = $source; destination = $destination; sha256 = $row.sha256; bytes = $row.bytes }
}
if (-not $Apply) {
    [ordered]@{ status = 'DRY_RUN_VALIDATED'; files = $plan.Count; overwrite = $false } | ConvertTo-Json
    return
}

$restored = [Collections.Generic.List[object]]::new()
try {
    foreach ($row in $plan) {
        # note: Recheck the source/target boundary immediately before each literal-path, nonrecursive move.
        $null = Assert-LocalPath $row.source
        $null = Assert-LocalPath $row.destination
        if ((Test-Path -LiteralPath $row.source) -or (Get-FileHash -LiteralPath $row.destination -Algorithm SHA256).Hash -ine $row.sha256) { throw 'Restore input changed after precheck.' }
        Move-Item -LiteralPath $row.destination -Destination $row.source
        $restored.Add($row)
    }
    foreach ($row in $plan) {
        if ((Test-Path -LiteralPath $row.destination) -or (Get-FileHash -LiteralPath $row.source -Algorithm SHA256).Hash -ine $row.sha256) { throw 'Restored bytes did not verify.' }
    }
    [ordered]@{ status = 'RESTORED_VERIFIED'; files = $plan.Count } | ConvertTo-Json
}
catch {
    # note: Roll back only verified files moved by this invocation; never overwrite an intervening file.
    $failure = $_
    for ($index = $restored.Count - 1; $index -ge 0; $index--) {
        $row = $restored[$index]
        if (Test-Path -LiteralPath $row.destination) { throw "Restore rollback blocked by an existing quarantine file: $($row.destination). Original failure: $failure" }
        if ((Get-FileHash -LiteralPath $row.source -Algorithm SHA256).Hash -ine $row.sha256) { throw "Restore rollback refused changed bytes. Original failure: $failure" }
        $null = Assert-LocalPath $row.source
        $null = Assert-LocalPath $row.destination
        Move-Item -LiteralPath $row.source -Destination $row.destination
    }
    throw $failure
}
