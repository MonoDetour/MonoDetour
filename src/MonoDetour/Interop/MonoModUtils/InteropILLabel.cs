using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Bindings.Reorg.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Interop.MonoModUtils;

static class InteropILLabel
{
    internal static Instruction? InteropGetTarget(this ILLabel label) =>
        MonoModVersion.IsReorg ? ReorgILLabel.GetTarget(label) : LegacyGetTarget(label);

    internal static void InteropSetTarget(this ILLabel label, Instruction value)
    {
        if (MonoModVersion.IsReorg)
            ReorgILLabel.SetTarget(label, value);
        else
            LegacySetTarget(label, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static Instruction? LegacyGetTarget(ILLabel label) => label.Target;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static Instruction? LegacySetTarget(ILLabel label, Instruction value) => label.Target = value;
}
