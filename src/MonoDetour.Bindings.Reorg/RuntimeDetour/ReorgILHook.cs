using System.Collections.Generic;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Bindings.Reorg.RuntimeDetour;

static class ReorgILHook
{
    static readonly Dictionary<IDetourConfig, DetourConfig> interfaceToConfig = [];

    /// <summary>
    /// Constructs a reorg ILHook, mapping MonoDetour's IDetourConfig to a real DetourConfig type.<br/>
    /// Does not apply by default.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ILHook ConstructILHook(
        MethodBase target,
        ILContext.Manipulator manipulator,
        IDetourConfig? config
    )
    {
        if (config is null)
        {
            return new ILHook(target, manipulator, applyByDefault: false);
        }

        if (interfaceToConfig.TryGetValue(config, out var realConfig))
        {
            return new ILHook(target, manipulator, realConfig, applyByDefault: false);
        }

        realConfig = new DetourConfig(config.Id, config.Priority, config.Before, config.After);
        interfaceToConfig.Add(config, realConfig);

        return new ILHook(target, manipulator, realConfig, applyByDefault: false);
    }
}
