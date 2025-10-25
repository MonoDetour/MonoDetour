using MonoDetour.Cil;
using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.HookTests;

public static class TryCatchStackTests
{
    static bool caught;

    [Fact]
    public static void CanWrapTryCatch()
    {
        using var dmd = new DynamicMethodDefinition("Throws", typeof(void), []);
        {
            var il = dmd.GetILProcessor();
            var instrs = dmd.Definition.Body.Instructions;

            il.Emit(Op.Nop);

            il.Emit(Op.Ldc_I4_1);
            il.Emit(Op.Ldnull);
            il.Emit(Op.Throw);

            il.Emit(Op.Ldc_I4_2);
            il.Emit(Op.Pop);
            il.Emit(Op.Pop);

            il.Emit(Op.Nop);
            il.Emit(Op.Ret);
        }

        new ILContext(dmd.Definition).Invoke(il =>
        {
            ILManipulationInfo info = new(il, null, il.Instrs.AsReadOnly());
            ILWeaver w = new(info);

            w.MatchStrict(x => x.MatchLdcI4(2) && w.SetCurrentTo(x)).ThrowIfFailure();

            w.HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
                typeof(Exception),
                w.CreateCall(CatchException)
            );
        });

        var method = dmd.Generate();
        method.Invoke(null, []);

        Assert.True(caught);
    }

    static void CatchException(Exception ex)
    {
        caught = true;
    }

    [Fact]
    public static void CanFixStackNotEmptyBeforePrefixTry()
    {
        var m = DefaultMonoDetourManager.New();

        // m.Hook<PrefixDetour>(Stub, Prefix_DoNothing, new(priority: 1));
        m.ILHook(Stub, PassIntFromStartToEnd, new(priority: 0));

        // This throws if the hooks produce invalid IL.
        Stub();
    }

    private static void PassIntFromStartToEnd(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // w.InsertBefore(w.First, w.Create(OpCodes.Ldc_I4_0));

        w.MatchMultipleStrict(
            (match) =>
            {
                match.InsertBeforeCurrentStealLabels(w.Create(Op.Ldc_I4_1), w.Create(Op.Pop));
            },
            x => x.MatchRet() && w.SetCurrentTo(x)
        );
    }

    static void Prefix_DoNothing() { }

    static void Stub() { }
}
