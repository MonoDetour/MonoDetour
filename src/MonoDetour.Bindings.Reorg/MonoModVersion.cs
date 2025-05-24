using MonoMod.RuntimeDetour;

namespace MonoDetour.Bindings.Reorg;

internal static class MonoModVersion
{
    internal static bool IsReorg { get; }

    static MonoModVersion()
    {
        if (typeof(Hook).Assembly.GetName().Version.Major >= 25)
        {
            IsReorg = true;
        }
        else
        {
            IsReorg = false;
        }
    }
}
