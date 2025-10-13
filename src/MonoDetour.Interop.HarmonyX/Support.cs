namespace MonoDetour.Interop.HarmonyX;

/// <summary>
/// HarmonyX interop support for MonoDetour.
/// </summary>
public static class Support
{
    internal static readonly MonoDetourManager manager = new(
        "com.github.MonoDetour.Interop.HarmonyX"
    );
    static bool initialized;

    /// <summary>
    /// Initialize and apply HarmonyX interop for MonoDetour.
    /// </summary>
    public static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        TrackInstructions.Init();
        TrackPatches.Init();
    }

    internal static void Dispose()
    {
        manager.DisposeHooks();
        initialized = false;
    }
}
