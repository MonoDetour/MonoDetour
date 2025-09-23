// Very important for making sure global namespaces work in HookGen.

using System.Collections;

class SomeType
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
