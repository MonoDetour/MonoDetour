using System.Runtime.CompilerServices;
using MonoDetour.Interop.Cecil;
using MonoDetour.Interop.RuntimeDetour;

namespace MonoDetour;

file static class ModuleInitialization
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        ILHookInstructionILLabelCastFixes.InitHook();
        ILHookDMDManipulation.InitHook();
    }
}
