using System;
using System.Reflection;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropFastDelegateInvokers
{
    internal static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(
        ILContext il,
        Type delegateType
    ) => MonoModVersion.IsReorg ? ReorgFastDelegateInvokers.GetDelegateInvoker(delegateType) : null;
}
