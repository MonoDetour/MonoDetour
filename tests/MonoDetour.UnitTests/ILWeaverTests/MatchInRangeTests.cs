namespace MonoDetour.UnitTests.ILWeaverTests;

public static class MatchInRangeTests
{
    [Fact]
    public static void CanMatchInRange()
    {
        using var dmd = new DynamicMethodDefinition("MatchInRangeTest", null, []);

        var il = dmd.GetILProcessor();
        for (int i = 0; i < 3; i++)
        {
            il.Emit(OpCodes.Ldc_I4, i);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldstr, "hello");
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Pop);
        }
        il.Emit(OpCodes.Ret);

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);

            Instruction start = null!;
            Instruction end = null!;

            w.MatchStrict(
                    x => x.MatchLdcI4(0),
                    x => x.MatchNop(),
                    x => x.MatchNop() && w.SetInstructionTo(ref start, x)
                )
                .ThrowIfFailure();

            w.MatchStrict(
                    x => x.MatchPop() && w.SetInstructionTo(ref end, x),
                    x => x.MatchPop(),
                    x => x.MatchRet()
                )
                .ThrowIfFailure();

            w.MatchInRangeStrict(
                    start,
                    end,
                    x => x.MatchNop(),
                    x => x.MatchNop(),
                    x => x.MatchLdstr("hello"),
                    x => x.MatchPop(),
                    x => x.MatchPop()
                )
                .ThrowIfFailure();

            // Works if didn't throw.
        });
    }
}
