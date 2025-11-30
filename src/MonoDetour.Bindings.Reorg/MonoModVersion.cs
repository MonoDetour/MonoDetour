using System;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoDetour.Bindings.Reorg.RuntimeDetour;

namespace MonoDetour.Bindings.Reorg;

internal static class MonoModVersion
{
    internal static bool IsReorg { get; }

    static MonoModVersion()
    {
        // Check that we are dealing with Reorg (>= v25) with the existence
        // of a type because of MonoMod forks with their own versioning.
        var archType = Type.GetType("MonoMod.Utils.ArchitectureKind, MonoMod.Utils");
        if (archType is not null)
        {
            IsReorg = true;
            InitAll();
        }
        else
        {
            IsReorg = false;
        }
    }

    static void InitAll()
    {
        ReorgILHook.Init();
        ReorgILLabel.Init();
        ReorgILCursor.Init();
        ReorgILContext.Init();
        ReorgFastDelegateInvokers.Init();
    }
}
