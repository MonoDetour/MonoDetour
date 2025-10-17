using System.Collections.Generic;
using System.Linq;
using HarmonyLib.Internal.Util;
using HarmonyLib.Public.Patching;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Interop.HarmonyX;

static class TrackPatches
{
    internal static readonly MonoDetourManager patchManager = new(HarmonyXInterop.ManagerName);

    internal static void Init()
    {
        var writePrefixes = typeof(HarmonyManipulator).GetMethod(
            nameof(HarmonyManipulator.WritePrefixes),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        if (writePrefixes is null)
        {
            patchManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "HarmonyManipulator.WritePrefixes doesn't exist!"
            );
            return;
        }

        var writePostfixes = typeof(HarmonyManipulator).GetMethod(
            nameof(HarmonyManipulator.WritePostfixes),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        if (writePostfixes is null)
        {
            patchManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "HarmonyManipulator.WritePostfixes doesn't exist!"
            );
            return;
        }

        patchManager.ILHook(writePrefixes, ILHook_HarmonyManipulator_WritePrefixes);

        if (HarmonyXInterop.anyFailed)
        {
            patchManager.Dispose();
            return;
        }

        patchManager.ILHook(writePostfixes, ILHook_HarmonyManipulator_WritePostfixes);

        if (HarmonyXInterop.anyFailed)
        {
            patchManager.Dispose();
            return;
        }
    }

    static void ILHook_HarmonyManipulator_WritePrefixes(ILManipulationInfo info)
    {
        HarmonyXInterop.anyFailed = true;
        ILWeaver w = new(info);

        Instruction ldarg0_ResultVar = null!;
        Instruction ldarg0_RunOriginalParam = null!;
        Instruction declareVar_ResultVar = null!;
        Instruction declareVar_RunOriginalParam = null!;
        Instruction start_RunOriginalParamLogic = null!;
        Instruction end_RunOriginalParamLogic = null!;
        Instruction end_RetLogic = null!;
        Instruction ilEmitterField = null!;

        var result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.ResultVar)),
            x => x.MatchLdloc(out _),
            x => x.MatchLdtoken(out _),
            x => x.MatchCall(out _),
            x => x.MatchCall(out _),
            x => x.MatchBrtrue(out _),
            x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_ResultVar, x),
            x => x.MatchLdfld(out _) && w.SetInstructionTo(ref ilEmitterField, x),
            x => x.MatchLdloc(out _),
            x =>
                x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                && w.SetInstructionTo(ref declareVar_ResultVar, x)
        );

        if (!result.IsValid)
        {
            // Let's try for older HarmonyX versions
            result = w.MatchRelaxed(
                x => x.MatchLdarg(0),
                x => x.MatchLdfld(out _),
                x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.ResultVar)),
                x => x.MatchLdloc(out _),
                x => x.MatchLdtoken(out _),
                x => x.MatchCall(out _),
                x => x.MatchBeq(out _),
                x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_ResultVar, x),
                x => x.MatchLdfld(out _) && w.SetInstructionTo(ref ilEmitterField, x),
                x => x.MatchLdloc(out _),
                x =>
                    x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                    && w.SetInstructionTo(ref declareVar_ResultVar, x)
            );

            if (!result.IsValid)
            {
                patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
                return;
            }
        }

        result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.RunOriginalParam)),
            x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_RunOriginalParam, x),
            x => x.MatchLdfld(out _),
            x => x.MatchLdtoken(out _),
            x => x.MatchCall(out _),
            x =>
                x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                && w.SetInstructionTo(ref declareVar_RunOriginalParam, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        result = w.MatchRelaxed(
            x => x.MatchLdarg(0) && w.SetInstructionTo(ref start_RunOriginalParamLogic, x),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld(typeof(OpCodes), nameof(OpCodes.Ldloc)),
            x => x.MatchLdloc(out _),
            x => x.MatchCallvirt(out _),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld(typeof(OpCodes), nameof(OpCodes.Brfalse)),
            x => x.MatchLdloc(out _),
            x => x.MatchCallvirt(out _) && w.SetInstructionTo(ref end_RunOriginalParamLogic, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld(out _),
            x => x.MatchLdloc(out _),
            x => x.MatchCallvirt(out _) && w.SetInstructionTo(ref end_RetLogic, x),
            x => x.MatchLdcI4(1),
            x => x.MatchRet() || x.MatchBr(out _)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        w.InsertBranchOver(ldarg0_ResultVar, declareVar_ResultVar);

        w.InsertBeforeStealLabels(
            declareVar_ResultVar.Next,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(GetRetVar)
        );
        // Mono evaluates unreachable instructions, so let's balance the
        // stack imbalance as the last instruction that is jumped over
        w.InsertAfter(declareVar_ResultVar, w.Create(OpCodes.Pop));

        w.InsertBranchOver(ldarg0_RunOriginalParam, declareVar_RunOriginalParam);

        w.InsertBeforeStealLabels(
            declareVar_RunOriginalParam.Next,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(GetRunOriginalParamVar)
        );
        w.InsertAfter(declareVar_RunOriginalParam, w.Create(OpCodes.Pop));

        w.InsertBranchOverIfTrue(
            start_RunOriginalParamLogic,
            end_RunOriginalParamLogic,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(RunOriginalParamLogic)
        );

        w.InsertAfter(
            end_RetLogic,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(ReturnLogic)
        );

        HarmonyXInterop.anyFailed = false;
    }

    static void ILHook_HarmonyManipulator_WritePostfixes(ILManipulationInfo info)
    {
        HarmonyXInterop.anyFailed = true;
        ILWeaver w = new(info);

        Instruction ldarg0_ResultVar = null!;
        Instruction ldarg0_RunOriginalParam = null!;
        Instruction declareVar_ResultVar = null!;
        Instruction declareVar_RunOriginalParam = null!;
        Instruction emitStloc = null!;
        Instruction startLabel_1 = null!;
        Instruction startLabel_2 = null!;
        Instruction ilEmitterField = null!;

        var result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.ResultVar)),
            x => x.MatchLdloc(out _),
            x => x.MatchLdtoken(out _),
            x => x.MatchCall(out _),
            x => x.MatchCall(out _),
            x => x.MatchBrtrue(out _),
            x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_ResultVar, x),
            x => x.MatchLdfld(out _) && w.SetInstructionTo(ref ilEmitterField, x),
            x => x.MatchLdloc(out _),
            x =>
                x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                && w.SetInstructionTo(ref declareVar_ResultVar, x)
        );

        if (!result.IsValid)
        {
            // Let's try for older HarmonyX versions
            result = w.MatchRelaxed(
                x => x.MatchLdarg(0),
                x => x.MatchLdfld(out _),
                x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.ResultVar)),
                x => x.MatchLdloc(out _),
                x => x.MatchLdtoken(out _),
                x => x.MatchCall(out _),
                x => x.MatchBeq(out _),
                x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_ResultVar, x),
                x => x.MatchLdfld(out _) && w.SetInstructionTo(ref ilEmitterField, x),
                x => x.MatchLdloc(out _),
                x =>
                    x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                    && w.SetInstructionTo(ref declareVar_ResultVar, x)
            );

            if (!result.IsValid)
            {
                patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
                return;
            }
        }

        result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld<HarmonyManipulator>(nameof(HarmonyManipulator.RunOriginalParam)),
            x => x.MatchLdarg(0) && w.SetInstructionTo(ref ldarg0_RunOriginalParam, x),
            x => x.MatchLdfld(out _),
            x => x.MatchLdtoken(out _),
            x => x.MatchCall(out _),
            x =>
                x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.DeclareVariable))
                && w.SetInstructionTo(ref declareVar_RunOriginalParam, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        result = w.MatchRelaxed(
            x =>
                x.MatchAnd() /* new HarmonyX */
                || x.MatchLdloc(out _), /* old HarmonyX */
            x => x.MatchBrfalse(out _),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchLdsfld(typeof(OpCodes), nameof(OpCodes.Stloc)),
            x => x.MatchLdloc(out _),
            x =>
                x.MatchCallvirt<ILEmitter>(nameof(ILEmitter.Emit))
                && w.SetInstructionTo(ref emitStloc, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        result = w.MatchRelaxed(
            x => x.MatchStloc(out _),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchCallvirt(out _),
            x => x.MatchStloc(out _) && w.SetInstructionTo(ref startLabel_1, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, "3: " + result.FailureMessage);
            return;
        }

        result = w.MatchRelaxed(
            x => x.MatchLdloc(0),
            x => x.MatchCallvirt(out _),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(out _),
            x => x.MatchCallvirt(out _),
            x => x.MatchStloc(out _) && w.SetInstructionTo(ref startLabel_2, x)
        );

        if (!result.IsValid)
        {
            patchManager.Log(MonoDetourLogger.LogChannel.Error, "4: " + result.FailureMessage);
            return;
        }

        w.InsertBranchOver(ldarg0_ResultVar, declareVar_ResultVar);

        w.InsertBeforeStealLabels(
            declareVar_ResultVar.Next,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(GetRetVar)
        );
        w.InsertAfter(declareVar_ResultVar, w.Create(OpCodes.Pop));

        w.InsertBranchOver(ldarg0_RunOriginalParam, declareVar_RunOriginalParam);

        w.InsertBeforeStealLabels(
            declareVar_RunOriginalParam.Next,
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(GetRunOriginalParamVar)
        );
        w.InsertAfter(declareVar_RunOriginalParam, w.Create(OpCodes.Pop));

        var labels = w.DeclareVariable(typeof(List<ILEmitter.Label>));

        w.InsertAfter(
            emitStloc,
            w.Create(OpCodes.Ldloca, labels),
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(AddStlocToLabelList)
        );

        w.InsertAfter(
            startLabel_1,
            w.Create(OpCodes.Ldloca, labels),
            w.Create(OpCodes.Ldloc, startLabel_1.Operand),
            w.CreateCall(AddToLabelList)
        );

        w.InsertAfter(
            startLabel_2,
            w.Create(OpCodes.Ldloca, labels),
            w.Create(OpCodes.Ldloc, startLabel_2.Operand),
            w.CreateCall(AddToLabelList)
        );

        w.InsertBeforeStealLabels(
            w.Last,
            w.Create(OpCodes.Ldloca, labels),
            w.Create(OpCodes.Ldarg_0),
            w.Create(OpCodes.Ldfld, ilEmitterField.Operand),
            w.CreateCall(LabelsToMonoDetourPostfixes)
        );

        HarmonyXInterop.anyFailed = false;
    }

    static void AddStlocToLabelList(ref List<ILEmitter.Label>? labels, ILEmitter il)
    {
        labels ??= [];
        var label = il.DeclareLabelFor(il.IL.Body.Instructions[^2]);
        labels.Add(label);
    }

    static void AddToLabelList(ref List<ILEmitter.Label>? labels, ILEmitter.Label label)
    {
        labels ??= [];
        labels.Add(label);
    }

    static void LabelsToMonoDetourPostfixes(ref List<ILEmitter.Label>? labels, ILEmitter il)
    {
        if (labels is null)
            return;

        foreach (var label in labels)
        {
            var info = HookTargetRecords.GetHookTargetInfo(il.IL.Body.Method);
            info.PostfixInfo.FirstPostfixInstructions.Add(label.instruction);
        }
    }

    static VariableDefinition? GetRetVar(ILEmitter ilEmitter)
    {
        var method = ilEmitter.IL.Body.Method;
        var hookTargetInfo = HookTargetRecords.GetHookTargetInfo(method);
        return hookTargetInfo.ReturnValue;
    }

    static VariableDefinition? GetRunOriginalParamVar(ILEmitter ilEmitter)
    {
        var method = ilEmitter.IL.Body.Method;
        var hookTargetInfo = HookTargetRecords.GetHookTargetInfo(method);
        return hookTargetInfo.PrefixInfo.ControlFlow;
    }

    static bool RunOriginalParamLogic(ILEmitter il)
    {
        var method = il.IL.Body.Method;
        var hookTargetInfo = HookTargetRecords.GetHookTargetInfo(method);

        if (hookTargetInfo.PrefixInfo.ControlFlowImplemented)
        {
            return true;
        }

        hookTargetInfo.PrefixInfo.SetControlFlowImplemented();
        return false;
    }

    static void ReturnLogic(ILEmitter il)
    {
        var method = il.IL.Body.Method;
        var info = HookTargetRecords.GetHookTargetInfo(method);

        if (info.PostfixInfo.FirstPostfixInstructions.FirstOrDefault() is null)
        {
            return;
        }

        foreach (var postfix in info.PostfixInfo.FirstPostfixInstructions)
        {
            if (!il.IL.Body.Instructions.Contains(postfix))
                continue;

            il.Emit(OpCodes.Br, il.DeclareLabelFor(postfix));
            return;
        }

        patchManager.Log(
            MonoDetourLogger.LogChannel.Warning,
            $"While applying HarmonyX Prefixes: "
                + "No postfix labels found despite postfixes being applied on the method. "
                + $"Postfixes might not run on method '{il.IL.Body.Method}'. "
        );
    }
}
