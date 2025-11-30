using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.RuntimeDetour;

static class ReorgILHook
{
    delegate ILHook ILHookConstructor(
        MethodBase source,
        ILContext.Manipulator manip,
        object? config,
        bool applyByDefault
    );

    delegate object DetourConfigConstructor(
        string id,
        int? priority = null,
        IEnumerable<string>? before = null,
        IEnumerable<string>? after = null
    );

    static Func<object> detourContext_GetDefaultConfig = null!;
    static ILHookConstructor newILHook = null!;
    static DetourConfigConstructor newDetourConfig = null!;
    static Func<object, int?> detourConfigPriority = null!;
    static MethodInfo detourConfig_WithPriority = null!;

    static readonly ConcurrentDictionary<IMonoDetourConfig, object> interfaceToConfig = [];
    static readonly ConcurrentDictionary<object, object> configToConfig = [];
    static readonly ConcurrentDictionary<string, object> idToConfig = [];

    internal static void Init()
    {
        detourContext_GetDefaultConfig = typeof(DetourContext)
            .GetMethod("GetDefaultConfig", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<object>>();

        var detourConfigType = Type.GetType(
            "MonoMod.RuntimeDetour.DetourConfig, MonoMod.RuntimeDetour"
        )!;

        detourConfig_WithPriority = detourConfigType.GetMethod("WithPriority", [typeof(int?)])!;

        {
            var constructor = typeof(ILHook).GetConstructor([
                typeof(MethodBase),
                typeof(ILContext.Manipulator),
                detourConfigType,
                typeof(bool),
            ])!;

            using var dmd = new DynamicMethodDefinition(
                "newILHook",
                typeof(ILHook),
                [typeof(MethodBase), typeof(ILContext.Manipulator), typeof(object), typeof(bool)]
            );
            var il = dmd.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            newILHook = dmd.Generate().CreateDelegate<ILHookConstructor>();
        }

        {
            var constructor = detourConfigType.GetConstructor([
                typeof(string),
                typeof(int?),
                typeof(IEnumerable<string>),
                typeof(IEnumerable<string>),
            ])!;

            using var dmd = new DynamicMethodDefinition(
                "iLHookConstructor",
                typeof(object),
                [
                    typeof(string),
                    typeof(int?),
                    typeof(IEnumerable<string>),
                    typeof(IEnumerable<string>),
                ]
            );
            var il = dmd.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            newDetourConfig = dmd.Generate().CreateDelegate<DetourConfigConstructor>();
        }

        {
            var getPriority = detourConfigType.GetProperty("Priority")!.GetGetMethod()!;

            using var dmd = new DynamicMethodDefinition(
                "get_Priority",
                typeof(int?),
                [typeof(object)]
            );
            var il = dmd.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, getPriority);
            il.Emit(OpCodes.Ret);

            detourConfigPriority = dmd.Generate().CreateDelegate<Func<object, int?>>();
        }
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
            var contextConfig = detourContext_GetDefaultConfig();
            if (contextConfig is null)
            {
                if (!idToConfig.TryGetValue(id, out var idConfig))
                {
                    idConfig = newDetourConfig(id: id, priority: 0, before: null, after: null);
                    idToConfig.TryAdd(id, idConfig);
                }
                return newILHook(target, manipulator, idConfig, applyByDefault: false);
            }

            if (detourConfigPriority(contextConfig) is not null)
            {
                return newILHook(target, manipulator, contextConfig, applyByDefault: false);
            }

            if (!configToConfig.TryGetValue(contextConfig, out var configWithPriority))
            {
                configWithPriority = detourConfig_WithPriority.Invoke(contextConfig, [0])!;
                configToConfig.TryAdd(contextConfig, configWithPriority);
            }

            return newILHook(target, manipulator, configWithPriority, applyByDefault: false);
        }

        if (!interfaceToConfig.TryGetValue(config, out var realConfig))
        {
            realConfig = newDetourConfig(
                config.OverrideId ?? id,
                config.Priority,
                config.Before,
                config.After
            );
            interfaceToConfig.TryAdd(config, realConfig);
        }

        return newILHook(target, manipulator, realConfig, applyByDefault: false);
    }
}
