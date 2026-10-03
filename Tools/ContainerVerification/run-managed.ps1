param([string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference = 'Stop'
# note: Use the fresh compilation receipt; outputs and fixture saves stay in an isolated temporary folder.
$compile = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Docs/Container_Inventory_Compile_Receipt_2026-10-02.json') -Raw | ConvertFrom-Json
if(@($compile.results | Where-Object status -ne 'PASS').Count -gt 0) { throw 'Compile the implementation first.' }
$testFolder = Join-Path ([IO.Path]::GetTempPath()) ('YourQuestContainerManaged-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder | Out-Null
[xml]$unityProject = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Assembly-CSharp.csproj') -Raw
$referencePaths = @($unityProject.Project.ItemGroup.Reference | Where-Object { $_.HintPath } | ForEach-Object {
    if([IO.Path]::IsPathRooted($_.HintPath)) { $_.HintPath } else { Join-Path $ProjectRoot $_.HintPath }
})
$requiredReferences = @((Join-Path $compile.outputFolder 'Assembly-CSharp.dll')) + @($referencePaths | Where-Object { [IO.Path]::GetFileName($_) -in @('UnityEngine.CoreModule.dll','Unity.Newtonsoft.Json.dll','Newtonsoft.Json.dll') })
$sdkVersion = (& dotnet --version).Trim()
$compiler = Join-Path 'C:/Program Files/dotnet/sdk' "$sdkVersion/Roslyn/bincore/csc.dll"
$framework = Get-ChildItem -LiteralPath 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref' -Directory | Where-Object Name -like '10.*' | Sort-Object Name -Descending | Select-Object -First 1
$arguments = @('/nologo','/target:exe','/langversion:latest',('/out:"' + (Join-Path $testFolder 'Contracts.dll') + '"'))
$arguments += Get-ChildItem -LiteralPath (Join-Path $framework.FullName 'ref/net10.0') -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
foreach($reference in $requiredReferences) { $arguments += '/reference:"' + $reference + '"'; Copy-Item -LiteralPath $reference -Destination $testFolder }
# note: Unity value-type metadata references its managed attribute modules even though no native engine methods are called by these checks.
foreach($reference in $referencePaths | Where-Object { [IO.Path]::GetFileName($_) -like 'UnityEngine.*.dll' }) { Copy-Item -LiteralPath $reference -Destination $testFolder }
$arguments += '"' + (Join-Path $PSScriptRoot 'HeadlessContracts.cs') + '"'
$responsePath = Join-Path $testFolder 'Contracts.rsp'; [IO.File]::WriteAllLines($responsePath, $arguments)
& dotnet $compiler "@$responsePath"
if($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
[IO.File]::WriteAllText((Join-Path $testFolder 'Contracts.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},"rollForward":"LatestPatch"}}')
& dotnet (Join-Path $testFolder 'Contracts.dll') $ProjectRoot
exit $LASTEXITCODE
