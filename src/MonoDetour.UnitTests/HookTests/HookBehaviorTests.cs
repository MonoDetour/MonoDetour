namespace MonoDetour.UnitTests.HookTests;

public static partial class HookBehaviorTests
{
    private static readonly Queue<int> order = [];
    private static bool iLHook2_emitRetTwiceToForceEarlyReturn;

    [Fact]
    public static void CanRedirectEarlyReturn()
    {
        iLHook2_emitRetTwiceToForceEarlyReturn = false;
        order.Clear();

        Assert.Equal(102, PerformHooks());
        Assert.Equal([1, 2, 3, 4], order);
    }

    [Fact]
    public static void CanReturnEarlyWithDoubleReturn()
    {
        iLHook2_emitRetTwiceToForceEarlyReturn = true;
        order.Clear();

        Assert.Equal(100, PerformHooks());
        Assert.Equal([1], order);
    }

    private static int PerformHooks()
    {
        TakeAndReturnInt.Postfix(Postfix1_2nd_Add1ToNum);
        TakeAndReturnInt.ILHook(ILHook2_1st_Add100ToNum_Returns);
        TakeAndReturnInt.ILHook(ILHook3_3rd_Returns);
        TakeAndReturnInt.Postfix(Postfix4_4th_Add1ToNum);
        TakeAndReturnInt.ILHook(ILHook5_5th_ReturnWithLdarg1);

        var someType = new LibraryMethods();
        var retVal = someType.TakeAndReturnInt(0);
        DefaultMonoDetourManager.Instance.DisposeHooks();

        return retVal;
    }

    private static void Postfix1_2nd_Add1ToNum(ref TakeAndReturnInt.Params args)
    {
        args.number_1 += 1;
        order.Enqueue(2);
    }

    private static void ILHook2_1st_Add100ToNum_Returns(ILContext il)
    {
        ILCursor c = new(il);
        c.EmitDelegate(() =>
        {
            order.Enqueue(1);
        });

        c.Emit(OpCodes.Ldarg_1);
        c.Emit(OpCodes.Ldc_I4, 100);
        c.Emit(OpCodes.Add);
        c.Emit(OpCodes.Starg, 1);
        c.Emit(OpCodes.Ldarg_1);

        c.Emit(OpCodes.Ret);

        if (iLHook2_emitRetTwiceToForceEarlyReturn)
            c.Emit(OpCodes.Ret);
    }

    private static void ILHook3_3rd_Returns(ILContext il)
    {
        ILCursor c = new(il);
        c.Index -= 1;
        c.EmitDelegate(() =>
        {
            order.Enqueue(3);
        });

        // This ret will be at the end of the method for the next applied
        // PostfixDetour so there will be 2 ret instructions for it,
        // so this also tests that 2 ret instructions at the end of a method
        // are turned into branches and won't force a return.
        c.Emit(OpCodes.Ret);
    }

    private static void Postfix4_4th_Add1ToNum(ref TakeAndReturnInt.Params args)
    {
        args.number_1 += 1;
        order.Enqueue(4);
    }

    private static void ILHook5_5th_ReturnWithLdarg1(ILContext il)
    {
        ILCursor c = new(il);
        c.Index -= 1;

        c.Emit(OpCodes.Pop);
        c.Emit(OpCodes.Ldarg_1);
    }
}
