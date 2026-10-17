param(
    [string]$SourceRoot = 'E:\GPT LLM TEST',
    [string]$Destination = 'C:\Ai\YourQuestRuntime'
)
$ErrorActionPreference = 'Stop'
# note: Provision only the active weights and their exact engine; never overwrite other installed models or remove the HDD originals.
$modelNames = @('Qwen3.5-4B-Q4_K_M.gguf', 'LFM2-1.2B-RAG-Q4_K_M.gguf')
$engineVersion = 'b10828-cuda13.3'
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) { throw "Destination already exists; verify its manifest instead of overwriting: $destinationPath" }
$stagingPath = $destinationPath + '.staging-' + [Guid]::NewGuid().ToString('N')
$files = @()
foreach ($name in $modelNames) {
    $files += [pscustomobject]@{ Source = Join-Path $SourceRoot "untested\$name"; Relative = "Models\$name" }
}
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $SourceRoot "Runtime\$engineVersion") -File)) {
    $files += [pscustomobject]@{ Source = $file.FullName; Relative = "Runtime\$engineVersion\$($file.Name)" }
}
$receipts = @()
$elapsed = [Diagnostics.Stopwatch]::StartNew()
foreach ($file in $files) {
    $source = Get-Item -LiteralPath $file.Source
    $sourceHash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($source.Name -eq $modelNames[0] -and $sourceHash -ne '3874209241c9a397e2f62cd3f70f80fd2dfbf0dfccb6838416bdb48a714e8630') {
        throw 'Control model does not match the currently pinned speech identity.'
    }
    $target = Join-Path $stagingPath $file.Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath $source.FullName -Destination $target
    $copied = Get-Item -LiteralPath $target
    $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($source.Length -ne $copied.Length -or $sourceHash -ne $targetHash) { throw "Copy verification failed: $($file.Relative)" }
    $receipts += [ordered]@{ relative=$file.Relative; source=$source.FullName; bytes=$copied.Length; sha256=$targetHash }
    Write-Output "Verified $($file.Relative)"
}
# note: Publish the complete verified directory atomically; an interrupted staging directory is never selected by the game.
$manifest = [ordered]@{ schema=1; utc=[DateTime]::UtcNow.ToString('o'); engine=$engineVersion; destination=$destinationPath; files=$receipts }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stagingPath 'manifest.json') -Encoding utf8
[IO.Directory]::Move($stagingPath, $destinationPath)
Write-Output "Published $($receipts.Count) verified files to $destinationPath in $([Math]::Round($elapsed.Elapsed.TotalSeconds, 2)) seconds."
