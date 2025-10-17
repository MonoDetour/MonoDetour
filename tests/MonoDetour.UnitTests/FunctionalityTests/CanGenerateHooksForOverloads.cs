namespace MonoDetour.UnitTests.FunctionalityTests;

file static class CanHookOverloadsTests
{
    // This fails the build if the source generator fails.
    // No need to call this method.
    static void CanHookOverloads()
    {
        Overloaded.Postfix(FirstPostfix, applyByDefault: false);
        Overloaded_System_Int32.Postfix(SecondPostfix, applyByDefault: false);
        Overloaded_Array_System_Int32.Postfix(ThirdPostfix, applyByDefault: false);
        Overloaded_Array_System_Int32_Array_System_Boolean_System_Boolean.Postfix(
            FourthPostfix,
            applyByDefault: false
        );
    }

    static void FirstPostfix(LibraryMethods self)
    {
        throw new NotImplementedException();
    }

    static void SecondPostfix(LibraryMethods self, ref int num)
    {
        throw new NotImplementedException();
    }

    private static void ThirdPostfix(LibraryMethods self, ref int[] nums)
    {
        throw new NotImplementedException();
    }

    private static void FourthPostfix(
        LibraryMethods self,
        ref int[] nums,
        ref bool[] bools,
        ref bool flag
    )
    {
        throw new NotImplementedException();
    }
}
