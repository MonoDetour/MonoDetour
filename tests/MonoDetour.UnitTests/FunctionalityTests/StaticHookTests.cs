namespace MonoDetour.UnitTests.FunctionalityTests;

public static class StaticHookTests
{
    static bool ran;

    [Fact]
    static void CanHookStaticMethods()
    {
        using var m = DefaultMonoDetourManager.New();

        StaticMethod.Prefix(Prefix_StaticMethod);

        LibraryMethods.StaticMethod();
        Assert.True(ran);
    }

    private static void Prefix_StaticMethod()
    {
        ran = true;
    }
}
