namespace MonoDetour.UnitTests.FunctionalityTests;

file static class CanHookOverloadsTests
{
    // This fails the build if the source generator fails.
    // No need to call this method.
    static void CanHookOverloads()
    {
        Overloaded.Postfix(FirstPostfix, applyByDefault: false);
        Overloaded_System_Int32.Postfix(SecondPostfix, applyByDefault: false);
    }

    static void FirstPostfix(LibraryMethods self)
    {
        throw new NotImplementedException();
    }

    static void SecondPostfix(LibraryMethods self, ref int num)
    {
        throw new NotImplementedException();
    }
}
