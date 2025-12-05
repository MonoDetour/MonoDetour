using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HarmonyLib.Internal.Patching;
using HarmonyLib.Internal.Util;
using HarmonyLib.Public.Patching;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Interop.HarmonyX;

static class EnforcePersistentInstructions
{
    internal static readonly MonoDetourManager persistentManager = new(HarmonyXInterop.ManagerName);

    internal static void Init()
    {
        var target = typeof(HarmonyManipulator).GetMethod(
            nameof(HarmonyManipulator.MakeReturnLabel),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        if (target is null)
        {
            persistentManager.Log(
                MonoDetourLogger.LogChannel.Error,
                "HarmonyManipulator.MakeReturnLabel doesn't exist!"
            );
            return;
        }

        persistentManager.ILHook(target, ILHook_HarmonyManipulator_MakeReturnLabel);
    }

    // What we do here is match the only loop in the method,
    // and we conditionally skip the whole logic of the loop.
    // This is because we don't want HarmonyX redirecting our
    // "persistent" return labels written by HardReturn ControlFlowPrefix hooks.
    private static void ILHook_HarmonyManipulator_MakeReturnLabel(ILManipulationInfo info)
    {
        // Set to false at end of method if everything ok.
        HarmonyXInterop.anyFailed = true;
        ILWeaver w = new(info);

        Instruction loopStartOnLocSet = null!;
        Instruction loopEnd = null!;
        int locInstruction = 0;

        var result = w.MatchRelaxed(
            x => x.MatchLdloc(out _),
            x => x.MatchLdfld(out _),
            x => x.MatchLdloc(out locInstruction),
            x => x.MatchCallvirt(out _) && w.SetInstructionTo(ref loopEnd, x),
            x => x.MatchLdloc(out _),
            x => x.MatchCallvirt<IEnumerator>(nameof(IEnumerator.MoveNext)),
            x => x.MatchBrtrue(out _)
        );

        if (!result.IsValid)
        {
            persistentManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        // This stloc is 'Current' from the IEnumerator
        result = w.MatchRelaxed(
            x => x.MatchCallvirt(out _),
            x => x.MatchStloc(locInstruction) && w.SetInstructionTo(ref loopStartOnLocSet, x)
        );

        if (!result.IsValid)
        {
            persistentManager.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        var hookTargetInfoVar = w.DeclareVariable(typeof(HookTargetRecords.HookTargetInfo));

        w.InsertBefore(
            w.First,
            w.Create(OpCodes.Ldarg_0),
            w.Create<HarmonyManipulator>(OpCodes.Ldfld, "il"),
            w.CreateCall(GetHookTargetInfo),
            w.Create(OpCodes.Stloc, hookTargetInfoVar)
        );

        w.InsertBranchOverIfTrue(
            loopStartOnLocSet.Next,
            loopEnd,
            w.Create(OpCodes.Ldloc, locInstruction),
            w.Create(OpCodes.Ldloc, hookTargetInfoVar),
            w.CreateCall(IsPersistent)
        );

        HarmonyXInterop.anyFailed = false;
    }

    static HookTargetRecords.HookTargetInfo GetHookTargetInfo(ILEmitter il) =>
        HookTargetRecords.GetHookTargetInfo(il.IL.Body.Method);

    static bool IsPersistent(Instruction instruction, HookTargetRecords.HookTargetInfo info) =>
        info.IsPersistentInstruction(instruction);
}
