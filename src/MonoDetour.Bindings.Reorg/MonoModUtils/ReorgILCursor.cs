using System;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgILCursor
{
    delegate int Signature<T>(ILCursor cursor, in T? t);
    static MethodInfo emitReferenceMethod = null!;

    internal static void Init()
    {
        emitReferenceMethod =
            typeof(ILCursor).GetMethod(nameof(ILCursor.EmitReference))
            ?? throw new NullReferenceException();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int EmitReference<T>(ILCursor cursor, in T? t) =>
        Container<T>.EmitReference(cursor, t);

    static class Container<T>
    {
        internal static Signature<T> EmitReference =>
            field ??= emitReferenceMethod
                .MakeGenericMethod([typeof(T)])
                .CreateDelegate<Signature<T>>();
    }
}
