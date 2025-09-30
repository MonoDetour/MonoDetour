namespace MonoDetour.UnitTests.FunctionalityTests;

public static class CanHookFromSubTypes
{
    // If this builds on Debug and Release, it works
    public static void AnalyzeOptionalParam()
    {
        SubType.SubMethod.Prefix(Prefix_SubMethod);
        SubType.SubSubType.SubSubMethod.Prefix(Prefix_SubSubMethod);
    }

    private static void Prefix_SubMethod(LibraryMethods.SubType self) { }

    private static void Prefix_SubSubMethod(LibraryMethods.SubType.SubSubType self) { }
}
