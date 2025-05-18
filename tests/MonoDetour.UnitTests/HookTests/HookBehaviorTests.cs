using MonoDetour.Cil;

namespace MonoDetour.UnitTests.HookTests;

public static partial class HookBehaviorTests
{
    private static readonly Queue<int> order = [];
    private static bool iLHook2_emitRetTwiceToForceEarlyReturn;
    static readonly Lock _lock = new();

    [Fact]
    public static void CanRedirectEarlyReturn()
    {
        lock (_lock)
        {
            iLHook2_emitRetTwiceToForceEarlyReturn = false;
            order.Clear();

            Assert.Equal(102, PerformHooks());
            Assert.Equal([1, 2, 3, 4], order);
        }
    }

    [Fact]
    public static void CanReturnEarlyWithDoubleReturn()
    {
        lock (_lock)
        {
            iLHook2_emitRetTwiceToForceEarlyReturn = true;
            order.Clear();

            Assert.Equal(100, PerformHooks());
            Assert.Equal([1], order);
        }
    }

    private static int PerformHooks()
    {
        var m = DefaultMonoDetourManager.New();

        TakeAndReturnInt.ILHook(ILHook1_5th_ReturnWithLdarg1, manager: m);
        TakeAndReturnInt.Postfix(Postfix2_4th_Add1ToNum, manager: m);
        TakeAndReturnInt.ILHook(ILHook3_3rd_Returns, manager: m);
        TakeAndReturnInt.ILHook(ILHook4_1st_Add100ToNum_Returns, manager: m);
        TakeAndReturnInt.Postfix(Postfix5_2nd_Add1ToNum, manager: m);

        var someType = new LibraryMethods();
        var retVal = someType.TakeAndReturnInt(0);
        m.DisposeHooks();

        return retVal;
    }

    private static void Postfix5_2nd_Add1ToNum(
        LibraryMethods self,
        ref int number,
        ref int returnValue
    )
    {
        number += 1;
        order.Enqueue(2);
    }

    private static void ILHook4_1st_Add100ToNum_Returns(ILManipulationInfo info)
    {
        ILCursor c = new(info.ManipulationContext);
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

    private static void ILHook3_3rd_Returns(ILManipulationInfo info)
    {
        ILCursor c = new(info.ManipulationContext);
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

    private static void Postfix2_4th_Add1ToNum(
        LibraryMethods self,
        ref int number,
        ref int returnValue
    )
    {
        number += 1;
        order.Enqueue(4);
    }

    private static void ILHook1_5th_ReturnWithLdarg1(ILManipulationInfo info)
    {
        ILCursor c = new(info.ManipulationContext);
        c.Index -= 1;

        c.Emit(OpCodes.Pop);
        c.Emit(OpCodes.Ldarg_1);
    }
}
