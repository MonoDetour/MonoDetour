namespace MonoDetour.UnitTests.FunctionalityTests;

public static class CanHookLocalMethods
{
    // If this builds on Debug and Release, it works
    public static void AnalyzeOptionalParam()
    {
        _HasLocalMethod_g__LocalMethod_21_0.Prefix(Prefix_LocalMethod);
    }

    private static void Prefix_LocalMethod()
    {
        throw new NotImplementedException();
    }
}
