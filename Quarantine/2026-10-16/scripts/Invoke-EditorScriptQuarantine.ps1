# note: This output-only helper plans a complete source/.meta quarantine batch; -Apply is deliberately gated on a reviewed compile receipt.
[CmdletBinding()]
param(
    [ValidateSet('Archive', 'Restore')]
    [string]$Action = 'Archive',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$outputScriptDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'outputs\ProjectSterilization_20261016\EditorAudit'))
$quarantineRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'Quarantine\2026-10-16\scripts'))
$isOutputLocation = [string]::Equals([IO.Path]::GetFullPath($PSScriptRoot), $outputScriptDirectory, [StringComparison]::OrdinalIgnoreCase)
$isQuarantineLocation = [string]::Equals([IO.Path]::GetFullPath($PSScriptRoot), $quarantineRoot, [StringComparison]::OrdinalIgnoreCase)
if (-not ($isOutputLocation -or $isQuarantineLocation)) {
    throw 'Helper location is neither the expected output directory nor the archived restore directory.'
}
if ($Action -eq 'Archive' -and -not $isOutputLocation) {
    throw 'Archive must be launched from the reviewed output helper; the quarantined copy is for Restore.'
}
if ($Action -eq 'Restore' -and -not $isQuarantineLocation) {
    throw 'Restore must use the self-contained helper inside Quarantine/2026-10-16/scripts.'
}
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git'))) {
    throw 'The expected YourQuest Git checkout is missing.'
}

$manifestPath = Join-Path $PSScriptRoot 'quarantine_manifest.json'
$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
$manifestHash = ([BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($manifestBytes))).Replace('-', '').ToLowerInvariant()
$manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json
if ($manifest.schema -ne 1 -or $manifest.quarantine_root -cne 'Quarantine/2026-10-16/scripts') {
    throw 'Quarantine manifest schema or destination is not the approved layout.'
}

$approvedPreimages = @{
    'Assets/Assets/Scripts/PrototypeBuilder/Editor/YQPrototypeSceneBuilder.cs' = '85afee572d488da0ad87ca947a6a5745c33dd50f995a2e473fb96f9700ab8a25|e047aac4e83ea3e3042c20fab261ca4dc83a1300abbec247df8f32dc91a8c80a|bbb5cbbee8039264a920c8aa30bea996'
    'Assets/Assets/Scripts/Tutorial/Editor/YourQuestPolishedPrototypeSceneMenu.cs' = 'd58b6ff0ebe5078eab95676388481123000bc8b73c63fa3c13e92dab1dc3bc13|08592080a2f8227d0ec84dc7af6ecc474a32cae6b11e5770ef888afc05c33042|be73e4db11c09dc4f9f4956e150873a1'
    'Assets/Assets/Scripts/Tutorial/Editor/YourQuestTestSceneMenu.cs' = 'ca7bf87c56e3fc23a8062d3d2fdf6967f7cc92c1103d7ccc0dcac183c93c0ddc|c2be08914e49732a61aced925f1e6495a9963371da5ee264b63f3cc3c167c821|939e6e636ca115941bbf5e1264591d6d'
    'Assets/Assets/Scripts/Tutorial/Editor/YourQuestInvestorSceneBuilder.cs' = 'a876161d27e365519195143087ef066a31f48cd7a9d3d6e9a99dc72ffca33a61|1f68800a9302a3a946e71d191d7813394d3e98f597c0f1678e62b86f2371d64a|858cc479f2df6414e854a059bac75457'
    'Assets/Assets/Scripts/Data/Serialization/SerializableDictionary.cs' = 'dd3f24e9fd27a190a8059741d7f3925b5e58c0e1b17d84c0d0b0f76b246cd8aa|9a5a16fbde75573793d982a34f6d6df3f336a59b7f43b44bca634add76ca7316|b5920985f3af8d6448d620a6782b18a5'
    'Assets/Assets/Scripts/Data/State/StateCompatibilityExtensions.cs' = '0189404d390377b739814684b1852929b4c2ac4e1a421db906fc49e1da22a6d4|9ad3e3587bc13d0bfb3b94876fe93e8cda0d437d8907c5bd5357d190937ff761|5f86870a2cb205e479b60a7df22b672c'
    'Assets/Assets/Scripts/Generated/YQRuntimeWorldAssetRegistrySmokeTest.cs' = '31f322fdae8027161fe2418fd7f34ac169bd2282d4d3ec776764e36317b5d5ba|fe4a684094bca0af78658db68aea72392ab478b44f552bd8ef0fbb554fc4f325|cac4030ef3a125949af29447e9ac0dc3'
    'Assets/Assets/Scripts/LLM/Debug/DebugSkillGenerator.cs' = 'e1d818d7de410a9ee096877eeb7e09be553f34303c02f1db74b273705c0f3ccd|f72ff58311738cd8d784b30a94f70b9ab61564e0d1090a2416d4170f667351ba|4620cf832175f624c9c1d4f9adf787ca'
    'Assets/Assets/Scripts/Observation/Threat/ThreatProbe.cs' = '87fee411314e23037da334a3be54e4425f426cee4350349abde650e7c2ca2428|602da181ef944796c2693dcec0cd8bb9af2736ce8a72614b87e9f252a866728b|d289e61ba75b50641af79053dec45e4e'
    'Assets/Assets/Scripts/Tutorial/YourQuestTutorialVitals.cs' = 'cc8e72dee3b807778303f87b3a278871b424264826ceb44ac4929473fa69518d|6f61e3366ceab3ecea1ac19a4719db9bab25b56a074dc4264d57f7a96bbc9af9|6739c2d78fec7f24e8cc2705e2c61f8e'
}

function Assert-WithinRepository([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes the intended checkout: $full"
    }
    return $full
}

function Assert-NoReparse([string]$Path) {
    $cursor = Assert-WithinRepository $Path
    while ($true) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point in transfer path: $cursor"
            }
        }
        if ([string]::Equals($cursor, $repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) { break }
        $next = [IO.Path]::GetDirectoryName($cursor)
        if ([string]::IsNullOrWhiteSpace($next) -or $next.Length -lt $repositoryRoot.Length) {
            throw "Path ancestry escapes the intended checkout: $Path"
        }
        $cursor = $next
    }
}

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hasher.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally {
        $hasher.Dispose()
        $stream.Dispose()
    }
}

function Assert-Hash([string]$Path, [string]$Expected) {
    $actual = Get-Sha256 $Path
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 mismatch: $Path; expected $Expected; actual $actual"
    }
}

function Assert-UnityClosed {
    $unityProcesses = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($unityProcesses.Count -ne 0) {
        throw 'Unity.exe is running; no source or .meta file will be moved.'
    }
}

function Assert-SafeRelativeSource([string]$RelativePath) {
    if ($RelativePath -notmatch '^Assets/Assets/Scripts/.+\.cs$' -or $RelativePath.Contains('\') -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Manifest path is not a project-owned script: $RelativePath"
    }
    foreach ($segment in $RelativePath.Split('/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..' -or $segment.Contains(':')) {
            throw "Unsafe manifest path segment: $RelativePath"
        }
    }
}

function Assert-Preflight([object[]]$Entries) {
    foreach ($entry in $Entries) {
        Assert-NoReparse $entry.From
        Assert-NoReparse $entry.To
        if (-not (Test-Path -LiteralPath $entry.From -PathType Leaf)) {
            throw "Required source file is missing: $($entry.From)"
        }
        if (Test-Path -LiteralPath $entry.To) {
            throw "Destination already exists; refusing overwrite: $($entry.To)"
        }
        Assert-Hash $entry.From $entry.Sha256
    }
}

function Ensure-DestinationDirectory([string]$Directory, [System.Collections.Generic.List[string]]$Created) {
    Assert-NoReparse $Directory
    $missing = New-Object 'System.Collections.Generic.List[string]'
    $cursor = $Directory
    while (-not (Test-Path -LiteralPath $cursor)) {
        $missing.Add($cursor)
        $cursor = [IO.Path]::GetDirectoryName($cursor)
        if (-not [string]::Equals($cursor, $repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            [void](Assert-WithinRepository $cursor)
        }
    }
    if (-not (Test-Path -LiteralPath $cursor -PathType Container)) {
        throw "Destination parent is not a directory: $cursor"
    }
    for ($i = $missing.Count - 1; $i -ge 0; $i--) {
        [IO.Directory]::CreateDirectory($missing[$i]) | Out-Null
        $Created.Add($missing[$i])
    }
    Assert-NoReparse $Directory
}

function Undo-AttemptedTransfers([object[]]$Attempted) {
    $problems = New-Object 'System.Collections.Generic.List[string]'
    for ($i = $Attempted.Count - 1; $i -ge 0; $i--) {
        $entry = $Attempted[$i]
        try {
            $fromExists = Test-Path -LiteralPath $entry.From
            $toExists = Test-Path -LiteralPath $entry.To
            if ($toExists -and -not $fromExists) {
                Assert-NoReparse $entry.From
                Assert-NoReparse $entry.To
                Assert-Hash $entry.To $entry.Sha256
                [IO.File]::Move($entry.To, $entry.From)
                Assert-Hash $entry.From $entry.Sha256
            } elseif ($fromExists -and -not $toExists) {
                Assert-Hash $entry.From $entry.Sha256
            } else {
                throw 'Transfer state is ambiguous; no overwrite attempted.'
            }
        } catch {
            $problems.Add("$($entry.Relative): $($_.Exception.Message)")
        }
    }
    return @($problems.ToArray())
}

$files = @($manifest.files)
if ($files.Count -ne $approvedPreimages.Count) { throw 'Quarantine manifest must contain exactly the ten reviewed scripts.' }
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
    $relative = [string]$file.path
    Assert-SafeRelativeSource $relative
    if (-not $seen.Add($relative)) { throw "Duplicate manifest path: $relative" }
    if ($file.guid -notmatch '^[0-9a-fA-F]{32}$' -or $file.source_sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $file.meta_sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Incomplete exact preimage for $relative"
    }
    if (-not $approvedPreimages.ContainsKey($relative)) { throw "Unapproved batch member: $relative" }
    $expected = $approvedPreimages[$relative]
    $actual = "$($file.source_sha256)|$($file.meta_sha256)|$($file.guid)"
    if (-not [string]::Equals($expected, $actual, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Accepted preimage changed in manifest: $relative"
    }
}
foreach ($required in $approvedPreimages.Keys) {
    if (-not $seen.Contains($required)) { throw "Accepted member missing from batch: $required" }
}

# note: This digest binds the compile receipt to the exact source/meta/GUID batch, independently of review-state edits.
$candidateTuples = [string[]]@($files | ForEach-Object {
    "$($_.path)|$(([string]$_.guid).ToLowerInvariant())|$(([string]$_.source_sha256).ToLowerInvariant())|$(([string]$_.meta_sha256).ToLowerInvariant())"
})
[Array]::Sort($candidateTuples, [StringComparer]::Ordinal)
$candidateBytes = [Text.Encoding]::UTF8.GetBytes(($candidateTuples -join "`n") + "`n")
$candidateSetHash = ([BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($candidateBytes))).Replace('-', '').ToLowerInvariant()

if ($Apply -and $Action -eq 'Archive') {
    if ($manifest.review_state -cne 'COORDINATOR_CLEARED_AFTER_DETACHED_COMPILE') {
        throw 'Archive remains staged; coordinator review and the combined detached compile are not recorded.'
    }
    $compileReceiptRelative = [string]$manifest.detached_compile_receipt
    if ($compileReceiptRelative -notmatch '^outputs/ProjectSterilization_20261016/.+' -or $compileReceiptRelative.Contains('..') -or $compileReceiptRelative.Contains('\')) {
        throw 'A project-output detached compile receipt path is required before archive.'
    }
    $compileReceiptPath = Assert-WithinRepository (Join-Path $repositoryRoot $compileReceiptRelative)
    Assert-NoReparse $compileReceiptPath
    if (-not (Test-Path -LiteralPath $compileReceiptPath -PathType Leaf)) {
        throw "Detached compile receipt is missing: $compileReceiptPath"
    }
    $compileReceipt = Get-Content -LiteralPath $compileReceiptPath -Raw | ConvertFrom-Json
    if ($compileReceipt.status -cne 'PASS' -or [int]$compileReceipt.compiler_exit_code -ne 0 -or
        -not [string]::Equals([string]$compileReceipt.candidate_set_sha256, $candidateSetHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Detached compile receipt is not a zero-exit PASS for this exact candidate set.'
    }
    $excluded = @($compileReceipt.excluded_sources)
    if ($excluded.Count -ne $files.Count) { throw 'Detached compile excluded-source count differs from the exact batch.' }
    $excludedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $excluded) {
        if (-not $excludedSet.Add([string]$path)) { throw "Duplicate excluded source in compile receipt: $path" }
    }
    foreach ($file in $files) {
        if (-not $excludedSet.Contains([string]$file.path)) { throw "Compile did not exclude the exact batch member: $($file.path)" }
    }

    $sourceInputs = @($compileReceipt.source_inputs)
    if ($sourceInputs.Count -le $files.Count) { throw 'Detached compile receipt lacks the combined runtime and Editor source inputs.' }
    $sourceHashes = @{}
    foreach ($input in $sourceInputs) {
        $path = [string]$input.path
        if ($path -notmatch '^Assets/.+\.cs$' -or $path.Contains('..') -or $path.Contains('\') -or $sourceHashes.ContainsKey($path) -or
            [string]$input.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Unsafe or duplicate source input in compile receipt: $path"
        }
        $liveInput = Assert-WithinRepository (Join-Path $repositoryRoot $path)
        Assert-NoReparse $liveInput
        Assert-Hash $liveInput ([string]$input.sha256)
        $sourceHashes[$path] = [string]$input.sha256
    }
    foreach ($file in $files) {
        if (-not $sourceHashes.ContainsKey([string]$file.path) -or
            -not [string]::Equals($sourceHashes[[string]$file.path], [string]$file.source_sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Candidate source preimage is not in the detached compile inputs: $($file.path)"
        }
    }

    $metaInputs = @($compileReceipt.meta_inputs)
    if ($metaInputs.Count -ne $files.Count) { throw 'Detached compile receipt must bind each candidate .meta preimage.' }
    $metaHashes = @{}
    foreach ($input in $metaInputs) {
        $path = [string]$input.path
        if ($path -notmatch '^Assets/Assets/Scripts/.+\.cs\.meta$' -or $path.Contains('..') -or $path.Contains('\') -or
            $metaHashes.ContainsKey($path) -or [string]$input.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Unsafe or duplicate .meta input in compile receipt: $path"
        }
        $liveInput = Assert-WithinRepository (Join-Path $repositoryRoot $path)
        Assert-NoReparse $liveInput
        Assert-Hash $liveInput ([string]$input.sha256)
        $metaHashes[$path] = [string]$input.sha256
    }
    foreach ($file in $files) {
        $metaPath = [string]$file.path + '.meta'
        if (-not $metaHashes.ContainsKey($metaPath) -or
            -not [string]::Equals($metaHashes[$metaPath], [string]$file.meta_sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Candidate .meta preimage is not in the detached compile inputs: $metaPath"
        }
    }

    $outputs = @($compileReceipt.assembly_outputs)
    if ($outputs.Count -ne 2) { throw 'Detached compile receipt must name both runtime and Editor assemblies.' }
    $outputKinds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($assembly in $outputs) {
        $path = [string]$assembly.path
        if (($assembly.kind -ne 'runtime' -and $assembly.kind -ne 'editor') -or -not $outputKinds.Add([string]$assembly.kind) -or
            $path -notmatch '^outputs/ProjectSterilization_20261016/.+' -or $path.Contains('..') -or $path.Contains('\') -or
            [string]$assembly.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Invalid compiled assembly evidence: $path"
        }
        $assemblyPath = Assert-WithinRepository (Join-Path $repositoryRoot $path)
        Assert-NoReparse $assemblyPath
        Assert-Hash $assemblyPath ([string]$assembly.sha256)
    }
}

$entries = @()
$trackedPaths = @()
foreach ($file in $files) {
    $relative = [string]$file.path
    $source = Assert-WithinRepository (Join-Path $repositoryRoot $relative)
    $meta = Assert-WithinRepository (Join-Path $repositoryRoot ($relative + '.meta'))
    $archivedSource = Assert-WithinRepository (Join-Path $quarantineRoot ($relative + '.quarantined'))
    $archivedMeta = Assert-WithinRepository (Join-Path $quarantineRoot ($relative + '.meta.quarantined'))
    $trackedPaths += $relative, ($relative + '.meta')
    $members = @(
        [pscustomobject]@{ Relative = $relative; Live = $source; Archived = $archivedSource; Sha256 = [string]$file.source_sha256 },
        [pscustomobject]@{ Relative = ($relative + '.meta'); Live = $meta; Archived = $archivedMeta; Sha256 = [string]$file.meta_sha256 }
    )
    foreach ($member in $members) {
        $from = if ($Action -eq 'Archive') { $member.Live } else { $member.Archived }
        $to = if ($Action -eq 'Archive') { $member.Archived } else { $member.Live }
        $entries += [pscustomobject]@{ Relative = $member.Relative; From = $from; To = $to; Sha256 = $member.Sha256 }
    }
    $metaInput = if ($Action -eq 'Archive') { $meta } else { $archivedMeta }
    if (Test-Path -LiteralPath $metaInput -PathType Leaf) {
        $actualGuid = [regex]::Match([IO.File]::ReadAllText($metaInput), '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$').Groups[1].Value
        if (-not [string]::Equals($actualGuid, [string]$file.guid, [StringComparison]::OrdinalIgnoreCase)) {
            throw "GUID mismatch in .meta: $metaInput"
        }
    }
}

$sidecars = @()
if ($Action -eq 'Archive') {
    $sidecars = @(
        [pscustomobject]@{ From = (Join-Path $outputScriptDirectory 'Invoke-EditorScriptQuarantine.ps1'); To = (Join-Path $quarantineRoot 'Invoke-EditorScriptQuarantine.ps1') },
        [pscustomobject]@{ From = $manifestPath; To = (Join-Path $quarantineRoot 'quarantine_manifest.json') },
        [pscustomobject]@{ From = (Join-Path $outputScriptDirectory 'Quarantine_README.md'); To = (Join-Path $quarantineRoot 'README.md') }
    )
    foreach ($sidecar in $sidecars) {
        Assert-NoReparse $sidecar.From
        Assert-NoReparse $sidecar.To
        if (-not (Test-Path -LiteralPath $sidecar.From -PathType Leaf)) { throw "Restore sidecar source is missing: $($sidecar.From)" }
        if (Test-Path -LiteralPath $sidecar.To) { throw "Restore sidecar destination already exists: $($sidecar.To)" }
        $sidecar | Add-Member -NotePropertyName Sha256 -NotePropertyValue (Get-Sha256 $sidecar.From)
    }
} else {
    foreach ($name in @('Invoke-EditorScriptQuarantine.ps1', 'quarantine_manifest.json', 'README.md')) {
        $sidecarPath = Join-Path $quarantineRoot $name
        Assert-NoReparse $sidecarPath
        if (-not (Test-Path -LiteralPath $sidecarPath -PathType Leaf)) { throw "Self-contained restore sidecar is missing: $sidecarPath" }
    }
}

if ($Action -eq 'Archive') {
    $gitStatus = & git -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- $trackedPaths
    if ($LASTEXITCODE -ne 0) { throw 'Git status precheck failed.' }
    if (-not [string]::IsNullOrWhiteSpace(($gitStatus -join "`n"))) {
        throw "One or more candidate scripts or .meta files have user changes: $($gitStatus -join '; ')"
    }
}
Assert-Preflight $entries

if (-not $Apply) {
    Write-Output "DRY RUN ${Action}: $($files.Count) scripts / $($entries.Count) paired files; manifest SHA-256 $manifestHash"
    Write-Output "Candidate-set SHA-256: $candidateSetHash"
    foreach ($entry in $entries) { Write-Output "$($entry.Relative) => $($entry.To)" }
    Write-Output 'No file was moved. -Apply requires the recorded coordinator/compile gate for Archive.'
    return
}

$createdDirectories = New-Object 'System.Collections.Generic.List[string]'
$attempted = @()
$copiedSidecars = @()
$receiptPath = $null
try {
    foreach ($sidecar in $sidecars) {
        Ensure-DestinationDirectory ([IO.Path]::GetDirectoryName($sidecar.To)) $createdDirectories
    }
    foreach ($entry in $entries) {
        Ensure-DestinationDirectory ([IO.Path]::GetDirectoryName($entry.To)) $createdDirectories
    }
    Assert-Preflight $entries
    Assert-UnityClosed
    foreach ($sidecar in $sidecars) {
        if (Test-Path -LiteralPath $sidecar.To) { throw "Restore sidecar appeared after preflight: $($sidecar.To)" }
        Assert-Hash $sidecar.From $sidecar.Sha256
        $copiedSidecars += $sidecar
        [IO.File]::Copy($sidecar.From, $sidecar.To, $false)
        Assert-Hash $sidecar.To $sidecar.Sha256
    }
    foreach ($entry in $entries) {
        # note: File.Move refuses an existing target; each source is checked again immediately before transfer.
        Assert-UnityClosed
        Assert-NoReparse $entry.From
        Assert-NoReparse $entry.To
        if (Test-Path -LiteralPath $entry.To) { throw "Destination appeared after preflight: $($entry.To)" }
        Assert-Hash $entry.From $entry.Sha256
        $attempted += $entry
        [IO.File]::Move($entry.From, $entry.To)
        Assert-Hash $entry.To $entry.Sha256
        if (Test-Path -LiteralPath $entry.From) { throw "Source still exists after transfer: $($entry.From)" }
    }
    foreach ($entry in $entries) {
        Assert-Hash $entry.To $entry.Sha256
        if (Test-Path -LiteralPath $entry.From) { throw "Source unexpectedly returned: $($entry.From)" }
    }

    $receipt = [ordered]@{
        action = $Action
        utc = [DateTime]::UtcNow.ToString('o')
        manifest_sha256 = $manifestHash
        candidate_set_sha256 = $candidateSetHash
        files = @($entries | ForEach-Object { [ordered]@{ relative = $_.Relative; sha256 = $_.Sha256; from = $_.From; to = $_.To } })
        restore_sidecars = @($sidecars | ForEach-Object { [ordered]@{ path = $_.To; sha256 = $_.Sha256 } })
        result = 'ALL_HASHES_VERIFIED'
    }
    $receiptPath = Join-Path $quarantineRoot ("quarantine_{0}_{1}.json" -f $Action.ToLowerInvariant(), [Guid]::NewGuid().ToString('N'))
    $bytes = [Text.Encoding]::UTF8.GetBytes(($receipt | ConvertTo-Json -Depth 6) + "`n")
    $stream = [IO.File]::Open($receiptPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    Write-Output "APPLIED ${Action}: $($files.Count) scripts / $($entries.Count) paired files; all destination SHA-256 checks passed."
    Write-Output "Receipt: $receiptPath"
} catch {
    $originalError = $_.Exception.Message
    $rollbackProblems = @(Undo-AttemptedTransfers $attempted)
    for ($i = $copiedSidecars.Count - 1; $i -ge 0; $i--) {
        $sidecar = $copiedSidecars[$i]
        try {
            if (Test-Path -LiteralPath $sidecar.To) {
                Assert-NoReparse $sidecar.To
                Assert-Hash $sidecar.To $sidecar.Sha256
                Remove-Item -LiteralPath $sidecar.To -Force
            }
        } catch { $rollbackProblems += "Sidecar $($sidecar.To) : $($_.Exception.Message)" }
    }
    for ($i = $createdDirectories.Count - 1; $i -ge 0; $i--) {
        $dir = $createdDirectories[$i]
        try {
            Assert-NoReparse $dir
            if ((Test-Path -LiteralPath $dir -PathType Container) -and @((Get-ChildItem -LiteralPath $dir -Force)).Count -eq 0) {
                Remove-Item -LiteralPath $dir -Force
            }
        } catch { $rollbackProblems += "Directory $dir : $($_.Exception.Message)" }
    }
    if ($null -ne $receiptPath -and (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        try { Remove-Item -LiteralPath $receiptPath -Force } catch { $rollbackProblems += "Receipt $receiptPath : $($_.Exception.Message)" }
    }
    if ($rollbackProblems.Count -ne 0) {
        throw "Quarantine $Action failed: $originalError. Rollback needs manual review: $($rollbackProblems -join '; ')"
    }
    throw "Quarantine $Action failed and file transfers were rolled back: $originalError"
}
