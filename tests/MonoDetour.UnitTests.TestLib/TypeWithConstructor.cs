namespace MonoDetour.UnitTests.TestLib;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS0414 // Field is assigned but never used
sealed class TypeWithConstructor
{
    static TypeWithConstructor()
    {
        StaticBool = true;
    }

    static readonly bool StaticBool;
    bool InstanceBool;

    public TypeWithConstructor()
    {
        LocalMethod();

        void LocalMethod()
        {
            InstanceBool = true;
        }
    }
}
#pragma warning restore CS0414 // Field is assigned but never used
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression
