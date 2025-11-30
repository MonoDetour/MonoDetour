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
}
