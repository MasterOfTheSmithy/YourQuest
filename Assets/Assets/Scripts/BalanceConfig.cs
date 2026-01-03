// Assets/Assets/Scripts/BalanceConfig.cs

using UnityEngine;

/// <summary>
/// Compatibility shim.
/// Some older systems/scripts still reference BalanceConfig.
/// The real config is ProgressionBalanceConfig.
/// </summary>
[CreateAssetMenu(menuName = "YourQuest/Progression/Balance Config (Legacy Alias)")]
public class BalanceConfig : ProgressionBalanceConfig
{
    // Intentionally empty. Inherits everything from ProgressionBalanceConfig.
}
