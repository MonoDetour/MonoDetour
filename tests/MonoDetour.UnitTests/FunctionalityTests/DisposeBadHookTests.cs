namespace MonoDetour.UnitTests.FunctionalityTests;

public static partial class DisposeBadHookTests
{
    [Fact]
    public static void CanDisposeBadHook()
    {
        var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Return1, Postfix_Return10);
        m.Hook<PostfixDetour>(ReturnFoo, Postfix_ReturnBar);

        Assert.Equal(10, Return1());
        Assert.Equal(1, Return1());
        Assert.Equal("foo", ReturnFoo());
    }

    static void Postfix_Return10(ref int returnValue)
    {
        returnValue = 10;
        throw new Exception();
    }

    static int Return1() => 1;

    static void Postfix_ReturnBar(ref string returnValue) => returnValue = "bar";

    static string ReturnFoo() => "foo";
}
