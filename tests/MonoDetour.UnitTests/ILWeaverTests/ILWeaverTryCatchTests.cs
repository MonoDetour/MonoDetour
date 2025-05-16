namespace MonoDetour.UnitTests.ILWeaverTests;

public static partial class ILWeaverTryCatchTests
{
    [Fact]
    public static void CanWriteTryCatch()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(new Action(Throw).Method, WriteTryCatch);
        Throw();
    }

    static void WriteTryCatch(ILContext il)
    {
        ILWeaver w = new(il);
        Console.WriteLine(il);

        w.HandlerCreate(ExceptionHandlerType.Catch, null, out var handler)
            .HandlerSetTryStart(w.First, handler)
            .HandlerSetTryEnd(w.Last, handler)
            .InsertAfter(w.Last, w.CreateCall(PrintException))
            .HandlerSetCatchEnd(w.Last, handler)
            .HandlerApply(handler)
            .InsertAfter(w.Last, w.Create(OpCodes.Ret));

        il.Method.RecalculateILOffsets();
        Console.WriteLine(il);
    }

    static void PrintException(Exception exception) => Console.WriteLine(exception.ToString());

    static void Throw() => throw new NotImplementedException();
}
