namespace MonoDetour.UnitTests.FunctionalityTests;

public static class ConstructorHookTests
{
    static bool ran;

    [Fact]
    static void CanGenerateCtorHooksProperly()
    {
        using var m = DefaultMonoDetourManager.New();

        Md.MonoDetour.UnitTests.TestLib.TypeWithConstructor._ctor.Prefix(Prefix_Ctor, manager: m);

        _ = new TypeWithConstructor();
        Assert.True(ran);
    }

    private static void Prefix_Ctor(TypeWithConstructor self)
    {
        ran = true;
    }
}
