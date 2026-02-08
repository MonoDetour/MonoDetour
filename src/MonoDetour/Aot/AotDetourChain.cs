using MonoDetour.Aot;

internal static class AotDetourChain
{
    public static void ApplyAll()
    {
        foreach (var (method, aotHooks) in AotMonoDetourHook.TargetToHooks)
        {
            // TODO: Sort hooks according to their MonoDetourConfigs.
            foreach (var aotHook in aotHooks)
            {
                aotHook.Manipulate(method);
            }
        }
    }
}
