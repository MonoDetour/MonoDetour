using System.Runtime.CompilerServices;
using MonoDetour.Interop.Cecil;

namespace MonoDetour;

file static class ModuleInitialization
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        ILHookGetDMDBeforeManipulation.InitHook();
        ILHookInstructionILLabelCastFixes.InitHook();
    }
}
