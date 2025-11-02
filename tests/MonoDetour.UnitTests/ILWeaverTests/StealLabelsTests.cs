using Op = System.Reflection.Emit.OpCodes;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class StealLabelsTests
{
    static int num;

    [Fact]
    public static void CanStealLabelsProperly()
    {
        using var dmd = new DynamicMethodDefinition("StealLabelsTest", typeof(void), []);
        {
            var il = dmd.GetILGenerator();
            var instrs = dmd.Definition.Body.Instructions;

            var label = il.DefineLabel();

            il.Emit(Op.Br, label);
            il.MarkLabel(label);
            il.Emit(Op.Call, ((Delegate)DoubleNum).Method);
            il.Emit(Op.Ret);
        }

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);

            w.MatchStrict(
                    x => x.MatchBr(out _),
                    x => x.MatchCall(out _) && w.SetCurrentTo(x),
                    x => x.MatchRet()
                )
                .ThrowIfFailure();

            // If InsertBeforeCurrentStealLabels steals labels for every
            // inserted instruction, w.Create(OpCodes.Br, w.Current)
            // points to w.Create(OpCodes.Ret).

            w.InsertBeforeCurrentStealLabels(
                w.CreateCall(IncrementNum),
                w.Create(OpCodes.Br, w.Current),
                w.Create(OpCodes.Ret)
            );

            // MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Warning, info.ToString());
        });

        var method = dmd.Generate().CreateDelegate<Action>();

        method();

        Assert.Equal(2, num);
    }

    static void IncrementNum()
    {
        if (num is >= 10)
        {
            // This bad path executes if our Create instruction method makes Instructions into
            // ILLabels right away, so InsertBeforeCurrentStealLabels would make this:
            // w.Create(OpCodes.Br, w.Current) point to this: w.CreateCall(IncrementNum).
            throw new InvalidProgramException($"Infinite loop: num is {num}");
        }

        num++;
    }

    static void DoubleNum() => num *= 2;
}
