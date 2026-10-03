using UnityEngine;

// note: Build-time opt-in is required in addition to Development Build; public players have no console command path.
public static class YQDeveloperConsoleGate
{
    public static bool AllowsBuild(bool editor, bool development, bool optedIn) => editor || (development && optedIn);
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
            return true;
#else
            return false;
#endif
        }
    }

    public static bool BlocksPersistence
    {
        get
        {
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
            return YQDeveloperTestSession.Active && !YQDeveloperTestSession.Publishing;
#else
            return false;
#endif
        }
    }

    public const string SaveBlocked = "Developer test session: saves/profile changes are blocked. Use test restore, or test persist --confirm to publish deliberately.";
}
