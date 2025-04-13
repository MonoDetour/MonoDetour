using System.Diagnostics.CodeAnalysis;

namespace MonoDetour.UnitTests.HookTests;

public static partial class ReturnValueTests
{
    private static readonly Queue<int> order = [];

    [Fact]
    public static void CanChangeReturnValue()
    {
        var m = new MonoDetourManager();
        m.LogLevel = MonoDetourManager.Logging.Diagnostic;
        var lib = new LibraryMethods();

        Assert.Equal(100, lib.TakeAndReturnInt2(100));

        TakeAndReturnInt2.Postfix(Postfix_TakeAndReturnInt2, m);

        Assert.Equal(150, lib.TakeAndReturnInt2(100));

        m.DisposeHooks();
    }

    private static void Postfix_TakeAndReturnInt2(ref TakeAndReturnInt2.Params args)
    {
        args.returnValue += 50;
    }
}
