using System;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropILContext
{
    internal static int InteropAddReference<T>(this ILContext context, in T? t) =>
        MonoModVersion.IsReorg
            ? ReorgILContext.AddReference(context, in t)
            : LegacyAddReference(context, t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int LegacyAddReference<T>(ILContext context, T? t) => context.AddReference(t);
}
