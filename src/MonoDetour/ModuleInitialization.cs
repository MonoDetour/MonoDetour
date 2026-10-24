using MonoDetour.Interop.Cecil;
using MonoDetour.Interop.RuntimeDetour;

namespace MonoDetour;

internal static class ModuleInitialization
{
    static bool isInitialized;

    /// <summary>
    /// Applies MonoDetour's internal patches if not applied yet
    /// to allow MonoDetour to function properly.
    /// </summary>
    internal static void InitializeModule()
    {
        if (isInitialized)
        {
            return;
        }
        isInitialized = true;

        ILHookInstructionILLabelCastFixes.InitHook();
        ILHookDMDManipulation.InitHook();
        LegacyILHookAntiExploder.InitHook();
    }
}
