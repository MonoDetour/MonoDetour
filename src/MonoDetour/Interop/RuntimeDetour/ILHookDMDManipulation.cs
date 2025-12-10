using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace MonoDetour.Interop.RuntimeDetour;

static class ILHookDMDManipulation
{
    internal static readonly ConditionalWeakTable<
        MethodDefinition,
        ReadOnlyCollection<Instruction>
    > s_MethodDefinitionToOriginalInstructions = new();

    internal static readonly ConditionalWeakTable<
        MethodDefinition,
        MethodBase
    > s_MethodDefinitionToOriginalMethod = new();

    static ConstructorInfo dmdConstructor = null!;

    static ILHook getDmdBeforeManipulationHook = null!;

    static readonly MonoDetourManager m = new(
        typeof(ILHookDMDManipulation).Assembly.GetName().Name!
    );

    static bool initialized;

    internal static void InitHook()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        try
        {
            if (MonoModVersion.IsReorg)
            {
                InitHookReorg();
            }
            else
            {
                InitHookLegacy();
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"MonoDetour doesn't seem to support this MonoMod version, "
                    + $"please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'",
                ex
            );
        }
    }

    static void InitHookReorg()
    {
        dmdConstructor =
            typeof(DynamicMethodDefinition).GetConstructor([typeof(DynamicMethodDefinition)])
            ?? throw new NullReferenceException("DMD constructor not found.");

        var type =
            Type.GetType(
                "MonoMod.RuntimeDetour.DetourManager+ManagedDetourState, MonoMod.RuntimeDetour"
            )
            ?? throw new NullReferenceException(
                "Type 'MonoMod.RuntimeDetour.DetourManager+ManagedDetourState, MonoMod.RuntimeDetour' not found."
            );

        var target =
            type.GetMethod("UpdateEndOfChain", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("Method 'UpdateEndOfChain' not found.");

        getDmdBeforeManipulationHook = new(target, GetDMDBeforeManipulation);

        // We now have access to MonoDetour hooks!
        m.ILHook(target, TryCatchAnalyzeCompilationReorg);
    }

    static void InitHookLegacy()
    {
        dmdConstructor =
            typeof(DynamicMethodDefinition).GetConstructor([typeof(MethodBase)])
            ?? throw new NullReferenceException("DMD constructor not found.");

        var type =
            Type.GetType("MonoMod.RuntimeDetour.ILHook+Context, MonoMod.RuntimeDetour")
            ?? throw new NullReferenceException(
                "Type 'MonoMod.RuntimeDetour.ILHook+Context, MonoMod.RuntimeDetour' not found."
            );

        var target =
            type.GetMethod("Refresh")
            ?? throw new NullReferenceException("Method 'Refresh' not found.");

        getDmdBeforeManipulationHook = new(target, GetDMDBeforeManipulation);

        m.ILHook(target, TryCatchAnalyzeCompilationLegacy);
    }

    private static void GetDMDBeforeManipulation(ILContext il)
    {
        ILCursor c = new(il);
        bool found = c.TryGotoNext(MoveType.After, x => x.MatchNewobj(dmdConstructor));
        if (!found)
        {
            Console.WriteLine(il);
            throw new NullReferenceException("DMD construction not found.");
        }

        c.EmitDelegate(BorrowDMD);
    }

    static DynamicMethodDefinition BorrowDMD(DynamicMethodDefinition dmd)
    {
        var definition = dmd.Definition;
        var instructions = definition.Body.Instructions.ToList().AsReadOnly();
        s_MethodDefinitionToOriginalInstructions.Add(definition, instructions);
        s_MethodDefinitionToOriginalMethod.Add(definition, dmd.OriginalMethod);
        return dmd;
    }

    static void TryCatchAnalyzeCompilationReorg(ILManipulationInfo info)
    {
        // IL_00d5: ldloc.0
        // IL_00d6: callvirt instance class [System.Runtime]System.Reflection.MethodInfo [MonoMod.Utils]MonoMod.Utils.DynamicMethodDefinition::Generate() /* 0A000077 */
        // IL_00db: stloc.3
        // IL_00dc: call class [MonoMod.Core]MonoMod.Core.Platforms.PlatformTriple [MonoMod.Core]MonoMod.Core.Platforms.PlatformTriple::get_Current() /* 0A00003A */
        // IL_00e1: ldloc.3
        // IL_00e2: callvirt instance void [MonoMod.Core]MonoMod.Core.Platforms.PlatformTriple::Compile(class [System.Runtime]System.Reflection.MethodBase) /* 0A00011B */

        ILWeaver w = new(info);

        int localIndexDmd = 0;
        Instruction tryStart = null!;

        w.MatchRelaxed(
                x => x.MatchLdloc(out localIndexDmd),
                x => x.MatchCallvirt(out _),
                x => x.MatchStloc(out _),
                x => x.MatchCall(out _) && w.SetInstructionTo(ref tryStart, x),
                x => x.MatchLdloc(out _),
                x => x.MatchCallvirt(out _) && w.SetCurrentTo(x)
            )
            .Extract(out var result);

        if (!result.IsValid)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "MonoDetour's invalid IL analysis failed to be applied. "
                    + $"Please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'"
            );
            MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        w.HandlerCreateCatch(typeof(InvalidProgramException), out var handler);
        w.HandlerSetTryStart(tryStart, handler);
        w.HandlerSetTryEnd(w.Current, handler);

        w.InsertAfterCurrent(w.Create(OpCodes.Ldloc, localIndexDmd), w.CreateCall(AnalyzeMethod));

        w.HandlerSetHandlerEnd(w.Current, handler);

        w.HandlerApply(handler);

        // StackSizeAnalyzer.Analyze(info.Context.Body);
    }

    static void TryCatchAnalyzeCompilationLegacy(ILManipulationInfo info)
    {
        // IL_0112: ldloc.s 6
        // IL_0114: callvirt instance class [mscorlib]System.Reflection.MethodInfo [MonoMod.Utils]MonoMod.Utils.DynamicMethodDefinition::Generate() /* 0A000052 */

        ILWeaver w = new(info);

        int localIndexDmd = 0;

        w.MatchRelaxed(
                x => x.MatchLdloc(out localIndexDmd) && w.SetCurrentTo(x),
                x =>
                    x.MatchCallvirt<DynamicMethodDefinition>(
                        nameof(DynamicMethodDefinition.Generate)
                    ),
                x => x.MatchStloc(out _)
            )
            .Extract(out var result);

        if (!result.IsValid)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "MonoDetour's invalid IL analysis failed to be applied. "
                    + $"Please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'"
            );
            MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        var methodBody = w.DeclareVariable(typeof(MethodBody));

        w.InsertAfterCurrent(
            w.Create(OpCodes.Dup),
            w.CreateCall(GetMethodBody),
            w.Create(OpCodes.Stloc, methodBody)
        );

        // IL_0128: ldarg.0
        // IL_0129: ldarg.0
        // IL_012a: ldfld class [mscorlib]System.Reflection.MethodBase MonoMod.RuntimeDetour.ILHook/Context::Method /* 040000DD */
        // IL_012f: ldloc.3
        // IL_0130: ldsflda valuetype MonoMod.RuntimeDetour.DetourConfig MonoMod.RuntimeDetour.ILHook::ILDetourConfig /* 04000051 */
        // IL_0135: newobj instance void MonoMod.RuntimeDetour.Detour::.ctor(class [mscorlib]System.Reflection.MethodBase, class [mscorlib]System.Reflection.MethodBase, valuetype MonoMod.RuntimeDetour.DetourConfig&) /* 0600001F */
        // IL_013a: stfld class MonoMod.RuntimeDetour.Detour MonoMod.RuntimeDetour.ILHook/Context::Detour /* 040000DE */
        // IL_013f: leave.s IL_0148

        Instruction tryStart = null!;

        w.MatchRelaxed(
                x => x.MatchLdarg(out _) && w.SetInstructionTo(ref tryStart, x),
                x => x.MatchLdarg(out _),
                x => x.MatchLdfld(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchLdsflda(out _),
                x => x.MatchNewobj(out _),
                x => x.MatchStfld(out _) && w.SetCurrentTo(x)
            )
            .Extract(out result);

        if (!result.IsValid)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "MonoDetour's invalid IL analysis failed to be applied. "
                    + $"Please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'"
            );
            MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        w.HandlerCreateCatch(typeof(InvalidProgramException), out var handler);
        w.HandlerSetTryStart(tryStart, handler);
        w.HandlerSetTryEnd(w.Current, handler);

        w.InsertAfterCurrent(w.Create(OpCodes.Ldloc, methodBody), w.CreateCall(AnalyzeMethodBody));

        w.HandlerSetHandlerEnd(w.Current, handler);

        w.HandlerApply(handler);
    }

    static MethodBody GetMethodBody(DynamicMethodDefinition dmd) => dmd.Definition.Body;

    static void AnalyzeMethod(InvalidProgramException ex, DynamicMethodDefinition dmd) =>
        AnalyzeMethodBody(ex, dmd.Definition.Body);

    static void AnalyzeMethodBody(InvalidProgramException ex, MethodBody body)
    {
        string analysis;
        try
        {
            analysis = body.CreateInformationalSnapshotJIT()
                .AnnotateErrors()
                .ToErrorMessageString();
        }
        catch (Exception ex2)
        {
            throw new Exception("MonoDetour failed to analyze invalid program: " + ex2, ex);
        }

        throw new InvalidProgramException(analysis, ex);
    }
}
