namespace MonoDetour.UnitTests.FunctionalityTests;

public static class HookGenerationTests
{
    [Fact]
    static void CanGenerateHooksForHasGenericInArgumentType()
    {
        _ = HasArgumentWithGenericType.Target() ?? throw new NullReferenceException();
    }
}
