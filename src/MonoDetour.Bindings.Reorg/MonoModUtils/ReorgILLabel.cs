using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgILLabel
{
    static Func<ILLabel, Instruction> get_Target = null!;
    static Action<ILLabel, Instruction> set_Target = null!;

    internal static void Init()
    {
        var target = typeof(ILLabel).GetProperty(nameof(ILLabel.Target));
        set_Target = target!.GetSetMethod()!.CreateDelegate<Action<ILLabel, Instruction>>();
        get_Target = target!.GetGetMethod()!.CreateDelegate<Func<ILLabel, Instruction>>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Instruction? GetTarget(ILLabel label) => get_Target(label);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void SetTarget(ILLabel label, Instruction value) => set_Target(label, value);
}
