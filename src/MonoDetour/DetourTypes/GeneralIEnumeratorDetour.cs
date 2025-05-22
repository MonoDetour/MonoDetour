using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that replaces the target method's returned
/// IEnumerator class with its own IEnumerator.
/// </summary>
internal class GeneralIEnumeratorDetour
{
    static readonly MethodInfo genericEnumeratorDriver = typeof(GeneralIEnumeratorDetour).GetMethod(
        nameof(GenericEnumeratorDriver),
        (BindingFlags)~0
    )!;

    public static void Manipulator(ILContext il, IReadOnlyMonoDetourHook hook)
    {
        ILCursor c = new(il);
        c.Index -= 1;

        if (hook is MonoDetourHook<IEnumeratorDetour>)
        {
            c.Emit(OpCodes.Call, hook.Manipulator);
        }
        else
        {
            c.InteropEmitReference(hook);
            if (hook.Target is MethodInfo methodInfo && methodInfo.ReturnType.IsGenericType)
            {
                var genericType = methodInfo.ReturnType.GenericTypeArguments[0];
                var method = genericEnumeratorDriver.MakeGenericMethod(genericType);
                c.Emit(OpCodes.Call, method);
            }
            else
            {
                c.Emit(OpCodes.Call, ((Delegate)EnumeratorDriver).Method);
            }
        }

        hook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                c.Method.RecalculateILOffsets();
                return $"Manipulated by {hook.Manipulator.Name}: {il}";
            }
        );
    }

    private static IEnumerator EnumeratorDriver(
        IEnumerator enumerator,
        IReadOnlyMonoDetourHook hook
    )
    {
        if (hook is MonoDetourHook<IEnumeratorPrefixDetour>)
            hook.Manipulator.Invoke(null, [enumerator]);

        while (enumerator.MoveNext())
            yield return enumerator.Current;

        if (hook is MonoDetourHook<IEnumeratorPostfixDetour>)
            hook.Manipulator.Invoke(null, [enumerator]);
    }

    private static IEnumerator<T> GenericEnumeratorDriver<T>(
        IEnumerator<T> enumerator,
        IReadOnlyMonoDetourHook hook
    )
    {
        if (hook is MonoDetourHook<IEnumeratorPrefixDetour>)
            hook.Manipulator.Invoke(null, [enumerator]);

        while (enumerator.MoveNext())
            yield return enumerator.Current;

        if (hook is MonoDetourHook<IEnumeratorPostfixDetour>)
            hook.Manipulator.Invoke(null, [enumerator]);
    }
}
