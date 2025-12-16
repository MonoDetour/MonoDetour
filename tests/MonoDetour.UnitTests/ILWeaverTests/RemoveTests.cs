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

            w.InsertBeforeCurrent(
                w.Create(Op.Ldc_I4_1),
                w.Create(Op.Ldc_I4_0).Get(out var start),
                w.Create(Op.Throw),
                w.Create(Op.Pop).Get(out var handlerStart),
                w.Create(Op.Ret).Get(out var ret)
            );

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
