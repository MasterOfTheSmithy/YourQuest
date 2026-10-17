param([Parameter(Mandatory=$true)][string[]]$Targets, [switch]$DeleteOriginal)
$ErrorActionPreference = 'Stop'
$quarantineWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$quarantineRoot = [IO.Path]::GetFullPath('E:\YourQuest-TempFiles')
$quarantineBatch = Join-Path $quarantineRoot ('InitialModelStack-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
$quarantineRecords = [Collections.Generic.List[object]]::new()
# note: Copy and hash every regular file before any source removal; reparse points are never followed.
foreach ($target in $Targets) {
    $source = (Resolve-Path -LiteralPath (Join-Path $quarantineWorkspace $target)).Path
    if (!$source.StartsWith($quarantineWorkspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Source escapes workspace.' }
    if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked source refused.' }
    if (Test-Path -LiteralPath $source -PathType Container) {
        foreach ($directory in Get-ChildItem -LiteralPath $source -Recurse -Force -Directory) {
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked directory refused.' }
        }
    }
    $files = if (Test-Path -LiteralPath $source -PathType Container) { @(Get-ChildItem -LiteralPath $source -Recurse -Force -File) } else { @(Get-Item -LiteralPath $source) }
    foreach ($file in $files) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked file refused.' }
        $relative = $file.FullName.Substring($quarantineWorkspace.Length + 1)
        $destination = [IO.Path]::GetFullPath((Join-Path $quarantineBatch $relative))
        if (!$destination.StartsWith($quarantineRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination escapes quarantine.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $file.FullName -Destination $destination
        if ((Get-Item -LiteralPath $destination).Length -ne $file.Length -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "Copy mismatch: $relative" }
        $quarantineRecords.Add([ordered]@{ source=$file.FullName; destination=$destination; bytes=$file.Length; sha256=$hash })
    }
}
[IO.Directory]::CreateDirectory($quarantineBatch) | Out-Null
$receiptPath = Join-Path $quarantineBatch 'manifest.json'
$receipt = [ordered]@{ schema=1; utc=[DateTime]::UtcNow.ToString('o'); deleteOriginal=[bool]$DeleteOriginal; files=$quarantineRecords.ToArray(); removed=[Collections.Generic.List[string]]::new() }
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding utf8
if ($DeleteOriginal) {
    # note: Recheck both preimages immediately before deleting exact files; remove only empty directories afterwards.
    foreach ($record in $quarantineRecords) {
        if ((Get-FileHash -LiteralPath $record.source -Algorithm SHA256).Hash -ne $record.sha256 -or (Get-FileHash -LiteralPath $record.destination -Algorithm SHA256).Hash -ne $record.sha256) { throw 'Preimage changed; deletion refused.' }
    }
    foreach ($record in $quarantineRecords) { Remove-Item -LiteralPath $record.source; $receipt.removed.Add($record.source) }
    foreach ($target in $Targets) {
        $source = [IO.Path]::GetFullPath((Join-Path $quarantineWorkspace $target))
        if (!$source.StartsWith($quarantineWorkspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target escapes workspace.' }
        if (Test-Path -LiteralPath $source -PathType Container) {
            Get-ChildItem -LiteralPath $source -Directory -Recurse -Force | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
                if (!(Get-ChildItem -LiteralPath $_.FullName -Force)) { Remove-Item -LiteralPath $_.FullName }
            }
            if (!(Get-ChildItem -LiteralPath $source -Force)) { Remove-Item -LiteralPath $source }
        }
    }
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding utf8
}
$quarantineBytes = 0L
foreach ($record in $quarantineRecords) { $quarantineBytes += $record.bytes }
Write-Output ([ordered]@{ manifest=$receiptPath; verifiedFiles=$quarantineRecords.Count; bytes=$quarantineBytes; deletedFiles=$receipt.removed.Count } | ConvertTo-Json)
