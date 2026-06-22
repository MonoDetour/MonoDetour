using System.Diagnostics;

namespace MonoDetour.UnitTests.Examples.Basic;

public static class ReplaceInstruction
{
    /*
    In this example, we are going to fix the mistake in this method:

    public static bool IsAboveZero(float num)
    {
        if (num >= 0)
            return true;
        else
            return false;
    }

    the method should return true if 'num' is above 0,
    but mistakenly, it also returns true if 'num' is equal to 0!

    The target method's instructions look like this:

    IL_0000: ldarg.0            load arg (float)num
    IL_0001: ldc.r4 0.0         load (float)0
    IL_0006: blt.un.s IL_000a   branch to labelRetFalse if
                                (float)num is less than (float)0

    IL_0008: ldc.i4.1           load (int)1 (equivalent to 'true')
    IL_0009: ret                and return

    IL_000a: ldc.i4.0           load (int)0 (equivalent to 'false')
    IL_000b: ret                and return

    In the instructions, 'blt.un' is used, whose logic is equivalent
    to the less than '<' operator in C#.

    Here's the logic rewritten in C# to closely follow the logic
    and control flow of the instructions:

    public static bool IsAboveZero(float num)
    {
        if (num < 0)
            goto labelReturnFalse;

        return true;

        labelReturnFalse:
        return false;
    }

    In the above snippet we can see that if 'num' is *less than* 0, we return false.
    To make the logic correct, the condition needs to be *less than or equal*.

    The OpCode for this is 'ble.un', which is described as following:

    > Transfers control to a target instruction if the first value is less than or equal
    to the second value, when comparing unsigned integer values or unordered float values.

    https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ble_un

    So our goal is to change the 'blt.un' OpCode with 'ble.un', and then
    the behavior of the IsAboveZero method is fixed.
    */

    static void ILHook_IsAboveZero(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // When matching the blt.un OpCode, we want to store the
        // branch label here for our replacement instruction.
        ILLabel? labelReturnFalse = null!;

        // We perform a sufficiently unique match on the instructions to
        // find the exact location we are looking for, and set the ILWeaver's
        // Current property to the instruction with the blt.un OpCode.
        ILWeaverResult result = w.MatchRelaxed(
            x => x.MatchLdarg(0),
            x => x.MatchLdcR4(0),
            x => x.MatchBltUn(out labelReturnFalse) && w.SetCurrentTo(x)
        );

        // If the match failed, throw.
        result.ThrowIfFailure();

        // We replace the instruction that has blt.un with our instruction.
        w.ReplaceCurrent(w.Create(OpCodes.Ble_Un, labelReturnFalse));
    }

    [Fact]
    public static void TestReplaceInstruction()
    {
        PrepareAndValidateBehaviorForIsAboveZero();

        // Apply our ILHook
        DefaultMonoDetourManager.Instance.ILHook(IsAboveZero, ILHook_IsAboveZero);

        // Ensure that the behavior is now correct
        Assert.True(IsAboveZero(1));
        Assert.False(IsAboveZero(0));
        Assert.False(IsAboveZero(-1));
    }

    private static void PrepareAndValidateBehaviorForIsAboveZero()
    {
        // Manually write the instructions to ensure they are always consistent
        DefaultMonoDetourManager.Instance.ILHook(
            IsAboveZero,
            ILHook_IsAboveZero_WriteInstructions,
            new MonoDetourConfig(priority: 1)
        );

        // Ensure incorrect behavior exists
        Assert.True(IsAboveZero(0));

        // And ensure the method otherwise acts correctly
        Assert.True(IsAboveZero(1));
        Assert.False(IsAboveZero(-1));
    }

    static bool IsAboveZero(float num) => throw new UnreachableException("Placeholder");

    static void ILHook_IsAboveZero_WriteInstructions(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        ILLabel labelRetFalse = w.DefineLabel();

        w.InsertBeforeCurrent(
            // load arg (float)num
            w.Create(OpCodes.Ldarg_0),
            // load (float)0
            w.Create(OpCodes.Ldc_R4, (float)0),
            // branch to labelRetFalse if (float)num is less than (float)0
            w.Create(OpCodes.Blt_Un, labelRetFalse),
            // load (int)1 and return (equivalent to 'true')
            w.Create(OpCodes.Ldc_I4, 1),
            w.Create(OpCodes.Ret),
            // load (int)0 and return (equivalent to 'false')
            w.Create(OpCodes.Ldc_I4, 0).MarkLabel(labelRetFalse),
            w.Create(OpCodes.Ret)
        );
    }
}
