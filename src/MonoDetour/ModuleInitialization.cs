using System;
using System.Runtime.CompilerServices;
using MonoDetour.Interop.Cecil;
using MonoDetour.Interop.RuntimeDetour;
using MonoDetour.Logging;

namespace MonoDetour;

internal static class ModuleInitialization
{
    static bool isInitialized;

    // I don't understand why I should not use a ModuleInitializer
    // and apparently it'd kinda be like a static constructor
    // so changing to those wouldn't really change much.
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void InitializeModule()
    {
        var manualInit = Environment.GetEnvironmentVariable("MONODETOUR_MANUAL_INIT");
        if (manualInit == "1")
        {
            return;
        }

        Initialize();
    }

    internal static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }
        isInitialized = true;

        MonoDetourLogger.ParseEnvironmentVariables();
        ILHookInstructionILLabelCastFixes.InitHook();
        ILHookDMDManipulation.InitHook();
        LegacyILHookAntiExploder.InitHook();
    }
}
