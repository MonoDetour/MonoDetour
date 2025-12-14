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
}
