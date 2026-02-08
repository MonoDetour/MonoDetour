using MonoDetour.Aot;

internal static class AotDetourChain
{
    public static void ApplyAll()
    {
        foreach (var target in AotMonoDetourHook.TargetToHooks)
        {
            var method = target.Key;
            var aotHooks = target.Value;

            // TODO: Sort hooks according to their MonoDetourConfigs.
            foreach (var aotHook in aotHooks)
            {
                aotHook.Manipulate(method);
            }
        }
    }
}
