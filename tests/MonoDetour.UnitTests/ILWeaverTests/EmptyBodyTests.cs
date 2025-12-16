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
            w.RemoveCurrentAndShiftLabels();

            // Test 3: Removing the only instruction.
            // ILWeaver should have added a temporary instruction so this would be last.
            w.RemoveCurrentAndShiftLabels();

            // This is the way to actually remove all instructions while in a ILHook manipulator.
            w.Instructions.Clear();

            // ...and testing the same with other insert methods.
            w.InsertAfterCurrent(w.Create(Op.Nop), w.Create(Op.Ret));
            w.RemoveCurrentAndShiftLabels();
            w.RemoveCurrentAndShiftLabels();
            w.InsertBeforeCurrentStealLabels(w.Create(Op.Nop), w.Create(Op.Ret));
        });

        var method = dmd.Generate().CreateDelegate<Action>();

        method();
    }

    [Fact]
    public static void CanInsertCorrectly()
    {
        using var dmd = new DynamicMethodDefinition("CanInsertCorrectlyTest", null, []);

        dmd.Definition.ILWeave(info =>
        {
            ILWeaver w = new(info);

            w.InsertBeforeCurrent(w.Create(Op.Ldc_I4_0), w.Create(Op.Pop));

            // This should be the last instruction.
            w.InsertBeforeCurrent(w.Create(Op.Ret));
        });

        var method = dmd.Generate().CreateDelegate<Action>();

        method();
    }
}
