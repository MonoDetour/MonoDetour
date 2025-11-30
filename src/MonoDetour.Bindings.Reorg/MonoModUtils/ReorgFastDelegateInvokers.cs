using System;
using System.Reflection;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgFastDelegateInvokers
{
    static Func<Type, (MethodInfo Invoker, Type Delegate)?> getDelegateInvoker = null!;

    internal static void Init()
    {
        var fastDelegateInvokersType = Type.GetType(
            "MonoMod.Cil.FastDelegateInvokers, MonoMod.Utils"
        )!;

        getDelegateInvoker = fastDelegateInvokersType
            .GetMethod(
                "GetDelegateInvoker",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(Type)],
                null
            )!
            .CreateDelegate<Func<Type, (MethodInfo Invoker, Type Delegate)?>>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(Type delegateType) =>
        getDelegateInvoker(delegateType);
}
