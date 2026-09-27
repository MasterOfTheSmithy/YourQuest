# C:\Users\Garri\YourQuest\YourQuest_ProjectDump.ps1
# LEAN YourQuest exporter:
# - Full text only for YourQuest-owned implementation/wiring.
# - Third-party/store assets are inventory-only.
# - Unity cache/generated folders are excluded.
# - Designed for ChatGPT review without producing hundreds of irrelevant chunks.

param(
    [string]$Root = $PSScriptRoot,
    [int]$MaxCharsPerTop = 300000,
    [int]$MaxEntriesPerTop = 20,
    [long]$MaxEmbeddedFileBytes = 5242880,       # 5 MiB
    [long]$MaxTotalEmbeddedChars = 75000000,     # 75M chars hard ceiling
    [switch]$NoZip
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# -----------------------------------------------------------------------------
# Paths
# -----------------------------------------------------------------------------

$RootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
$AssetsPath = Join-Path $RootPath 'Assets'
$ProjectSettingsPath = Join-Path $RootPath 'ProjectSettings'
$PackagesPath = Join-Path $RootPath 'Packages'

$OutputDirectoryName = 'ChatGPT_ProjectDump'
$OutDir = Join-Path $RootPath $OutputDirectoryName
$ZipPath = Join-Path $RootPath ($OutputDirectoryName + '.zip')

$GeneratedAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$script:TotalEmbeddedChars = [long]0

if (-not (Test-Path -LiteralPath $AssetsPath -PathType Container)) {
    throw "Assets folder not found: $AssetsPath. Run this from the Unity project root."
}

if (-not (Test-Path -LiteralPath $ProjectSettingsPath -PathType Container)) {
    throw "ProjectSettings folder not found: $ProjectSettingsPath. Run this from the Unity project root."
}

Write-Host ''
Write-Host '============================================================'
Write-Host ' YourQuest - LEAN ChatGPT Project Dump'
Write-Host '============================================================'
Write-Host ('Root:             ' + $RootPath)
Write-Host ('Output:           ' + $OutDir)
Write-Host ('Chunk chars:      ' + $MaxCharsPerTop)
Write-Host ('Entries/chunk:    ' + $MaxEntriesPerTop)
Write-Host ('Per-file cap:     ' + $MaxEmbeddedFileBytes)
Write-Host ('Global embed cap: ' + $MaxTotalEmbeddedChars)
Write-Host ''

if (Test-Path -LiteralPath $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutDir | Out-Null

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

function Write-Utf8Lines {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Lines
    )

    [System.IO.File]::WriteAllLines(
        $Path,
        [string[]]$Lines,
        $Utf8NoBom
    )
}

function Get-RelativeProjectPath {
    param([Parameter(Mandatory = $true)][string]$FullPath)

    $normalized = [System.IO.Path]::GetFullPath($FullPath)

    if ($normalized.StartsWith($RootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $normalized.Substring($RootPath.Length).TrimStart('\', '/').Replace('\', '/')
    }

    return $normalized.Replace('\', '/')
}

function Get-ExtensionLower {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if ([string]::IsNullOrWhiteSpace($File.Extension)) {
        return ''
    }

    return $File.Extension.ToLowerInvariant()
}

function Escape-Tsv {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return ([string]$Value).Replace("`t", ' ').Replace("`r", ' ').Replace("`n", ' ')
}

function Test-IsExcludedPath {
    param([Parameter(Mandatory = $true)][string]$FullPath)

    $p = $FullPath.Replace('/', '\')

    $excluded = @(
        '\Library\',
        '\Temp\',
        '\Logs\',
        '\obj\',
        '\bin\',
        '\.git\',
        '\.vs\',
        '\node_modules\',
        '\MemoryCaptures\',
        '\Recordings\',
        '\UserSettings\',
        '\Build\',
        '\Builds\',
        '\CrashReports\',
        '\BurstDebugInformation_DoNotShip\',
        ('\' + $OutputDirectoryName + '\')
    )

    foreach ($segment in $excluded) {
        if ($p.IndexOf($segment, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Test-IsFirstParty {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $relative = Get-RelativeProjectPath $File.FullName

    # Main YourQuest-owned implementation tree.
    if ($relative.StartsWith('Assets/Assets/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    # Current YourQuest-named root assets/scenes.
    if ($relative.StartsWith('Assets/YourQuest', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $false
}

function Get-AssetOwner {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $relative = Get-RelativeProjectPath $File.FullName

    if ($relative.StartsWith('Assets/Assets/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'YourQuest'
    }

    if ($relative.StartsWith('Assets/YourQuest', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'YourQuest'
    }

    if ($relative.StartsWith('Assets/', [System.StringComparison]::OrdinalIgnoreCase)) {
        $parts = $relative.Split('/')
        if ($parts.Count -ge 2) {
            return $parts[1]
        }
        return 'Assets'
    }

    if ($relative.StartsWith('ProjectSettings/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'ProjectSettings'
    }

    if ($relative.StartsWith('Packages/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'Packages'
    }

    return '<ROOT>'
}

$ScriptExtensions = @('.cs', '.asmdef', '.asmref')
$SceneExtensions = @('.unity')
$PrefabExtensions = @('.prefab')
$UnityDataExtensions = @(
    '.asset', '.mat', '.controller', '.overridecontroller',
    '.anim', '.mask', '.playable', '.rendertexture',
    '.spriteatlas', '.physicmaterial', '.physicsmaterial2d'
)
$ShaderUiExtensions = @(
    '.shader', '.shadergraph', '.subgraph', '.hlsl',
    '.cginc', '.compute', '.uxml', '.uss'
)
$ConfigExtensions = @(
    '.json', '.txt', '.md', '.xml', '.yaml', '.yml',
    '.inputactions', '.rsp', '.props', '.targets',
    '.toml', '.ini', '.cfg', '.csv', '.editorconfig'
)
$ModelExtensions = @(
    '.fbx', '.obj', '.blend', '.dae', '.3ds',
    '.glb', '.gltf', '.ply', '.stl'
)
$TextureExtensions = @(
    '.png', '.jpg', '.jpeg', '.tga', '.psd', '.exr',
    '.hdr', '.tif', '.tiff', '.bmp', '.gif', '.webp'
)
$AudioExtensions = @('.wav', '.mp3', '.ogg', '.aiff', '.aif', '.flac')
$VideoExtensions = @('.mp4', '.mov', '.webm', '.avi', '.mkv')
$PluginExtensions = @('.dll', '.so', '.dylib', '.aar', '.jar')
$FontExtensions = @('.ttf', '.otf')

function Get-AssetKind {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $relative = Get-RelativeProjectPath $File.FullName
    $ext = Get-ExtensionLower $File

    if ($relative.StartsWith('ProjectSettings/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'ProjectSetting'
    }

    if ($relative.StartsWith('Packages/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'PackageFile'
    }

    if ($ext -eq '.meta') { return 'Meta' }
    if ($ScriptExtensions -contains $ext) { return 'Script' }
    if ($SceneExtensions -contains $ext) { return 'Scene' }
    if ($PrefabExtensions -contains $ext) { return 'Prefab' }
    if ($ext -eq '.mat') { return 'Material' }
    if ($ext -eq '.asset') { return 'UnityAsset' }

    if (
        $ext -eq '.controller' -or
        $ext -eq '.overridecontroller' -or
        $ext -eq '.anim' -or
        $ext -eq '.mask'
    ) {
        return 'Animation'
    }

    if ($ShaderUiExtensions -contains $ext) { return 'ShaderOrUI' }
    if ($ModelExtensions -contains $ext) { return 'Model' }
    if ($TextureExtensions -contains $ext) { return 'Texture' }
    if ($AudioExtensions -contains $ext) { return 'Audio' }
    if ($VideoExtensions -contains $ext) { return 'Video' }
    if ($PluginExtensions -contains $ext) { return 'Plugin' }
    if ($FontExtensions -contains $ext) { return 'Font' }
    if ($ConfigExtensions -contains $ext) { return 'Config' }
    if ($UnityDataExtensions -contains $ext) { return 'UnityData' }

    return 'Other'
}

function Get-EmbedGroup {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $relative = Get-RelativeProjectPath $File.FullName
    $ext = Get-ExtensionLower $File

    # Project settings are always high-value.
    if ($relative.StartsWith('ProjectSettings/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'ProjectSettings'
    }

    # Only root package manifests; do not dump package source trees.
    if ($relative.Equals('Packages/manifest.json', [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.Equals('Packages/packages-lock.json', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'Packages'
    }

    # Everything else must be first-party.
    if (-not (Test-IsFirstParty $File)) {
        return $null
    }

    if ($ScriptExtensions -contains $ext) { return 'Scripts' }
    if ($SceneExtensions -contains $ext) { return 'Scenes' }
    if ($PrefabExtensions -contains $ext) { return 'Prefabs' }
    if ($UnityDataExtensions -contains $ext) { return 'UnityData' }
    if ($ShaderUiExtensions -contains $ext) { return 'ShadersUI' }
    if ($ConfigExtensions -contains $ext) { return 'Config' }

    return $null
}

# -----------------------------------------------------------------------------
# Scan metadata only
# -----------------------------------------------------------------------------

Write-Host '[1/9] Scanning project metadata...'

$fileMap = @{}
$scanRoots = @($AssetsPath, $ProjectSettingsPath)

if (Test-Path -LiteralPath $PackagesPath -PathType Container) {
    $scanRoots += $PackagesPath
}

foreach ($scanRoot in $scanRoots) {
    $found = Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Force -ErrorAction SilentlyContinue

    foreach ($file in $found) {
        if (Test-IsExcludedPath $file.FullName) {
            continue
        }

        $fileMap[$file.FullName.ToLowerInvariant()] = $file
    }
}

$AllFiles = @($fileMap.Values | Sort-Object FullName)
$AssetFiles = @($AllFiles | Where-Object { (Get-ExtensionLower $_) -ne '.meta' })

Write-Host ('       Files including .meta: ' + $AllFiles.Count)
Write-Host ('       Actual assets/files:    ' + $AssetFiles.Count)

# -----------------------------------------------------------------------------
# Compact inventory
# -----------------------------------------------------------------------------

Write-Host '[2/9] Writing compact inventories...'

$Inventory = New-Object System.Collections.Generic.List[string]
$Inventory.Add("Owner`tFirstParty`tKind`tExtension`tBytes`tRelativePath")

$PackStats = @{}

foreach ($file in $AssetFiles) {
    $relative = Get-RelativeProjectPath $file.FullName
    $owner = Get-AssetOwner $file
    $kind = Get-AssetKind $file
    $ext = Get-ExtensionLower $file
    $firstParty = Test-IsFirstParty $file

    $Inventory.Add(
        (Escape-Tsv $owner) + "`t" +
        $firstParty + "`t" +
        (Escape-Tsv $kind) + "`t" +
        (Escape-Tsv $ext) + "`t" +
        $file.Length + "`t" +
        (Escape-Tsv $relative)
    )

    $key = $owner + '|' + $kind
    if (-not $PackStats.ContainsKey($key)) {
        $PackStats[$key] = @{
            Owner = $owner
            Kind = $kind
            Count = 0
            Bytes = [long]0
        }
    }

    $PackStats[$key].Count++
    $PackStats[$key].Bytes += [long]$file.Length
}

Write-Utf8Lines (Join-Path $OutDir '_ASSET_INVENTORY.tsv') $Inventory

$PackSummary = New-Object System.Collections.Generic.List[string]
$PackSummary.Add("Owner`tKind`tCount`tBytes")

foreach ($row in ($PackStats.Values | Sort-Object Owner, Kind)) {
    $PackSummary.Add(
        (Escape-Tsv $row.Owner) + "`t" +
        (Escape-Tsv $row.Kind) + "`t" +
        $row.Count + "`t" +
        $row.Bytes
    )
}

Write-Utf8Lines (Join-Path $OutDir '_ASSET_PACK_SUMMARY.tsv') $PackSummary

# Dedicated compact catalogs make later searches cheap.
$catalogKinds = @('Scene', 'Prefab', 'Model', 'Texture', 'Audio', 'Material', 'UnityAsset')

foreach ($catalogKind in $catalogKinds) {
    $catalog = New-Object System.Collections.Generic.List[string]
    $catalog.Add("Owner`tBytes`tRelativePath")

    foreach ($file in ($AssetFiles | Where-Object { (Get-AssetKind $_) -eq $catalogKind })) {
        $catalog.Add(
            (Escape-Tsv (Get-AssetOwner $file)) + "`t" +
            $file.Length + "`t" +
            (Escape-Tsv (Get-RelativeProjectPath $file.FullName))
        )
    }

    Write-Utf8Lines (Join-Path $OutDir ('_CATALOG_' + $catalogKind.ToUpperInvariant() + '.tsv')) $catalog
}

# -----------------------------------------------------------------------------
# GUID map
# -----------------------------------------------------------------------------

Write-Host '[3/9] Building GUID map...'

$GuidLines = New-Object System.Collections.Generic.List[string]
$GuidLines.Add("Guid`tOwner`tAssetPath")
$AssetPathByGuid = @{}

$metaFiles = @($AllFiles | Where-Object { (Get-ExtensionLower $_) -eq '.meta' })

foreach ($meta in $metaFiles) {
    try {
        if ($meta.Length -gt 2097152) {
            continue
        }

        $content = [System.IO.File]::ReadAllText($meta.FullName)
        $m = [System.Text.RegularExpressions.Regex]::Match(
            $content,
            '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$'
        )

        if (-not $m.Success) {
            continue
        }

        $guid = $m.Groups[1].Value.ToLowerInvariant()
        $metaRelative = Get-RelativeProjectPath $meta.FullName

        if ($metaRelative.EndsWith('.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
            $assetRelative = $metaRelative.Substring(0, $metaRelative.Length - 5)
        }
        else {
            $assetRelative = $metaRelative
        }

        # Derive owner from the asset path without needing a FileInfo.
        $owner = '<ROOT>'
        if ($assetRelative.StartsWith('Assets/Assets/', [System.StringComparison]::OrdinalIgnoreCase) -or
            $assetRelative.StartsWith('Assets/YourQuest', [System.StringComparison]::OrdinalIgnoreCase)) {
            $owner = 'YourQuest'
        }
        elseif ($assetRelative.StartsWith('Assets/', [System.StringComparison]::OrdinalIgnoreCase)) {
            $parts = $assetRelative.Split('/')
            if ($parts.Count -ge 2) {
                $owner = $parts[1]
            }
        }
        elseif ($assetRelative.StartsWith('Packages/', [System.StringComparison]::OrdinalIgnoreCase)) {
            $owner = 'Packages'
        }

        $AssetPathByGuid[$guid] = $assetRelative
        $GuidLines.Add(
            $guid + "`t" +
            (Escape-Tsv $owner) + "`t" +
            (Escape-Tsv $assetRelative)
        )
    }
    catch {
        Write-Warning ('Meta read failed: ' + $meta.FullName)
    }
}

Write-Utf8Lines (Join-Path $OutDir '_GUID_MAP.tsv') $GuidLines

# -----------------------------------------------------------------------------
# Script index: all scripts, full contents only for YourQuest
# -----------------------------------------------------------------------------

Write-Host '[4/9] Building script index...'

$ScriptIndex = New-Object System.Collections.Generic.List[string]
$ScriptIndex.Add("Owner`tFirstParty`tBytes`tLines`tNamespace`tTypes`tRelativePath")

$allScripts = @($AssetFiles | Where-Object { (Get-ExtensionLower $_) -eq '.cs' })

foreach ($file in $allScripts) {
    $relative = Get-RelativeProjectPath $file.FullName
    $owner = Get-AssetOwner $file
    $firstParty = Test-IsFirstParty $file
    $lineCount = 0
    $namespace = ''
    $types = ''

    try {
        # C# source is small enough to index even for third-party packages.
        # We do not embed third-party source in TOP files.
        $content = [System.IO.File]::ReadAllText($file.FullName)

        if ($content.Length -gt 0) {
            $lineCount = [System.Text.RegularExpressions.Regex]::Matches($content, "`n").Count + 1
        }

        $ns = [System.Text.RegularExpressions.Regex]::Match(
            $content,
            '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)'
        )
        if ($ns.Success) {
            $namespace = $ns.Groups[1].Value
        }

        $typeMatches = [System.Text.RegularExpressions.Regex]::Matches(
            $content,
            '(?m)^\s*(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|unsafe)\s+)*(?:class|struct|interface|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)'
        )

        $typeNames = New-Object System.Collections.Generic.List[string]
        foreach ($tm in $typeMatches) {
            $name = $tm.Groups[1].Value
            if (-not [string]::IsNullOrWhiteSpace($name) -and -not $typeNames.Contains($name)) {
                $typeNames.Add($name)
            }
        }

        $types = [string]::Join(', ', $typeNames)
    }
    catch {
        $types = '<READ_ERROR>'
    }

    $ScriptIndex.Add(
        (Escape-Tsv $owner) + "`t" +
        $firstParty + "`t" +
        $file.Length + "`t" +
        $lineCount + "`t" +
        (Escape-Tsv $namespace) + "`t" +
        (Escape-Tsv $types) + "`t" +
        (Escape-Tsv $relative)
    )
}

Write-Utf8Lines (Join-Path $OutDir '_SCRIPT_INDEX.tsv') $ScriptIndex

# -----------------------------------------------------------------------------
# First-party Unity wiring summary only
# -----------------------------------------------------------------------------

Write-Host '[5/9] Inspecting first-party serialized Unity wiring...'

$UnityRefs = New-Object System.Collections.Generic.List[string]
$UnityRefs.Add("ReferenceFile`tScriptGuid`tCount`tResolvedPath`tStatus")

$UnityObjects = New-Object System.Collections.Generic.List[string]
$UnityObjects.Add("RelativePath`tKind`tGameObjects`tMonoBehaviours`tPrefabInstances`tScriptRefs`tUnresolved")

$inspectable = @('.unity', '.prefab', '.asset', '.controller', '.overridecontroller')

$firstPartySerialized = @(
    $AssetFiles |
        Where-Object {
            (Test-IsFirstParty $_) -and
            ($inspectable -contains (Get-ExtensionLower $_))
        }
)

foreach ($file in $firstPartySerialized) {
    $relative = Get-RelativeProjectPath $file.FullName
    $kind = Get-AssetKind $file

    if ($file.Length -gt $MaxEmbeddedFileBytes) {
        $UnityObjects.Add(
            (Escape-Tsv $relative) + "`t" +
            (Escape-Tsv $kind) + "`tSKIPPED_LARGE`t`t`t`t"
        )
        continue
    }

    try {
        $content = [System.IO.File]::ReadAllText($file.FullName)

        $goCount = [System.Text.RegularExpressions.Regex]::Matches(
            $content, '(?m)^--- !u!1 &'
        ).Count

        $mbCount = [System.Text.RegularExpressions.Regex]::Matches(
            $content, '(?m)^--- !u!114 &'
        ).Count

        $piCount = [System.Text.RegularExpressions.Regex]::Matches(
            $content, '(?m)^--- !u!1001 &'
        ).Count

        $scriptMatches = [System.Text.RegularExpressions.Regex]::Matches(
            $content,
            'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})'
        )

        $counts = @{}
        foreach ($sm in $scriptMatches) {
            $guid = $sm.Groups[1].Value.ToLowerInvariant()
            if (-not $counts.ContainsKey($guid)) {
                $counts[$guid] = 0
            }
            $counts[$guid]++
        }

        $unresolved = 0
        foreach ($guid in ($counts.Keys | Sort-Object)) {
            if ($AssetPathByGuid.ContainsKey($guid)) {
                $resolved = $AssetPathByGuid[$guid]
                $status = 'RESOLVED'
            }
            else {
                $resolved = ''
                $status = 'UNRESOLVED_IN_SCANNED_PROJECT'
                $unresolved++
            }

            $UnityRefs.Add(
                (Escape-Tsv $relative) + "`t" +
                $guid + "`t" +
                $counts[$guid] + "`t" +
                (Escape-Tsv $resolved) + "`t" +
                $status
            )
        }

        $UnityObjects.Add(
            (Escape-Tsv $relative) + "`t" +
            (Escape-Tsv $kind) + "`t" +
            $goCount + "`t" +
            $mbCount + "`t" +
            $piCount + "`t" +
            $scriptMatches.Count + "`t" +
            $unresolved
        )
    }
    catch {
        $UnityObjects.Add(
            (Escape-Tsv $relative) + "`t" +
            (Escape-Tsv $kind) + "`tREAD_ERROR`t`t`t`t"
        )
    }
}

Write-Utf8Lines (Join-Path $OutDir '_UNITY_SCRIPT_REFERENCES.tsv') $UnityRefs
Write-Utf8Lines (Join-Path $OutDir '_UNITY_OBJECT_SUMMARY.tsv') $UnityObjects

# -----------------------------------------------------------------------------
# Manifest + chunk writer
# -----------------------------------------------------------------------------

$Manifest = New-Object System.Collections.Generic.List[string]
$Manifest.Add('=== YOURQUEST LEAN PROJECT DUMP ===')
$Manifest.Add("Generated: $GeneratedAt")
$Manifest.Add("Root: $RootPath")
$Manifest.Add("Actual project files indexed: $($AssetFiles.Count)")
$Manifest.Add("First-party root: Assets/Assets/")
$Manifest.Add("Also first-party: Assets/YourQuest*")
$Manifest.Add("Third-party asset contents: INVENTORY ONLY")
$Manifest.Add('')
$Manifest.Add('STATIC INDEX FILES')
$Manifest.Add('  + _PROJECT_SUMMARY.txt')
$Manifest.Add('  + _ASSET_PACK_SUMMARY.tsv')
$Manifest.Add('  + _ASSET_INVENTORY.tsv')
$Manifest.Add('  + _SCRIPT_INDEX.tsv')
$Manifest.Add('  + _GUID_MAP.tsv')
$Manifest.Add('  + _UNITY_SCRIPT_REFERENCES.tsv')
$Manifest.Add('  + _UNITY_OBJECT_SUMMARY.tsv')
$Manifest.Add('  + _CATALOG_SCENE.tsv')
$Manifest.Add('  + _CATALOG_PREFAB.tsv')
$Manifest.Add('  + _CATALOG_MODEL.tsv')
$Manifest.Add('  + _CATALOG_TEXTURE.tsv')
$Manifest.Add('  + _CATALOG_AUDIO.tsv')
$Manifest.Add('  + _CATALOG_MATERIAL.tsv')
$Manifest.Add('  + _CATALOG_UNITYASSET.tsv')
$Manifest.Add('')

function New-TopState {
    param([string]$Group, [int]$Number)

    $idx = New-Object System.Collections.Generic.List[string]
    $body = New-Object System.Collections.Generic.List[string]
    $entries = New-Object System.Collections.Generic.List[string]

    $idx.Add('# INDEX')
    $idx.Add("# Group: $Group")
    $idx.Add("# Generated: $GeneratedAt")
    $idx.Add('# --------------------------------')

    return @{
        Number = $Number
        Chars = [long]0
        Entries = 0
        Index = $idx
        Body = $body
        ManifestEntries = $entries
    }
}

function Flush-TopState {
    param([string]$Prefix, [hashtable]$State)

    if ($State.Entries -le 0) {
        return
    }

    $name = ('{0}{1:000}.txt' -f $Prefix, $State.Number)
    $path = Join-Path $OutDir $name
    $all = New-Object System.Collections.Generic.List[string]

    foreach ($line in $State.Index) { $all.Add($line) }
    $all.Add('')
    foreach ($line in $State.Body) { $all.Add($line) }

    Write-Utf8Lines $path $all

    $Manifest.Add("OUT: $name")
    foreach ($entry in $State.ManifestEntries) {
        $Manifest.Add("  + $entry")
    }
}

function Add-Slice {
    param(
        [hashtable]$State,
        [System.IO.FileInfo]$File,
        [string]$DisplayPath,
        [string]$Slice
    )

    $State.Index.Add("# - $DisplayPath")
    $State.Body.Add('')
    $State.Body.Add("===== $DisplayPath =====")
    $State.Body.Add("# Bytes: $($File.Length)")
    $State.Body.Add('')

    if ($Slice.Length -gt 0) {
        foreach ($line in [System.Text.RegularExpressions.Regex]::Split($Slice, "`r`n|`n|`r")) {
            $State.Body.Add($line)
        }
    }

    $State.Body.Add('')
    $State.ManifestEntries.Add($DisplayPath)
    $State.Entries++
    $State.Chars += [Math]::Max($Slice.Length, 1)
    $script:TotalEmbeddedChars += [Math]::Max($Slice.Length, 1)
}

function Export-Group {
    param([string]$Group, [string]$Prefix, [System.IO.FileInfo[]]$Files)

    $Manifest.Add("=== TOP: $Group ===")

    if ($null -eq $Files -or $Files.Count -eq 0) {
        $Manifest.Add('  <none>')
        $Manifest.Add('')
        return
    }

    $state = New-TopState $Group 1

    foreach ($file in $Files) {
        $relative = Get-RelativeProjectPath $file.FullName

        if ($script:TotalEmbeddedChars -ge $MaxTotalEmbeddedChars) {
            $Manifest.Add("  ! GLOBAL EMBED BUDGET REACHED: $relative")
            continue
        }

        if ($file.Length -gt $MaxEmbeddedFileBytes) {
            $Manifest.Add("  ! INDEX ONLY, LARGE FILE: $relative ($($file.Length) bytes)")
            continue
        }

        try {
            $content = [System.IO.File]::ReadAllText($file.FullName)
        }
        catch {
            $Manifest.Add("  ! READ ERROR: $relative")
            continue
        }

        if ($null -eq $content) {
            $content = ''
        }

        $offset = 0
        $part = 1

        if ($content.Length -eq 0) {
            if ($state.Entries -ge $MaxEntriesPerTop -or $state.Chars -ge $MaxCharsPerTop) {
                Flush-TopState $Prefix $state
                $state = New-TopState $Group ($state.Number + 1)
            }
            Add-Slice $state $file $relative ''
            continue
        }

        while ($offset -lt $content.Length) {
            if ($state.Entries -ge $MaxEntriesPerTop -or $state.Chars -ge $MaxCharsPerTop) {
                Flush-TopState $Prefix $state
                $state = New-TopState $Group ($state.Number + 1)
            }

            $budgetRemaining = $MaxTotalEmbeddedChars - $script:TotalEmbeddedChars
            if ($budgetRemaining -le 0) {
                break
            }

            $available = $MaxCharsPerTop - $state.Chars
            if ($available -lt 4096 -and $state.Entries -gt 0) {
                Flush-TopState $Prefix $state
                $state = New-TopState $Group ($state.Number + 1)
                $available = $MaxCharsPerTop
            }

            $remaining = $content.Length - $offset
            $take = [Math]::Min([long]$available, [long]$remaining)
            $take = [Math]::Min([long]$take, [long]$budgetRemaining)

            if ($take -le 0) {
                break
            }

            $takeInt = [int]$take

            if ($takeInt -lt $remaining -and $takeInt -gt 1024) {
                $probe = $content.Substring($offset, $takeInt)
                $lastLf = $probe.LastIndexOf("`n")
                $minSplit = [int]($takeInt * 0.65)
                if ($lastLf -ge $minSplit) {
                    $takeInt = $lastLf + 1
                }
            }

            $slice = $content.Substring($offset, $takeInt)

            if ($offset -gt 0 -or $takeInt -lt $content.Length) {
                $display = "$relative (PART $part)"
            }
            else {
                $display = $relative
            }

            Add-Slice $state $file $display $slice
            $offset += $takeInt
            $part++
        }
    }

    Flush-TopState $Prefix $state
    $Manifest.Add('')
}

# -----------------------------------------------------------------------------
# Export only high-value text
# -----------------------------------------------------------------------------

Write-Host '[6/9] Exporting high-value text only...'

$groups = @(
    @{ Name='Scripts';         Prefix='TOP_Scripts_' },
    @{ Name='Scenes';          Prefix='TOP_Scenes_' },
    @{ Name='Prefabs';         Prefix='TOP_Prefabs_' },
    @{ Name='UnityData';       Prefix='TOP_UnityData_' },
    @{ Name='ShadersUI';       Prefix='TOP_ShadersUI_' },
    @{ Name='Config';          Prefix='TOP_Config_' },
    @{ Name='ProjectSettings'; Prefix='TOP_ProjectSettings_' },
    @{ Name='Packages';        Prefix='TOP_Packages_' }
)

foreach ($g in $groups) {
    $name = $g.Name

    $files = @(
        $AssetFiles |
            Where-Object { (Get-EmbedGroup $_) -eq $name } |
            Sort-Object FullName
    )

    Write-Host ('       ' + $name.PadRight(18) + $files.Count)
    Export-Group $name $g.Prefix $files
}

# -----------------------------------------------------------------------------
# Summary / README / manifest
# -----------------------------------------------------------------------------

Write-Host '[7/9] Writing summary...'

$Summary = New-Object System.Collections.Generic.List[string]
$Summary.Add('# YOURQUEST LEAN PROJECT SUMMARY')
$Summary.Add("# Generated: $GeneratedAt")
$Summary.Add("# Root: $RootPath")
$Summary.Add('')
$Summary.Add('POLICY')
$Summary.Add('Full contents: YourQuest-owned files under Assets/Assets/ and Assets/YourQuest*.')
$Summary.Add('Full contents: ProjectSettings and Packages/manifest.json + packages-lock.json.')
$Summary.Add('Inventory only: marketplace/store/third-party assets, demo scenes, prefabs, models, textures, audio.')
$Summary.Add('Excluded entirely: Library, Temp, Logs, obj, bin, .git, .vs, UserSettings, builds and dump output.')
$Summary.Add('')
$Summary.Add("Actual files indexed: $($AssetFiles.Count)")
$Summary.Add("C# scripts indexed: $($allScripts.Count)")
$Summary.Add("First-party serialized files inspected: $($firstPartySerialized.Count)")
$Summary.Add("Embedded chars: $script:TotalEmbeddedChars")
$Summary.Add("Embedded chars hard limit: $MaxTotalEmbeddedChars")
$Summary.Add('')
$Summary.Add('RECOMMENDED CHATGPT REVIEW ORDER')
$Summary.Add('1. _PROJECT_SUMMARY.txt')
$Summary.Add('2. _MANIFEST.txt')
$Summary.Add('3. _SCRIPT_INDEX.tsv')
$Summary.Add('4. TOP_Scripts_*.txt')
$Summary.Add('5. _UNITY_OBJECT_SUMMARY.tsv + _UNITY_SCRIPT_REFERENCES.tsv')
$Summary.Add('6. TOP_Scenes_*.txt')
$Summary.Add('7. TOP_ProjectSettings_*.txt')
$Summary.Add('8. Asset catalogs only when choosing/locating third-party content')
$Summary.Add('')
$Summary.Add('THIRD-PARTY ASSET DISCOVERY')
$Summary.Add('_ASSET_PACK_SUMMARY.tsv gives counts/sizes by top-level asset pack.')
$Summary.Add('_CATALOG_PREFAB.tsv lists every prefab by name/path without dumping YAML.')
$Summary.Add('_CATALOG_MODEL.tsv, _CATALOG_TEXTURE.tsv, _CATALOG_AUDIO.tsv, etc. do the same.')
$Summary.Add('If a specific third-party prefab/script later matters, export/read that single dependency separately.')

Write-Utf8Lines (Join-Path $OutDir '_PROJECT_SUMMARY.txt') $Summary
Write-Utf8Lines (Join-Path $OutDir '_MANIFEST.txt') $Manifest

$Readme = @(
    '# YourQuest LEAN ChatGPT Dump',
    '',
    'Upload the ZIP or the files in this folder.',
    '',
    'This version intentionally does NOT dump marketplace/demo content wholesale.',
    'Third-party assets remain discoverable through catalogs and GUID/path indexes.',
    'YourQuest-owned implementation and wiring remain available as full text.',
    '',
    'This is designed for targeted architecture/debugging work without consuming context on unrelated asset packs.'
)

Write-Utf8Lines (Join-Path $OutDir 'README_FIRST.txt') $Readme

# -----------------------------------------------------------------------------
# Zip
# -----------------------------------------------------------------------------

Write-Host '[8/9] Creating ZIP...'

if (-not $NoZip) {
    try {
        Compress-Archive `
            -Path (Join-Path $OutDir '*') `
            -DestinationPath $ZipPath `
            -CompressionLevel Optimal `
            -Force

        Write-Host ('       ZIP: ' + $ZipPath)
    }
    catch {
        Write-Warning ('ZIP creation failed: ' + $_.Exception.Message)
    }
}

Write-Host '[9/9] Complete.'
Write-Host ''
Write-Host '============================================================'
Write-Host ' LEAN PROJECT DUMP COMPLETE'
Write-Host '============================================================'
Write-Host ('Indexed files:  ' + $AssetFiles.Count)
Write-Host ('Embedded chars: ' + $script:TotalEmbeddedChars)
Write-Host ('Output:         ' + $OutDir)

if (-not $NoZip -and (Test-Path -LiteralPath $ZipPath)) {
    Write-Host ('ZIP:            ' + $ZipPath)
}
Write-Host ''
