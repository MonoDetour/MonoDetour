using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class MatchTests
{
    [Fact]
    public static void CanMatchProperly()
    {
        using var dmd = new DynamicMethodDefinition("MatchTest", typeof(void), []);
        {
            var il = dmd.GetILProcessor();
            var instrs = dmd.Definition.Body.Instructions;

            il.Emit(Op.Nop);
            il.Emit(Op.Pop);
            il.Emit(Op.Nop);
            il.Emit(Op.Ret);
        }

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);

            Instruction firstNop = null!;

            w.MatchStrict(
                    x => x.MatchNop() && w.SetInstructionTo(ref firstNop, x),
                    x => x.MatchPop()
                )
                .ThrowIfFailure();

            w.InsertAfter(firstNop, w.Create(Op.Ldc_I4_0));

            // MonoDetourLogger.Log(
            //     MonoDetourLogger.LogChannel.Error,
            //     w.MatchStrict(
            //         x => x.MatchNop(),
            //         x =>
            //             x.MatchCall(out var y)
            //             && y.Name.StartsWith("Foo", StringComparison.InvariantCulture)
            //             && w.SetCurrentTo(x)
            //     ).FailureMessage!
            // );

            // MonoDetourLogger.Log(
            //     MonoDetourLogger.LogChannel.Error,
            //     w.MatchStrict(x => x.MatchNop()).FailureMessage!
            // );
        });

        var method = dmd.Generate().CreateDelegate<Action>();

        // Throws if the match matched the last nop
        method();
    }

    [Fact]
    public static void CanMatchProperly2()
    {
        using var dmd = new DynamicMethodDefinition("MatchTest2", typeof(void), []);
        {
            var il = dmd.GetILProcessor();
            var instrs = dmd.Definition.Body.Instructions;

            il.Emit(Op.Ldc_I4_0);
            il.Emit(Op.Nop);
            il.Emit(Op.Ldc_I4_0);
            il.Emit(Op.Nop);
            il.Emit(Op.Pop);
            il.Emit(Op.Pop);
            il.Emit(Op.Ret);
        }

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);
            w.LogFilter = MonoDetourLogger.LogChannel.IL;

            // The first ldc_i4_0 and Nop should not cause this to fail.
            w.MatchStrict(x => x.MatchLdcI4(0), x => x.MatchNop(), x => x.MatchPop())
                .ThrowIfFailure();

            // Unsatisfiable match, should not result in infinite loop
            _ = w.MatchStrict(x => x.MatchAnd());
        });

        var method = dmd.Generate().CreateDelegate<Action>();
        method();
    }
}
