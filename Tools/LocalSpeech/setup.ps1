param([string]$Python = "python")
$ErrorActionPreference = 'Stop'
$speechProject = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$speechRuntime = Join-Path $speechProject 'outputs/InitialModelStack'
$speechVenv = Join-Path $speechRuntime 'venv'
$speechPython = Join-Path $speechVenv 'Scripts/python.exe'
function Invoke-SpeechPython([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Speech setup failed with exit code $LASTEXITCODE" }
}
# note: Keep model installation explicit and separate from gameplay; runtime is CPU-only and offline.
if (!(Test-Path -LiteralPath $speechPython)) {
    Invoke-SpeechPython $Python @('-m', 'venv', $speechVenv)
}
Invoke-SpeechPython $speechPython @('-m', 'pip', 'install', '--upgrade', 'pip')
Invoke-SpeechPython $speechPython @('-m', 'pip', 'install', 'torch==2.13.0+cpu', '--index-url', 'https://download.pytorch.org/whl/cpu', '--no-deps')
Invoke-SpeechPython $speechPython @('-m', 'pip', 'install', '-r', (Join-Path $speechProject 'Tools/InitialModelStack/requirements-lock.txt'))
Invoke-SpeechPython $speechPython @((Join-Path $speechProject 'Tools/InitialModelStack/provision.py'))
