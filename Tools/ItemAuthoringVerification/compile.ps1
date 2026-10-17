param([string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference = 'Stop'
# note: Compile actual runtime sources against installed Unity references into a disposable folder; do not edit Unity caches or take Editor ownership.
$compileFolder = Join-Path $ProjectRoot ('outputs/ItemAuthoring_20261007/compile-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $compileFolder | Out-Null
$sdkVersion = (& dotnet --version).Trim()
$compiler = Join-Path 'C:/Program Files/dotnet/sdk' "$sdkVersion/Roslyn/bincore/csc.dll"
[xml]$project = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Assembly-CSharp.csproj') -Raw
$sources = @($project.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $ProjectRoot $_.Include)) })
$sources += Join-Path $ProjectRoot 'Assets/Assets/Scripts/Generated/YQItemAuthoringPolicy.cs'
$sources = @($sources | Sort-Object -Unique)
$references = @($project.Project.ItemGroup.Reference | Where-Object { $_.HintPath } | ForEach-Object {
    if([IO.Path]::IsPathRooted($_.HintPath)) { [IO.Path]::GetFullPath($_.HintPath) }
    else { [IO.Path]::GetFullPath((Join-Path $ProjectRoot $_.HintPath)) }
})
$references += @($project.Project.ItemGroup.ProjectReference | Where-Object { $_.Include } | ForEach-Object {
    Join-Path $ProjectRoot ('Library/ScriptAssemblies/' + [IO.Path]::GetFileNameWithoutExtension($_.Include) + '.dll')
})
$defines = @($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ })[0]
$response = @('/target:library','/langversion:9.0','/nologo','/nostdlib+','/unsafe+',"/define:$defines",('/out:"' + (Join-Path $compileFolder 'Assembly-CSharp.dll') + '"'))
$response += $references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' }
$response += $sources | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $compileFolder 'Assembly-CSharp.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
$compilerOutput = @(& dotnet $compiler "@$responsePath" 2>&1)
$compileExit = $LASTEXITCODE
$compilerOutput | Set-Content -LiteralPath (Join-Path $compileFolder 'Assembly-CSharp.log')
[IO.File]::WriteAllLines((Join-Path $compileFolder 'references.txt'), $references)
$trackedSources = @('Generated/GeneratedRpgContentService.cs','Generated/YQItemAuthoringPolicy.cs','Generated/YQOriginGenerationService.cs','LLM/YQLlmRequest.cs','LLM/YQBoundedRepair.cs')
$hashes = @($trackedSources | ForEach-Object { Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $ProjectRoot "Assets/Assets/Scripts/$_") | Select-Object Path,Hash })
$reportFolder = Join-Path $ProjectRoot 'outputs/ItemAuthoring_20261007'
New-Item -ItemType Directory -Force -Path $reportFolder | Out-Null
$report = [pscustomobject]@{generatedAtUtc=[DateTime]::UtcNow.ToString('O'); status=$(if($compileExit -eq 0){'PASS'}else{'FAIL'}); sourceCount=$sources.Count;
    evidence='Fresh detached runtime C# compile against actual Unity/project references; no Unity import, live model or gameplay claim';
    outputFolder=$compileFolder; log=(Join-Path $compileFolder 'Assembly-CSharp.log'); sources=$hashes}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportFolder 'CompileReceipt.json')
$report | Select-Object status,sourceCount,outputFolder,log | ConvertTo-Json -Compress
if($compileExit -ne 0) { $compilerOutput | Select-Object -Last 20; exit 1 }
