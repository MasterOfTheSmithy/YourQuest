param(
    [string]$ServerPath = "C:\Ai\llama.cpp\llama-server.exe",
    [string]$ModelPath = "C:\Ai\Text Models\Qwen3.5-4B-Q4_K_M.gguf",
    [int]$Port = 11435,
    [int]$ContextTokens = 16384,
    [int]$GpuHeadroomMb = 3072
)

# note: This launcher starts the same persistent text-only llama.cpp server that Unity is configured to use.
$ErrorActionPreference = "Stop"

# note: Validate paths up front so failures are readable instead of becoming a silent Unity timeout.
if (-not (Test-Path -LiteralPath $ServerPath)) {
    throw "llama-server.exe was not found: $ServerPath"
}

if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "GGUF model was not found: $ModelPath"
}

# note: Keep the server on localhost, one slot, 16K context, prompt cache enabled, and roughly 3 GB GPU headroom.
function Quote-Arg([string]$Value) {
    if ($Value -match '\s') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

# note: Start-Process joins argument arrays loosely, so paths with spaces must be quoted before launch.
$arguments = @(
    "--model", (Quote-Arg $ModelPath),
    "--host", "127.0.0.1",
    "--port", "$Port",
    "--ctx-size", "$ContextTokens",
    "--parallel", "1",
    "--flash-attn", "on",
    "--no-kv-offload",
    "--fit", "on",
    "--fit-target", "$GpuHeadroomMb",
    "--no-webui",
    "--reasoning", "off"
) -join " "

# note: Start from the llama.cpp folder so the small executable can find its DLLs.
$workingDirectory = Split-Path -Parent $ServerPath
Start-Process -FilePath $ServerPath -ArgumentList $arguments -WorkingDirectory $workingDirectory -WindowStyle Hidden

Write-Host "YourQuest llama.cpp server starting on http://127.0.0.1:$Port"
