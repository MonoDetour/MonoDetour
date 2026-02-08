using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Aot.DetourTypes;

/// <summary>
/// Implements MonoDetour support for an AOT Hook that will run at the start of the target method.
/// </summary>
public class AotPrefixDetour : IAotMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyAotMonoDetourHook AotHook
    {
        get => aotHook;
        set => aotHook = value;
    }

    IReadOnlyAotMonoDetourHook aotHook = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(MethodDefinition method)
    {
        var info = HookTargetRecords.GetHookTargetInfo(method);
        ILWeaver w = new(new(new(method), null, out var onFinish));

        bool modifiesControlFlow = AotHook.ModifiesControlFlow();
        bool modifiesReturnValue = modifiesControlFlow && info.ReturnValue is not null;

        w.InsertBeforeCurrent(
            w.CreateParamsBeforeCurrent(AotHook),
            w.If(modifiesReturnValue)?.Create(OpCodes.Ldloca, info.ReturnValue!),
            w.Create(OpCodes.Call, AotHook.Manipulator)
        );

        if (modifiesControlFlow)
        {
            // Evaluate temporary control flow value.
            var setControlFlowSwitch = w.Create(OpCodes.Switch, new object());
            w.InsertBeforeCurrent(setControlFlowSwitch);

            w.DefineAndMarkLabelToFutureNextInsert(out var hardReturn);
            // Hack: MonoDetour will not redirect ret instructions if there are two.
            // New stuff: For HarmonyX interop, persistent instructions are a thing.
            // MonoDetour should probably just switch to that system?
            for (int i = 0; i < 2; i++)
            {
                if (info.ReturnValue is not null)
                    w.InsertBeforeCurrent(w.Create(OpCodes.Ldloc, info.ReturnValue));
                w.InsertBeforeCurrent(info.MarkPersistentInstruction(w.Create(OpCodes.Ret)));
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

            if (info.PostfixInfo.FirstPostfixInstructions.FirstOrDefault() is null)
                w.InsertBeforeCurrent(w.Create(OpCodes.Ret));
            else
            {
                bool foundPostfix = false;

                foreach (var postfix in info.PostfixInfo.FirstPostfixInstructions)
                {
                    if (!w.Body.Instructions.Contains(postfix))
                        continue;

                    w.InsertBeforeCurrent(w.Create(OpCodes.Br, postfix));
                    foundPostfix = true;
                    break;
                }

                if (!foundPostfix)
                {
                    AotHook.Owner.Log(
                        MonoDetourLogger.LogChannel.Warning,
                        $"While applying Prefix: {AotHook.Manipulator.Name} ({AotHook.Owner.Id}): "
                            + "No postfix labels found despite postfixes being applied on the method. "
                            + $"Postfixes might not run on method '{AotHook.Target}'. "
                    );
                    w.InsertBeforeCurrent(w.Create(OpCodes.Ret));
                }
            }
        }

        onFinish();

        if (AotHook.ModifiesControlFlow())
        {
            info.PrefixInfo.MarkControlFlowPrefixAsApplied();
        }

        AotHook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                var body = w.Body.CreateInformationalSnapshotJIT().AnnotateErrors();
                return $"Manipulated by Prefix: {AotHook.Manipulator.Name} ({AotHook.Owner.Id}):\n{body}";
            }
        );

        // Utils.DebugValidateCILValidatorNoErrors(AotHook, w.Body);
    }
}
