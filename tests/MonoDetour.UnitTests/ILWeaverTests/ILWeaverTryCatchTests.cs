using MonoDetour.Cil;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static partial class ILWeaverTryCatchTests
{
    static bool caught = false;

    [Fact]
    public static void CanWriteTryCatch()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(new Action(Throw).Method, WriteTryCatch);

        Assert.False(caught);
        Throw();
        Assert.True(caught);
    }

    static void WriteTryCatch(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        w.HandlerCreateCatch(null, out var handler)
            .HandlerSetTryStart(w.First, handler)
            .HandlerSetTryEnd(w.Last, handler)
            .InsertAfter(w.Last, w.CreateCall(PrintException))
            .HandlerSetHandlerEnd(w.Last, handler)
            .HandlerApply(handler)
            .InsertAfter(w.Last, w.Create(OpCodes.Ret));
    }

    static void PrintException(Exception exception)
    {
        Helpers.ThrowIfArgumentNull(exception);
        caught = true;
    }

    static void Throw() => throw new NotImplementedException();
}
