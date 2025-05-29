using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Interop.Cecil;

static class ILHookInstructionILLabelCastFixes
{
    static ILHook castILLabelToInstructionToStringILHook = null!;
    static ILHook castILLabelToInstructionGetSizeILHook = null!;

    static bool initialized;

    /// <summary>
    /// Initializes ILHooks to fix MonoMod's ILLabel and ILLabel[] casts
    /// to Mono.Cecil's Instruction in some Instruction methods.
    /// </summary>
    /// <remarks>
    /// MonoMod turns Instructions into ILLabels for ILHook manipulators.
    /// </remarks>
    internal static void InitHook()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        try
        {
            InitHookToString();
            InitHookGetSize();
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"MonoDetour doesn't seem to support this Mono.Cecil version, "
                    + $"please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Instruction).Assembly}'",
                ex
            );
        }
    }

    static void InitHookToString()
    {
        castILLabelToInstructionToStringILHook = new(
            typeof(Instruction).GetMethod(nameof(Instruction.ToString), []),
            ILHook_Instruction_ToString
        );
    }

    static void InitHookGetSize()
    {
        castILLabelToInstructionGetSizeILHook = new(
            typeof(Instruction).GetMethod(nameof(Instruction.GetSize), []),
            ILHook_Instruction_GetSize
        );
    }

    private static void ILHook_Instruction_ToString(ILContext il)
    {
        // IL_006f: ldarg.0
        // IL_0070: ldfld object Mono.Cecil.Cil.Instruction::operand /* 0400055C */
        // IL_0075: castclass Mono.Cecil.Cil.Instruction /* 02000104 */
        // IL_007a: call void Mono.Cecil.Cil.Instruction::AppendLabel(class [netstandard]System.Text.StringBuilder, class Mono.Cecil.Cil.Instruction) /* 06000A90 */
        // IL_007f: br.s IL_00eb

        // IL_0081: ldarg.0
        // IL_0082: ldfld object Mono.Cecil.Cil.Instruction::operand /* 0400055C */
        // IL_0087: castclass class Mono.Cecil.Cil.Instruction[] /* 1B00011B */
        // IL_008c: stloc.1

        ILCursor c = new(il);

        bool found = c.TryGotoNext(x => x.MatchCastclass<Instruction>());
        if (!found)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                $"{nameof(ILHook_Instruction_ToString)}] Could not find 'castclass Mono.Cecil.Cil.Instruction'!"
            );
            return;
        }

        c.EmitDelegate(IfILLabelThenReturnTargetInstruction);

        found = c.TryGotoNext(x => x.MatchCastclass<Instruction[]>());
        if (!found)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                $"{nameof(ILHook_Instruction_ToString)}] Could not find 'castclass class Mono.Cecil.Cil.Instruction[]'!"
            );
            return;
        }

        c.EmitDelegate(IfILLabelArrayThenReturnTargetInstruction);
    }

    private static void ILHook_Instruction_GetSize(ILContext il)
    {
        ILCursor c = new(il);

        bool found = c.TryGotoNext(x => x.MatchCastclass<Instruction[]>());
        if (!found)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                $"[{nameof(ILHook_Instruction_GetSize)}] Could not find 'castclass class Mono.Cecil.Cil.Instruction[]'!"
            );
            return;
        }

        c.EmitDelegate(IfILLabelArrayThenReturnTargetInstruction);
    }

    static object? IfILLabelThenReturnTargetInstruction(object operand)
    {
        if (operand is ILLabel label)
        {
            var target =
                label.InteropGetTarget()
                ?? throw new NullReferenceException("ILLabel.Target must not not be null!");

            return target;
        }

        return operand;
    }

    static object IfILLabelArrayThenReturnTargetInstruction(object operand)
    {
        if (operand is ILLabel[] label)
        {
            return label.Select(l => l.InteropGetTarget()).ToArray();
        }

        return operand;
    }
}
