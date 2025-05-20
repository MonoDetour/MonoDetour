using System;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropILCursor
{
    internal static int InteropEmitReference<T>(this ILCursor cursor, in T? t) =>
        MonoModVersion.IsReorg
            ? ReorgILCursor.EmitReference(cursor, in t)
            : LegacyEmitReference(cursor, t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int LegacyEmitReference<T>(ILCursor cursor, T? t) => cursor.EmitReference(t);

    internal static int InteropEmitReferenceBefore<T>(
        this ILContext context,
        Instruction target,
        in T? t
    ) => InteropEmitReference(new ILCursor(context).Goto(target), t);

    internal static void EmitGetReferenceBefore<T>(ILContext context, Instruction target, int id)
        where T : Delegate => new ILCursor(context).Goto(target).EmitGetReference<T>(id);

    internal static int EmitDelegateBefore<T>(ILContext context, Instruction target, in T cb)
        where T : Delegate => new ILCursor(context).Goto(target).EmitDelegate(cb);
}
