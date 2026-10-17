param([string]$OutputDirectory = 'outputs/LlmLatencyImplementation_20261007/Compile')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$compileFolder = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (!$compileFolder.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must remain inside the project.' }
New-Item -ItemType Directory -Path $compileFolder -ErrorAction Stop | Out-Null
# note: Compile production sources against installed Unity references without entering the Editor or editing its generated project/cache files.
$sdkVersion = (& dotnet --version).Trim()
$compiler = Join-Path 'C:/Program Files/dotnet/sdk' "$sdkVersion/Roslyn/bincore/csc.dll"
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'Assembly-CSharp.csproj') -Raw
$sources = @($project.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $projectRoot $_.Include)) })
$sources += @('Assets/Assets/Scripts/Generated/YQItemAuthoringPolicy.cs','Assets/Assets/Scripts/LLM/YQLlmResidencyPolicy.cs') | ForEach-Object { Join-Path $projectRoot $_ }
$sources = @($sources | Sort-Object -Unique)
$references = @($project.Project.ItemGroup.Reference | Where-Object { $_.HintPath } | ForEach-Object {
    if ([IO.Path]::IsPathRooted($_.HintPath)) { [IO.Path]::GetFullPath($_.HintPath) } else { [IO.Path]::GetFullPath((Join-Path $projectRoot $_.HintPath)) }
})
$references += @($project.Project.ItemGroup.ProjectReference | Where-Object { $_.Include } | ForEach-Object { Join-Path $projectRoot ('Library/ScriptAssemblies/' + [IO.Path]::GetFileNameWithoutExtension($_.Include) + '.dll') })
$defines = @($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ })[0]
$response = @('/target:library','/langversion:9.0','/nologo','/nostdlib+','/unsafe+',"/define:$defines",('/out:"' + (Join-Path $compileFolder 'Assembly-CSharp.dll') + '"'))
$response += $references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' }
$response += $sources | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $compileFolder 'Assembly-CSharp.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
$compilerOutput = @(& dotnet $compiler "@$responsePath" 2>&1)
$compileExit = $LASTEXITCODE
$compilerOutput | Set-Content -LiteralPath (Join-Path $compileFolder 'Assembly-CSharp.log')
$hashes = @($sources | ForEach-Object { Get-FileHash -Algorithm SHA256 -LiteralPath $_ | Select-Object Path,Hash })
$report = [pscustomobject]@{ utc=[DateTime]::UtcNow.ToString('O'); exitCode=$compileExit; sourceCount=$sources.Count; sourceHashes=$hashes; evidence='Detached production runtime compile; no Unity import or gameplay claim' }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $compileFolder 'Receipt.json')
$compilerOutput | Select-Object -Last 20
Write-Output "Compile exit=$compileExit; sources=$($sources.Count); output=$compileFolder"
exit $compileExit
