namespace MonoDetour.UnitTests.FunctionalityTests;

file static class CanSanitizeRefness
{
    // This fails the build if the source generator fails.
    // No need to call this method.
    static void CanHookOverloads()
    {
        ReturnsTuple.Prefix(Prefix_ReturnsTuple);
        TakesRefDictionary.Prefix(Prefix_TakesRefDictionary);

        // Technically this doesn't belong in this file but eh
        CausesConflictingParameterNames.Postfix(Post);
    }

    private static void Post(
        LibraryMethods self,
        ref bool self1,
        ref bool returnValue1,
        ref bool returnValue
    )
    {
        throw new NotImplementedException();
    }

    private static void Prefix_ReturnsTuple(LibraryMethods self, ref (bool, bool) tuple)
    {
        throw new NotImplementedException();
    }

    private static void Prefix_TakesRefDictionary(
        LibraryMethods self,
        ref Dictionary<bool, bool> dictionary
    )
    {
        throw new NotImplementedException();
    }
}
