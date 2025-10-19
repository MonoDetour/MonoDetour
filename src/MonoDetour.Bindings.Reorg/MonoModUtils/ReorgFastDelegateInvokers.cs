using System;
using System.Reflection;
using MonoMod.Cil;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgFastDelegateInvokers
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(Type delegateType) =>
        FastDelegateInvokers.GetDelegateInvoker(delegateType);
}
