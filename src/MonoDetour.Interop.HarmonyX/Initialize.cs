using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HarmonyLib.Internal.Patching;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Interop.HarmonyX;

/// <summary>
/// Initialize HarmonyX interop for MonoDetour.
/// </summary>
public static class Initialize
{
    static readonly MonoDetourManager manager = new("com.github.MonoDetour.Interop.HarmonyX");
    static bool initialized;

    /// <summary>
    /// Initialize HarmonyX interop for MonoDetour.
    /// </summary>
    public static void Apply()
    {
        if (initialized)
            return;

        initialized = true;

        var target = typeof(ILManipulator).GetMethod(nameof(ILManipulator.WriteTo));
        manager.ILHook(target, ILHook_ILManipulator_WriteTo);
    }

    // HarmonyX rewrites all instructions in the target method.
    // We still want to keep track of which instruction was there originally,
    // so we do this hacky workaround.
    private static void ILHook_ILManipulator_WriteTo(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // Match end of instruction loop
        var result = w.MatchRelaxed(
            x => x.MatchLdloc(2) && w.SetCurrentTo(x),
            x => x.MatchCallvirt(out _),
            x => x.MatchBrtrue(out _)
        );

        if (!result.IsValid)
        {
            manager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        var harmonyToCecil = w.DeclareVariable(
            typeof(Dictionary<CodeInstruction, (Instruction, bool)>)
        );
        var newOriginalInstructions = w.DeclareVariable(typeof(List<Instruction>));
        var oldToNew = w.DeclareVariable(typeof(Dictionary<Instruction, Instruction>));

        w.InsertBefore(
            w.First,
            w.Create(OpCodes.Ldarg_0), // this; ILManipulator
            w.Create(OpCodes.Ldarg_1), // MethodBody
            w.CreateCall(MapInstructions),
            w.Create(OpCodes.Stloc, harmonyToCecil),
            w.Create(OpCodes.Ldloc, harmonyToCecil),
            w.CreateCall(CreateInstructionsList),
            w.Create(OpCodes.Stloc, newOriginalInstructions),
            w.Create(OpCodes.Ldloc, harmonyToCecil),
            w.CreateCall(CreateOldToNewDictionary),
            w.Create(OpCodes.Stloc, oldToNew)
        );

        w.InsertBeforeCurrent(
            w.Create(OpCodes.Ldarg_1),
            w.Create(OpCodes.Ldloc_3), // 'cur'; current instruction
            w.Create(OpCodes.Ldloc, harmonyToCecil),
            w.Create(OpCodes.Ldloc, newOriginalInstructions),
            w.Create(OpCodes.Ldloc, oldToNew),
            w.CreateCall(MapInstruction)
        );

        w.InsertBeforeStealLabels(
            w.Last,
            w.Create(OpCodes.Ldarg_1),
            w.Create(OpCodes.Ldloc, newOriginalInstructions),
            w.Create(OpCodes.Ldloc, oldToNew),
            w.CreateCall(UpdateOriginalInstructions)
        );
    }

    static Dictionary<CodeInstruction, (Instruction, bool)> MapInstructions(
        ILManipulator manipulator,
        MethodBody body
    )
    {
        var originalInstructions = HookTargetRecords.GetOriginalInstructions(body.Method);
        HashSet<Instruction> originals = [.. originalInstructions];

        var rawInstructions = manipulator.codeInstructions;
        var harmonyToCecil = new Dictionary<CodeInstruction, (Instruction, bool)>(
            rawInstructions.Count()
        );

        foreach (var raw in rawInstructions)
        {
            harmonyToCecil.Add(
                raw.Instruction,
                (raw.CILInstruction, originals.Contains(raw.CILInstruction))
            );
        }

        return harmonyToCecil;
    }

    static List<Instruction> CreateInstructionsList(
        Dictionary<CodeInstruction, (Instruction, bool)> harmonyToCecil
    ) => new(harmonyToCecil.Count);

    static Dictionary<Instruction, Instruction> CreateOldToNewDictionary(
        Dictionary<CodeInstruction, (Instruction, bool)> harmonyToCecil
    ) => new(harmonyToCecil.Count);

    static void MapInstruction(
        MethodBody body,
        CodeInstruction cur,
        Dictionary<CodeInstruction, (Instruction, bool)> harmonyToCecil,
        List<Instruction> newOriginalInstructions,
        Dictionary<Instruction, Instruction> oldToNew
    )
    {
        if (!harmonyToCecil.TryGetValue(cur, out (Instruction oldIns, bool original) value))
            return;

        var newIns = body.Instructions[^1];
        oldToNew.Add(value.oldIns, newIns);

        if (!value.original)
            return;

        newOriginalInstructions.Add(newIns);
    }

    static void UpdateOriginalInstructions(
        MethodBody body,
        List<Instruction> newOriginalInstructions,
        Dictionary<Instruction, Instruction> oldToNew
    )
    {
        var method = body.Method;
        HookTargetRecords.SwapOriginalInstructionsCollection(method, new(newOriginalInstructions));

        var hookTargetInfo = HookTargetRecords.GetHookTargetInfo(body.Method);
        var postfixes = hookTargetInfo.PostfixInfo.FirstPostfixInstructions;

        for (int i = 0; i < postfixes.Count; i++)
        {
            var oldInstruction = postfixes[i];

            if (!oldToNew.TryGetValue(oldInstruction, out var newInstruction))
                continue;

            postfixes[i] = newInstruction;
        }
    }
}
