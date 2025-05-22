using System.Runtime.CompilerServices;

namespace MonoDetour;

file static class ModuleInitialization
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        ILHookGetDMDBeforeManipulation.InitHook();
    }
}
