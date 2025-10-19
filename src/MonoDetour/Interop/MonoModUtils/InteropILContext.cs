using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoDetour.Cil;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropILContext
{
    static MethodInfo? getGetter;

    internal static int InteropAddReference<T>(this ILContext context, in T? t) =>
        MonoModVersion.IsReorg
            ? ReorgILContext.AddReference(context, in t)
            : LegacyAddReference(context, t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int LegacyAddReference<T>(ILContext context, T? t) => context.AddReference(t);

    internal static IEnumerable<Instruction> InteropGetReference(
        this ILContext context,
        ILWeaver weaver,
        int id,
        Delegate @delegate
    ) =>
        MonoModVersion.IsReorg
            ? ReorgILContext.GetReference(@delegate.GetType(), context, id)
            : LegacyGetReference(@delegate.GetType(), context, weaver, id);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<Instruction> LegacyGetReference(
        Type t,
        ILContext context,
        ILWeaver w,
        int id
    )
    {
        if (context.ReferenceBag is not RuntimeILReferenceBag bag)
        {
            // This is not optimal. Maybe MonoDetour should handle its own Bag for cases like this?
            throw new Exception(
                $"ReferenceBag is not {nameof(RuntimeILReferenceBag)}! "
                    + "If you are not in an ILHook managed by MonoMod, do not use this method."
            );
        }

        if (getGetter is null)
        {
            var type = typeof(RuntimeILReferenceBag);
            var method = type.GetMethod(nameof(RuntimeILReferenceBag.GetGetter))!;
            getGetter = method;
        }

        var genericGetDelegateInvoker = getGetter.MakeGenericMethod(t);
        var delegateInvoker = (MethodInfo)genericGetDelegateInvoker.Invoke(bag, [])!;

        yield return w.Create(OpCodes.Ldc_I4, id);
        yield return w.Create(OpCodes.Call, delegateInvoker);
    }
}
