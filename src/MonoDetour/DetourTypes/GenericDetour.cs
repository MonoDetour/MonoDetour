using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

static class GenericDetour
{
    static readonly Dictionary<MethodBase, ILLabel> firstRedirectForMethod = [];

    public static void Manipulator(ILContext il, MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new InvalidProgramException();

        MonoDetourData data = info.Data;

        // Console.WriteLine("Original: " + il.ToString());

        ILCursor c = new(il);

        if (info.DetourType == typeof(PostfixDetour))
        {
            c.Index -= 1;
            bool found = firstRedirectForMethod.TryGetValue(info.Data.Target, out var target);

            ILLabel retLabel = RedirectEarlyReturnsToLabel(c, target);

            if (!found)
                firstRedirectForMethod.Add(info.Data.Target, retLabel);

            c.MoveAfterLabels(); // Move ret label to next emitted instruction.
        }

        int structArgumentIdx = c.EmitParamsStruct(info);

        c.Emit(OpCodes.Ldloca, structArgumentIdx);

        if (!data.Manipulator.IsStatic)
        {
            throw new NotSupportedException(
                "Only static manipulator methods are supported for now."
            );
        }
        else
            c.Emit(OpCodes.Call, data.Manipulator);

        // I'd want to add this preprocessor directive,
        // but we'd need support for this in our HookGen.
        // #if !NET7_0_OR_GREATER // ref fields are supported since net7.0 so we don't need to apply this 'hack'
        if (!data.ManipulatorParameter.IsIn)
            c.ApplyStructValuesToMethod(info, structArgumentIdx);
        // #endif

        if (data.Owner.LogLevel == MonoDetourManager.Logging.Diagnostic)
        {
            c.Method.RecalculateILOffsets();
            Console.WriteLine($"Manipulated by {data.Manipulator.Name}: " + il);
        }
    }

    // Taken and adapted from HarmonyX
    private static ILLabel RedirectEarlyReturnsToLabel(ILCursor c, ILLabel? target)
    {
        if (c.Body.Instructions.Count == 0)
            c.Emit(OpCodes.Nop);

        ILLabel retLabel = c.Context.DefineLabel();

        var hasRet = false;
        foreach (var ins in c.Body.Instructions.Where(ins => ins.MatchRet()))
        {
            hasRet = true;

            // To allow intentional early returns,
            // we ignore cases where ret is called twice in a row.
            // But double ret at end of method is not early, and probably not intentional.
            if (ins.Next?.Next is not null)
            {
                if (ins.Next.OpCode == OpCodes.Ret || ins.Previous?.OpCode == OpCodes.Ret)
                    continue;
            }

            // A previous label may exist before us, but
            // someone has emitted a ret instruction afterwards.
            // If that is the case, redirect their ret to the existing label.
            if (
                target is not null
                && c.Body.Instructions.IndexOf(ins) < c.Body.Instructions.IndexOf(target.Target)
            )
            {
                // Console.WriteLine("Retargeted instruction!");
                ins.OpCode = OpCodes.Br;
                ins.Operand = target.Target!.Next;
                continue;
            }

            // If the above isn't the case, we can still point them to us.
            ins.OpCode = OpCodes.Br;
            ins.Operand = retLabel;
        }

        Instruction lastInstruction = c.Body.Instructions[c.Body.Instructions.Count - 1];
        lastInstruction.OpCode = hasRet ? OpCodes.Ret : OpCodes.Nop;
        lastInstruction.Operand = null;
        retLabel.Target = lastInstruction;

        if (target is not null && c.Body.Instructions.IndexOf(target.Target) == -1)
        {
            // We set the first target to previous instruction so it
            // doesn't get retargeted as someone who points to a ret label.
            target.Target = lastInstruction.Previous;
        }

        // Console.WriteLine("Last instruction is " + lastInstruction);
        // Console.WriteLine("target is " + target?.Target);

        return retLabel;
    }
}
