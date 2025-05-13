using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

class ReorgILLabel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Instruction? GetTarget(ILLabel label) => label.Target;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void SetTarget(ILLabel label, Instruction value) => label.Target = value;
}
