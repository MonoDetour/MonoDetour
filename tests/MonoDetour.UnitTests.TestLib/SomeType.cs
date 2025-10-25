// Very important for making sure global namespaces work in HookGen.

using System.Collections;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
sealed class SomeType
{
    void SomeMethod()
    {
        return;
    }

    IEnumerator SomeIEnumerator()
    {
        yield break;
    }
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression
