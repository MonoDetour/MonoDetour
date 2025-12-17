using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Cil;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour.Interop.RuntimeDetour;

static class LegacyILHookAntiExploder
{
    static readonly MonoDetourManager antiExploderManager = new(
        typeof(LegacyILHookAntiExploder).Assembly.GetName().Name!
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
            if (!MonoModVersion.IsReorg)
            {
                InitHookLegacy();

                // Because we don't have test for legacy MonoMod, we'll build them
                // into MonoDetour directly when built on Debug configuration.
#if DEBUG
                DebugTestLegacyILHookAntiExploder();
#endif
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

    static void InitHookLegacy()
    {
        var type = typeof(ILHook);

        var target =
            type.GetMethod(nameof(ILHook.Apply))
            ?? throw new NullReferenceException("Method 'Apply' not found.");

        if (ILHookDMDManipulation.legacyContextRefreshMethod is null)
            throw new NullReferenceException("ILHook.Context.Refresh is null");

        antiExploderManager.ILHook(target, TryCatchILHookApply);
    }

    static void TryCatchILHookApply(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        var result = w.MatchRelaxed(x =>
            x.MatchCallvirt(ILHookDMDManipulation.legacyContextRefreshMethod!) && w.SetCurrentTo(x)
        );

        if (!result.IsValid)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                "MonoDetour's ILHook.Apply anti-exploder failed to be applied. "
                    + $"Please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'"
            );
            MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Error, result.FailureMessage);
            return;
        }

        w.HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
            null,
            w.Create(OpCodes.Ldarg_0),
            w.CreateCall(UndoFailedILHook)
        );
    }

    static void UndoFailedILHook(Exception ex, ILHook ilHook)
    {
        if (!ilHook.IsValid)
            return;

        try
        {
            ilHook.Undo();
        }
        catch (Exception ex2)
        {
            throw new Exception("Undoing ILHook failed: " + ex2, ex);
        }

        throw new Exception("ILHook failed to apply and was disabled.", ex);
    }

#if DEBUG
    static void DebugTestLegacyILHookAntiExploder()
    {
        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.Warning,
            $"[Debug] Test '{nameof(DebugTestLegacyILHookAntiExploder)}' has started..."
        );

        try
        {
            antiExploderManager.ILHook(
                TestReturnTrue,
                info =>
                {
                    throw new NotImplementedException("Test: throwing.");
                }
            );
        }
        catch (Exception ex)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Warning,
                "[Debug] Caught failed ILHook as expected: " + ex.Message
            );
        }

        antiExploderManager.ILHook(
            TestReturnTrue,
            info =>
            {
                ILWeaver w = new(info);
                w.InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4_0), w.Create(OpCodes.Ret));
            }
        );

        if (TestReturnTrue())
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                $"[Debug] Test '{nameof(DebugTestLegacyILHookAntiExploder)}' failed!"
            );
        }
        else
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Warning,
                $"[Debug] Test '{nameof(DebugTestLegacyILHookAntiExploder)}' succeeded!"
            );
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool TestReturnTrue() => true;
#endif
}
