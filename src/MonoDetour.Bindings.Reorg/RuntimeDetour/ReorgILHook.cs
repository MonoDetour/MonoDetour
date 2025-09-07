using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Bindings.Reorg.RuntimeDetour;

static class ReorgILHook
{
    static readonly ConcurrentDictionary<IMonoDetourConfig, object> interfaceToConfig = [];
    static readonly ConcurrentDictionary<object, object> configToConfig = [];
    static readonly ConcurrentDictionary<string, object> idToConfig = [];

    // This only exists because we shouldn't use DetourConfig type in fields because
    // Assembly.GetTypes() would throw on this type, and games keep doing that
    // and not handling it properly with try catch (because the API sucks).
    static bool TryGetDetourConfig<TKey>(
        this IDictionary<TKey, object> data,
        TKey key,
        [NotNullWhen(true)] out DetourConfig? value
    )
    {
        if (data.TryGetValue(key, out var tmp))
        {
            value = (DetourConfig)tmp;
            return true;
        }

        value = default;
        return false;
    }

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
                if (!idToConfig.TryGetDetourConfig(id, out var idConfig))
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

            if (!configToConfig.TryGetDetourConfig(contextConfig, out var configWithPriority))
            {
                configWithPriority = contextConfig.WithPriority(0);
                configToConfig.TryAdd(contextConfig, configWithPriority);
            }

            return new ILHook(target, manipulator, configWithPriority, applyByDefault: false);
        }

        if (!interfaceToConfig.TryGetDetourConfig(config, out var realConfig))
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
