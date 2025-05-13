using MonoMod.Cil;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

class ReorgILCursor
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int EmitReference<T>(ILCursor cursor, in T? t) => cursor.EmitReference(t);
}
