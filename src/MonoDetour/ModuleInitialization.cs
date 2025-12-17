using System.Runtime.CompilerServices;
using MonoDetour.Interop.Cecil;
using MonoDetour.Interop.RuntimeDetour;

namespace MonoDetour;

file static class ModuleInitialization
{
    // I don't understand why I should not use a ModuleInitializer
    // and apparently it'd kinda be like a static constructor
    // so changing to those wouldn't really change much.
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void InitializeModule()
    {
        ILHookInstructionILLabelCastFixes.InitHook();
        ILHookDMDManipulation.InitHook();
        LegacyILHookAntiExploder.InitHook();
    }
}
