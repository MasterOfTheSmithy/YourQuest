param()

$ErrorActionPreference = 'Stop'
# note: Read-only entry point for animation work; it never opens Unity, imports assets, changes a save, or treats a receipt as current merely because its filename exists.
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePaths = @(
    'Assets/Assets/Scripts/Tutorial/YQPlayerEquipmentVisual.cs',
    'Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs',
    'Assets/Assets/Scripts/Tutorial/YQFirstPersonArmsView.cs',
    'Assets/Assets/Scripts/Tutorial/YQInvestorCombat.cs',
    'Assets/Assets/Scripts/Tutorial/Editor/YQAuthoredPlayerAnimations.cs',
    'Assets/Assets/Scripts/Tutorial/Editor/YQPlayerLocomotionRepair.cs',
    'Assets/Assets/Scripts/Tutorial/Editor/YQPlayerActionAnimationSetup.cs',
    'Assets/Assets/Scripts/Tutorial/Editor/YQPlayerAnimationRuntimeVerification.cs',
    'Assets/Assets/Resources/Player/YQPlayer.controller',
    'Assets/Assets/Resources/Player/YQFirstPersonArms.asset'
)
$sources = foreach ($relativePath in $sourcePaths) {
    $file = Get-Item -LiteralPath (Join-Path $projectRoot $relativePath) -ErrorAction SilentlyContinue
    if ($null -ne $file) {
        [ordered]@{ path = $relativePath; modifiedUtc = $file.LastWriteTimeUtc.ToString('o'); sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    } else { [ordered]@{ path = $relativePath; missing = $true } }
}
$assemblies = foreach ($name in @('Assembly-CSharp.dll', 'Assembly-CSharp-Editor.dll')) {
    $file = Get-Item -LiteralPath (Join-Path $projectRoot "Library/ScriptAssemblies/$name") -ErrorAction SilentlyContinue
    if ($null -ne $file) {
        [ordered]@{ name = $name; modifiedUtc = $file.LastWriteTimeUtc.ToString('o'); sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    }
}
$receipts = foreach ($relativePath in @(
    'Docs/Player_Animation_Editor_Receipt_2026-10-01.json',
    'Docs/Player_Animation_Locomotion_Editor_Receipt_2026-10-02.json',
    'Docs/Player_Animation_Runtime_Receipt_2026-10-02.json'
)) {
    $fullPath = Join-Path $projectRoot $relativePath
    if (!(Test-Path -LiteralPath $fullPath)) { [ordered]@{ path = $relativePath; status = 'MISSING' }; continue }
    try {
        $receipt = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
        # note: New PowerShell versions deserialize ISO dates as DateTime; casting preserves UTC instead of reparsing a culture-formatted local string.
        $receiptTime = ([DateTimeOffset]$receipt.utc).UtcDateTime
        $newerSources = @($sources | Where-Object { $_.modifiedUtc -and [DateTimeOffset]::Parse($_.modifiedUtc).UtcDateTime -gt $receiptTime } | ForEach-Object { $_.path })
        [ordered]@{ path = $relativePath; status = $receipt.status; utc = $receipt.utc; error = $receipt.error; changedSinceReceipt = $newerSources; currentBuildCertified = $false }
    } catch { [ordered]@{ path = $relativePath; status = 'UNREADABLE'; error = $_.Exception.Message } }
}
$editorStatusPath = Join-Path $projectRoot 'Temp/YQ_STREAMER_PLAYMODE.status'
$editorStatus = if (Test-Path -LiteralPath $editorStatusPath) { @(Get-Content -LiteralPath $editorStatusPath | Where-Object { $_ -match '^(playing|playingOrWillChange|paused|compiling|updating|gameplayPresentationReleased|gameplayInputReady|utc)=' }) } else { @('No editor status file; observe Unity directly.') }
[ordered]@{
    inspectedUtc = [DateTime]::UtcNow.ToString('o')
    guide = 'Docs/Player_Animation_Workflow.md'
    evidenceBoundary = 'File freshness and historical receipts only. Inspect fresh Unity diagnostics, match the tested build, and visually review both views before claiming acceptance.'
    editor = $editorStatus
    receipts = @($receipts)
    assemblies = @($assemblies)
    sources = @($sources)
} | ConvertTo-Json -Depth 8
