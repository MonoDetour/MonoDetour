using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HarmonyLib.Internal.Patching;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Interop.HarmonyX;

static class TrackInstructions
{
    internal static readonly MonoDetourManager instructionManager = new(HarmonyXInterop.ManagerName);

    internal static void Init()
    {
        var target = typeof(ILManipulator).GetMethod(nameof(ILManipulator.WriteTo));
        if (target is null)
        {
            instructionManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "ILManipulator.WriteTo doesn't exist!"
            );
            return;
        }

        instructionManager.ILHook(target, ILHook_ILManipulator_WriteTo);
    }

    // HarmonyX rewrites all instructions in the target method.
    // We still want to keep track of which instruction was there originally,
    // so we do this hacky workaround.
    private static void ILHook_ILManipulator_WriteTo(ILManipulationInfo info)
    {
        // Set to false at end of method if everything ok.
        HarmonyXInterop.anyFailed = true;
        ILWeaver w = new(info);

        // Get end of instruction loop
        if (!w.Body.HasExceptionHandlers)
        {
            instructionManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "ILManipulator.WriteTo has no Exception handlers!"
            );
            return;
        }

        if (!w.Body.ExceptionHandlers[0].TryStart.MatchBr(out var loopEndLabel))
        {
            instructionManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "ILManipulator.WriteTo first try block's first instruction is not br!"
            );
            return;
        }

        var loopEnd = loopEndLabel.InteropGetTarget()!;
        w.CurrentTo(loopEnd);

        if (!loopEnd.MatchLdloc(out int loopLocIdx))
        {
            instructionManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "ILManipulator.WriteTo first try block's first instruction's branch target is not Ldloc! "
                    + $"Instead it is: '{loopEnd}'"
            );
            return;
        }

        int locIdxForCurrent;

        // We want the 'Current' CodeInstruction being iterated.
        // In older HarmonyX versions, the foreach loop is done on a tuple.
        // In newer versions, it's directly done on CodeInstruction.
        // If this is not a CodeInstruction, it's a tuple, and the next local
        // index will be used for the first item in the tuple, which is what
        // we are looking for.
        if (w.Body.Variables[loopLocIdx].VariableType == w.Context.Import(typeof(CodeInstruction)))
            locIdxForCurrent = loopLocIdx;
        else
            locIdxForCurrent = loopLocIdx + 1;

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
            w.Create(OpCodes.Ldloc, locIdxForCurrent),
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

        HarmonyXInterop.anyFailed = false;
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
