using System.Collections.Concurrent;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Bindings.Reorg.RuntimeDetour;

static class ReorgILHook
{
    static readonly ConcurrentDictionary<IMonoDetourConfig, DetourConfig> interfaceToConfig = [];
    static readonly ConcurrentDictionary<DetourConfig, DetourConfig> configToConfig = [];
    static readonly ConcurrentDictionary<string, DetourConfig> idToConfig = [];

    /// <summary>
    /// Constructs a reorg ILHook, mapping MonoDetour's IDetourConfig to a real DetourConfig type.<br/>
    /// Does not apply by default.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ILHook ConstructILHook(
        MethodBase target,
        ILContext.Manipulator manipulator,
        IMonoDetourConfig? config,
        string id
    )
    {
        if (config is null)
        {
            var contextConfig = DetourContext.GetDefaultConfig();
            if (contextConfig is null)
            {
                if (!idToConfig.TryGetValue(id, out var idConfig))
                {
                    idConfig = new(id: id, priority: 0);
                    idToConfig.TryAdd(id, idConfig);
                }
                return new ILHook(target, manipulator, idConfig, applyByDefault: false);
            }

            if (contextConfig.Priority is not null)
            {
                return new(target, manipulator, contextConfig, applyByDefault: false);
            }

            if (!configToConfig.TryGetValue(contextConfig, out var configWithPriority))
            {
                configWithPriority = contextConfig.WithPriority(0);
                configToConfig.TryAdd(contextConfig, configWithPriority);
            }

            return new ILHook(target, manipulator, configWithPriority, applyByDefault: false);
        }

        if (!interfaceToConfig.TryGetValue(config, out var realConfig))
        {
            realConfig = new DetourConfig(
                config.OverrideId ?? id,
                config.Priority,
                config.Before,
                config.After
            );
            interfaceToConfig.TryAdd(config, realConfig);
        }

        return new ILHook(target, manipulator, realConfig, applyByDefault: false);
    }
}
