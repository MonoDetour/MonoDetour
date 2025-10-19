using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropFastDelegateInvokers
{
    static MethodInfo? getDelegateInvoker;

    internal static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(
        ILContext il,
        Type delegateType
    ) =>
        MonoModVersion.IsReorg
            ? ReorgFastDelegateInvokers.GetDelegateInvoker(delegateType)
            : LegacyGetDelegateInvoker(il, delegateType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static (MethodInfo Invoker, Type Delegate)? LegacyGetDelegateInvoker(
        ILContext il,
        Type delegateType
    )
    {
        if (il.ReferenceBag is not RuntimeILReferenceBag bag)
        {
            // This is not optimal. Maybe MonoDetour should handle its own Bag for cases like this?
            throw new Exception(
                $"ReferenceBag is not {nameof(RuntimeILReferenceBag)}! "
                    + "If you are not in an ILHook managed by MonoMod, do not use this method."
            );
        }

        if (getDelegateInvoker is null)
        {
            var type = typeof(RuntimeILReferenceBag);
            var method = type.GetMethod(nameof(RuntimeILReferenceBag.GetDelegateInvoker))!;
            getDelegateInvoker = method;
        }

        var genericGetDelegateInvoker = getDelegateInvoker.MakeGenericMethod(delegateType);
        var delegateInvoker = (MethodInfo)genericGetDelegateInvoker.Invoke(bag, [])!;

        if (delegateInvoker is null)
            return null;

        return (delegateInvoker, delegateType);
    }
}
