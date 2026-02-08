using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.RuntimeDetour;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Interop.RuntimeDetour;

static class ProxyILHookConstructor
{
    static readonly ILHook detourContextHook = null!;

    static ProxyILHookConstructor()
    {
        if (MonoModVersion.IsReorg)
        {
            return;
        }

        detourContextHook = new(
            ((Delegate)DetourContextGetCurrent).Method,
            ILHook_DetourContextGetCurrent
        );
    }

    private static void ILHook_DetourContextGetCurrent(ILContext il)
    {
        var getter =
            typeof(DetourContext)
                .GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetGetMethod(nonPublic: true)
            ?? throw new NullReferenceException("Couldn't find 'DetourContext.get_Current'.");

        ILCursor c = new(il);
        c.Body.Instructions.Clear();
        c.Emit(OpCodes.Call, getter);
        c.Emit(OpCodes.Ret);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static DetourContext DetourContextGetCurrent()
    {
        throw new NotImplementedException($"{nameof(DetourContextGetCurrent)} wasn't initialized.");
    }

    /// <summary>
    /// Constructs an ILHook for either legacy or reorg, mapping <see cref="IMonoDetourConfig"/>
    /// to a valid config for either.<br/>
    /// Does not apply by default.
    /// </summary>
    internal static ILHook ConstructILHook(
        MethodBase target,
        ILContext.Manipulator manipulator,
        IMonoDetourConfig? config,
        string id
    )
    {
        if (MonoModVersion.IsReorg)
        {
            return ReorgILHook.ConstructILHook(target, manipulator, config, id);
        }
        else
        {
            return ConstructLegacyILHook(target, manipulator, config, id);
        }
    }

    /// <summary>
    /// Constructs a legacy ILHook, mapping MonoDetour's IDetourConfig to a real ILHookConfig type.<br/>
    /// Does not apply by default.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ILHook ConstructLegacyILHook(
        MethodBase target,
        ILContext.Manipulator manipulator,
        IMonoDetourConfig? config,
        string id
    )
    {
        if (config is null)
        {
            // In this implementation existing DetourContext will not override applyByDefault.
            // In legacy applyByDefault is defined in a DetourConfig as ManualApply
            // but we mirror reorg's interface where it is in an ILHook's constructor.

            DetourContext existingContext = DetourContextGetCurrent();
            if (existingContext is null)
            {
                return new ILHook(target, manipulator, new() { ID = id, ManualApply = true });
            }

            // We'll use the DetourContext's ID since it defaults to a sensible value (assembly name).
            var contextILHookConfig = existingContext.ILHookConfig;
            if (contextILHookConfig.ManualApply == true)
            {
                return new ILHook(target, manipulator, contextILHookConfig);
            }

            var configWithManualApply = contextILHookConfig with { ManualApply = true };
            return new ILHook(target, manipulator, configWithManualApply);
        }

        var realConfig = new ILHookConfig()
        {
            ID = config.OverrideId ?? id,
            Priority = config.Priority,
            Before = config.Before,
            After = config.After,
            ManualApply = true,
        };

        return new ILHook(target, manipulator, realConfig);
    }
}
