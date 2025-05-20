using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgILContext
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int AddReference<T>(ILContext context, in T value) =>
        context.AddReference(value);
}
