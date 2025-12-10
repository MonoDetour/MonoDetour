using MonoDetour.Cil.Analysis;

namespace MonoDetour.UnitTests.FunctionalityTests;

public static class InstructionInvalidCastTests
{
    [Fact]
    static void DoesNotThrowOnInvalidCast()
    {
        using var dmd = new DynamicMethodDefinition("InvalidCast", null, []);
        var il = dmd.GetILProcessor();

        var instruction = il.Create(OpCodes.Nop);
        instruction.OpCode = OpCodes.Switch;
        instruction.Operand = null;
        il.Append(instruction);

        instruction = il.Create(OpCodes.Nop);
        instruction.OpCode = OpCodes.Switch;
        instruction.Operand = new Instruction?[] { null };
        il.Append(instruction);

        instruction = il.Create(OpCodes.Nop);
        instruction.OpCode = OpCodes.Switch;
        instruction.Operand = 1;
        il.Append(instruction);

        instruction = il.Create(OpCodes.Nop);
        instruction.OpCode = OpCodes.Br;
        instruction.Operand = null;
        il.Append(instruction);

        instruction = il.Create(OpCodes.Nop);
        instruction.OpCode = OpCodes.Br;
        instruction.Operand = 1;
        il.Append(instruction);

        // Would throw right here if it were to.
        var msg = il
            .Body.CreateInformationalSnapshotEvaluateAll()
            .AnnotateErrors()
            .ToErrorMessageString();

        // MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Error, msg);
    }
}
