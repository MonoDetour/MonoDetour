using MonoDetour.Logging;

namespace MonoDetour.Interop.HarmonyX;

/// <summary>
/// HarmonyX interop support for MonoDetour.
/// </summary>
public static class HarmonyXInterop
{
    internal const string ManagerName = "com.github.MonoDetour.Interop.HarmonyX";
    static bool initialized;
    internal static bool anyFailed;

    /// <summary>
    /// Initialize and apply HarmonyX interop for MonoDetour.
    /// </summary>
    public static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        TrackInstructions.Init();

        if (anyFailed)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "HarmonyX interop module has completely failed to initialize."
            );
            return;
        }

        TrackPatches.Init();

        if (anyFailed)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "HarmonyX interop module has partly failed to initialize."
            );
            return;
        }
    }

    internal static void Dispose()
    {
        TrackInstructions.instructionManager.DisposeHooks();
        TrackPatches.patchManager.DisposeHooks();
        initialized = false;
    }
}
