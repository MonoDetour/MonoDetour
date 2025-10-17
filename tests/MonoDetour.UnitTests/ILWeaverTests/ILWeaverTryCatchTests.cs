using System.Runtime.CompilerServices;
using MonoDetour.Cil;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static partial class ILWeaverTryCatchTests
{
    static bool caught = false;

    [Fact]
    public static void CanWriteTryCatch()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(Throw, WriteTryCatch);

        Assert.False(caught);
        Throw();
        Assert.True(caught);
    }

    [Fact]
    public static void CanInsertInstructionAfterTryCatchWithoutInvalidIL()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(HasTryCatchAndReturnFalse, WriteReturnTrue);
        m.ILHook(HasTryCatchAndReturnFalse, WriteThrowInsideTry);

        bool result = HasTryCatchAndReturnFalse();
        Assert.True(result);
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
        MonoMod.Utils.Helpers.ThrowIfArgumentNull(exception);
        caught = true;
    }

    static void Throw() => throw new NotImplementedException();

    private static void WriteReturnTrue(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        w.MatchRelaxed(x => x.MatchLdcI4(0) && w.SetCurrentTo(x));
        w.InsertAfterCurrent(w.Create(OpCodes.Pop), w.Create(OpCodes.Ldc_I4_1));
    }

    private static void WriteThrowInsideTry(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        var exceptionCtor =
            typeof(Exception).GetConstructor([]) ?? throw new NullReferenceException();

        w.CurrentTo(w.Body.ExceptionHandlers.First().TryStart);
        w.InsertBeforeCurrentStealLabels(
            w.Create(OpCodes.Newobj, exceptionCtor),
            w.Create(OpCodes.Throw)
        );
    }

    static bool HasTryCatchAndReturnFalse()
    {
        try
        {
            _ = "doing stuff";
        }
        catch
        {
            _ = "catching stuff";
        }
        finally { }
        return false;
    }
}
