namespace MonoDetour.Bindings.Reorg;

internal static class MonoModVersion
{
    internal static bool IsReorg { get; }

    static MonoModVersion()
    {
        try
        {
            _ = MonoMod.Utils.ArchitectureKind.Unknown;
            IsReorg = true;
        }
        catch
        {
            IsReorg = false;
        }
    }
}
