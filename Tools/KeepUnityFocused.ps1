param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class UnityKeepAwakeNative
{
    // note: Keep Windows awake and the display active while the Unity verification run is in progress.
    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint flags);

    // note: Bring the already-running Unity editor window to the foreground so editor recompiles and play-mode transitions are not backgrounded.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr windowHandle);

    // note: Restore a minimized Unity editor before foregrounding it.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr windowHandle, int command);
}
'@

$executionStateContinuous = [Convert]::ToUInt32('80000000', 16)
$executionStateSystemRequired = [uint32]0x00000001
$executionStateDisplayRequired = [uint32]0x00000002
$executionState = $executionStateContinuous -bor $executionStateSystemRequired -bor $executionStateDisplayRequired
$stopFile = Join-Path $ProjectPath 'Temp\STOP_KEEP_UNITY_FOCUSED'

try
{
    while (-not (Test-Path -LiteralPath $stopFile))
    {
        # note: Refresh the Windows execution-state lease on every pass so long generation never allows sleep or display power-down.
        [UnityKeepAwakeNative]::SetThreadExecutionState($executionState) | Out-Null

        # note: Select the existing Unity editor only; this helper never launches a second editor or changes project files.
        $unity = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 } |
            Select-Object -First 1
        if ($null -ne $unity)
        {
            # note: Keep the active editor visible and focused for asset refresh, script reload, and play-mode verification.
            [UnityKeepAwakeNative]::ShowWindow($unity.MainWindowHandle, 5) | Out-Null
            [UnityKeepAwakeNative]::SetForegroundWindow($unity.MainWindowHandle) | Out-Null
        }
        # note: Leave a small local heartbeat so the verification harness can confirm the helper is alive without touching Unity state.
        Set-Content -LiteralPath (Join-Path $ProjectPath 'Temp\KeepUnityFocused.active') -Value (Get-Date -Format o)
        Start-Sleep -Seconds 3
    }
}
finally
{
    # note: Release the temporary keep-awake lease as soon as the stop sentinel is created.
    [UnityKeepAwakeNative]::SetThreadExecutionState($executionStateContinuous) | Out-Null
    Remove-Item -LiteralPath (Join-Path $ProjectPath 'Temp\KeepUnityFocused.active') -Force -ErrorAction SilentlyContinue
}
