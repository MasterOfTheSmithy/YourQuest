param([string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference = 'Stop'
# note: Compile the real Unity project sources/references into a disposable folder, without editing Unity caches or interrupting another Editor task.
$compileFolder = Join-Path ([IO.Path]::GetTempPath()) ('YourQuestContainerCompile-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $compileFolder | Out-Null
$sdkVersion = (& dotnet --version).Trim()
$compiler = Join-Path 'C:/Program Files/dotnet/sdk' "$sdkVersion/Roslyn/bincore/csc.dll"
$extraRuntime = @('Data/State/YQContainerInventory.cs','Generated/YQContainerLoot.cs','Tutorial/YQWorldContainer.cs','Tutorial/YQContainerUI.cs','Tutorial/YQDeveloperContainerCommands.cs')
$results = @()
foreach($assembly in @('Assembly-CSharp','Assembly-CSharp-Editor')) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $ProjectRoot "$assembly.csproj") -Raw
    $sources = @($project.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $ProjectRoot $_.Include)) })
    if($assembly -eq 'Assembly-CSharp') { $sources += $extraRuntime | ForEach-Object { Join-Path $ProjectRoot "Assets/Assets/Scripts/$_" } }
    else { $sources += Join-Path $ProjectRoot 'Assets/Assets/Scripts/Generated/Editor/YQContainerInventoryTests.cs' }
    $sources = @($sources | Sort-Object -Unique)
    $references = @($project.Project.ItemGroup.Reference | Where-Object { $_.HintPath } | ForEach-Object {
        $reference = if([IO.Path]::IsPathRooted($_.HintPath)) { [IO.Path]::GetFullPath($_.HintPath) } else { [IO.Path]::GetFullPath((Join-Path $ProjectRoot $_.HintPath)) }
        if($assembly -eq 'Assembly-CSharp-Editor' -and [IO.Path]::GetFileName($reference) -eq 'Assembly-CSharp.dll') { Join-Path $compileFolder 'Assembly-CSharp.dll' } else { $reference }
    })
    $references += @($project.Project.ItemGroup.ProjectReference | Where-Object { $_.Include } | ForEach-Object {
        $referenceName = [IO.Path]::GetFileNameWithoutExtension($_.Include)
        if($referenceName -eq 'Assembly-CSharp') { Join-Path $compileFolder 'Assembly-CSharp.dll' }
        else { Join-Path $ProjectRoot "Library/ScriptAssemblies/$referenceName.dll" }
    })
    $defines = @($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ })[0]
    $response = @('/target:library','/langversion:9.0','/nologo','/nostdlib+','/unsafe+',"/define:$defines",('/out:"' + (Join-Path $compileFolder "$assembly.dll") + '"'))
    $response += $references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' }
    $response += $sources | ForEach-Object { '"' + $_ + '"' }
    $responsePath = Join-Path $compileFolder "$assembly.rsp"
    [IO.File]::WriteAllLines($responsePath, $response)
    $compilerOutput = @(& dotnet $compiler "@$responsePath" 2>&1)
    $exitCode = $LASTEXITCODE
    $compilerOutput | Set-Content -LiteralPath (Join-Path $compileFolder "$assembly.log")
    $results += [pscustomobject]@{ assembly = $assembly; status = $(if($exitCode -eq 0) {'PASS'} else {'FAIL'}); sourceCount = $sources.Count; exitCode = $exitCode; log = (Join-Path $compileFolder "$assembly.log") }
    Write-Output "$assembly $(if($exitCode -eq 0) {'PASS'} else {'FAIL'}) ($($sources.Count) actual sources)"
    if($exitCode -ne 0) { $compilerOutput | Select-Object -Last 25; break }
}
$report = [pscustomobject]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); evidence = 'Fresh C# compile against installed Unity/project references; no production runtime claim'; results = $results; outputFolder = $compileFolder }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ProjectRoot 'Docs/Container_Inventory_Compile_Receipt_2026-10-02.json')
if($results.Count -ne 2 -or @($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
