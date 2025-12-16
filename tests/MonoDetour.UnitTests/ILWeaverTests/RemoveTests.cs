using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class RemoveTests
{
    [Fact]
    public static void CanRemoveRange()
    {
        using var dmd = new DynamicMethodDefinition("RemoveRangeTest", typeof(bool), []);

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);

            var start = w.Create(Op.Ldc_I4_0);
            var end = w.Create(Op.Throw);
            var handlerStart = w.Create(Op.Pop);
            var ret = w.Create(Op.Ret);

            w.InsertBeforeCurrent(w.Create(Op.Ldc_I4_1), start, end, handlerStart, ret);

            w.HandlerCreateCatch(null, out var handler)
                .HandlerSetTryStart(start, handler)
                .HandlerSetHandlerStart(handlerStart, handler)
                .HandlerSetHandlerEnd(handlerStart, handler)
                .HandlerApply(handler);

            w.RemoveRangeAndShiftLabels(start, ret.Previous);
        });

        var method = dmd.Generate().CreateDelegate<Func<bool>>();

        Assert.True(method());
    }
}
