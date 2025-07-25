using System.Linq;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the start of the target method.
/// </summary>
public class PrefixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyMonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il)
    {
        HookTargetRecords.HookTargetInfo info = HookTargetRecords.GetHookTargetInfo(il);
        var originalInstructions = HookTargetRecords.GetOriginalInstructions(il.Method);
        ILWeaver w = new(new(il, Hook.Target, originalInstructions));

        bool modifiesReturnValue = Hook.ModifiesControlFlow() && info.ReturnValue is not null;

        w.HandlerCreateCatch(null, out var handler);
        w.DefineAndMarkLabelToFutureNextInsert(out var tryStart);
        w.HandlerSetTryStart(tryStart, handler);

        if (modifiesReturnValue)
            w.EmitParamsAndReturnValueBeforeCurrent(info.ReturnValue!, Hook);
        else
            w.EmitParamsBeforeCurrent(Hook);

        w.InsertBeforeCurrent(w.Create(OpCodes.Call, Hook.Manipulator));

        if (Hook.ModifiesControlFlow())
            w.InsertBeforeCurrent(w.Create(OpCodes.Stloc, info.PrefixInfo.TemporaryControlFlow));

        w.HandlerSetTryEnd(w.Previous, handler);

        w.EmitReferenceBeforeCurrent(Hook, out _);
        w.InsertBeforeCurrent(w.CreateCall(Utils.DisposeBadHooks));

        w.HandlerSetHandlerEnd(w.Previous, handler);

        if (Hook.ModifiesControlFlow())
        {
            // Evaluate temporary control flow value.
            var setControlFlowSwitch = w.Create(OpCodes.Switch, new object());
            w.InsertBeforeCurrent(
                w.Create(OpCodes.Ldloc, info.PrefixInfo.TemporaryControlFlow),
                setControlFlowSwitch
            );

            w.DefineAndMarkLabelToFutureNextInsert(out var hardReturn);
            // Hack: MonoDetour will not redirect ret instructions if there are two.
            for (int i = 0; i < 2; i++)
            {
                if (info.ReturnValue is not null)
                    w.InsertBeforeCurrent(w.Create(OpCodes.Ldloc, info.ReturnValue));
                w.InsertBeforeCurrent(w.Create(OpCodes.Ret));
            }

            w.DefineAndMarkLabelToFutureNextInsert(out var softReturn);
            w.InsertBeforeCurrent(
                w.Create(OpCodes.Ldc_I4_1),
                w.Create(OpCodes.Stloc, info.PrefixInfo.ControlFlow)
            );

            w.DefineAndMarkLabelToCurrentOrFutureNextInsert(out var none);

            setControlFlowSwitch.Operand = new ILLabel[] { none, softReturn, hardReturn };
        }

        if (!info.PrefixInfo.ControlFlowImplemented)
        {
            // Evaluate actual control flow value.
            info.PrefixInfo.SetControlFlowImplemented();

            w.DefineAndMarkLabelToCurrent(out var none);

            w.InsertBeforeCurrent(
                w.Create(OpCodes.Ldloc, info.PrefixInfo.ControlFlow),
                w.Create(OpCodes.Brfalse, none)
            );

            if (info.ReturnValue is not null)
                w.InsertBeforeCurrent(w.Create(OpCodes.Ldloc, info.ReturnValue));

            var firstPostfix = info.PostfixInfo.FirstPostfixInstructions.FirstOrDefault();
            if (firstPostfix is not null)
                w.InsertBeforeCurrent(w.Create(OpCodes.Br, firstPostfix));
            else
                w.InsertBeforeCurrent(w.Create(OpCodes.Ret));
        }

        w.HandlerApply(handler);

        Hook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                var body = w.Body.CreateInformationalSnapshotJIT().AnnotateErrors();
                return $"Manipulated by Prefix: {Hook.Manipulator.Name}:\n{body}";
            }
        );

        Utils.DebugValidateCILValidatorNoErrors(Hook, w.Body);
    }
}
