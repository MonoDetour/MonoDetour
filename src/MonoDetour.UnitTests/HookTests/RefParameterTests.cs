using System.Diagnostics.CodeAnalysis;

namespace MonoDetour.UnitTests.HookTests;

public static partial class RefParametersTests
{
    private static readonly Queue<int> order = [];

    [Fact]
    public static void CanHookMethodWithReferenceTypeParams()
    {
        var m = new MonoDetourManager();
        var lib = new LibraryMethods();

        string? val = null;
        Assert.Equal("hello", lib.ReturnNullStringAsHello(val));

        ReturnNullStringAsHello.Prefix(Prefix_ReturnStringToHooked, m);

        val = "not null";
        Assert.Equal("hooked", lib.ReturnNullStringAsHello(val));

        m.DisposeHooks();
    }

    private static void Prefix_ReturnStringToHooked(ref ReturnNullStringAsHello.Params args)
    {
        args.value_1 = "hooked";
    }

    [Fact]
    public static void CanHookMethodWithRefParams()
    {
        var m = new MonoDetourManager();
        var lib = new LibraryMethods();

        string? val = null;
        lib.SetNullStringToHello(ref val);
        Assert.Equal("hello", val);

        SetNullStringToHello.Prefix(Prefix_SetStringToHooked, m);

        val = null;
        lib.SetNullStringToHello(ref val);
        Assert.Equal("hooked", val);

        m.DisposeHooks();
    }

    private static void Prefix_SetStringToHooked(ref SetNullStringToHello.Params args)
    {
        args.value_1 = "hooked";
    }

    [Fact]
    public static void CanHookMethodWithOutParams()
    {
        var m = new MonoDetourManager();
        var lib = new LibraryMethods();

        // This test is somewhat useless since the out param
        // needs to be initialized by the target method so
        // we can't really override it in a prefix.

        Assert.True(lib.TryGetThis(true, out var res));
        Assert.True(res is not null);
        Assert.False(lib.TryGetThis(false, out res));
        Assert.True(res is null);

        TryGetThis.Prefix(Prefix_TryGetThis, m);

        Assert.True(lib.TryGetThis(false, out res));
        Assert.True(res is not null);
        Assert.False(lib.TryGetThis(true, out res));
        Assert.True(res is null);

        m.DisposeHooks();
    }

    private static void Prefix_TryGetThis(ref TryGetThis.Params args)
    {
        args.getThis_1 = !args.getThis_1;
    }
}
