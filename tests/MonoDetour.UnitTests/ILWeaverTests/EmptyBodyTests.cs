using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class EmptyBodyTests
{
    [Fact]
    public static void CanRemoveAndCreateOnlyInstruction()
    {
        using var dmd = new DynamicMethodDefinition("EmptyBodyTest", null, []);

        var il = dmd.GetILProcessor();

        dmd.Definition.ILWeave(info =>
        {
            // None of these should throw:

            // Test 1: Constructing an ILWeaver in an empty body.
            ILWeaver w = new(info);

            // Test 2: Inserting instructions in an empty body.
            w.InsertBeforeCurrent(w.Create(Op.Nop), w.Create(Op.Ret));
            w.RemoveCurrentAndShiftLabels();

            // Test 3: Removing the only instruction.
            w.RemoveCurrentAndShiftLabels();

            // ...and testing the same with other insert methods.
            w.InsertAfter(null!, w.Create(Op.Nop), w.Create(Op.Ret));
            w.RemoveCurrentAndShiftLabels();
            w.RemoveCurrentAndShiftLabels();
            w.InsertBeforeStealLabels(null!, w.Create(Op.Nop), w.Create(Op.Ret));
        });

        var method = dmd.Generate().CreateDelegate<Action>();

        method();
    }

    // [Fact]
    // public static void CanInsertCorrectly()
    // {
    //     using var dmd = new DynamicMethodDefinition("CanInsertCorrectlyTest", null, []);

    //     dmd.Definition.ILWeave(info =>
    //     {
    //         ILWeaver w = new(info);

    //         w.InsertBeforeCurrent(w.Create(Op.Ldc_I4_0), w.Create(Op.Add));
    //         w.InsertBeforeCurrent(w.Create(Op.Ret));
    //         MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Warning, info.ToString());
    //         // w.RemoveRangeAndShiftLabels(w.First, w.Last.Previous);
    //     });

    //     var method = dmd.Generate().CreateDelegate<Action>();

    //     method();
    // }
}
