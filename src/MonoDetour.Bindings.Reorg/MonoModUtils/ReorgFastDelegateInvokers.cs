using System;
using System.Reflection;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgFastDelegateInvokers
{
    delegate ValueTuple<MethodInfo, Type>? GetDelegateInvokerSig(Type type);
    static GetDelegateInvokerSig? getDelegateInvoker;
    static MethodInfo getDelegateInvokerMethod = null!;

    internal static void Init()
    {
        var fastDelegateInvokersType = Type.GetType(
            "MonoMod.Cil.FastDelegateInvokers, MonoMod.Utils"
        )!;

        getDelegateInvokerMethod = fastDelegateInvokersType.GetMethod(
            "GetDelegateInvoker",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(Type)],
            null
        )!;

        try
        {
            // This always fails on Mono for some reason I don't know.
            getDelegateInvoker = getDelegateInvokerMethod.CreateDelegate<GetDelegateInvokerSig>();
        }
        catch (ArgumentException) when (Type.GetType("Mono.Runtime") is { }) { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(Type delegateType) =>
        getDelegateInvoker is { }
            ? getDelegateInvoker.Invoke(delegateType)
            : ((MethodInfo Invoker, Type Delegate)?)
                getDelegateInvokerMethod.Invoke(null, [delegateType]);
}
