using System.Collections;
using MonoMod.Utils;

namespace MonoDetour.UnitTests.HookTests;

public static partial class IEnumeratorTests
{
    private static readonly Queue<int> order = [];

    [Fact]
    public static void CanHookIEnumerator()
    {
        order.Clear();

        var m = new MonoDetourManager();
        EnumerateRange.IEnumeratorDetour(Hook_IEnumeratorDetour, m);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([1, 2, 3, 4], order);
        order.Clear();

        // Now do it with more hooks.

        EnumerateRange.IEnumeratorPrefix(Hook_IEnumeratorPrefix, m);
        EnumerateRange.IEnumeratorPostfix(Hook_IEnumeratorPostfix, m);

        enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        m.DisposeHooks();
        Assert.Equal([0, 1, 2, 3, 4, 4], order);
    }

    private static IEnumerator Hook_IEnumeratorDetour(IEnumerator enumerator)
    {
        while (enumerator.MoveNext())
        {
            order.Enqueue((int)enumerator.Current);
            yield return enumerator.Current;
        }
    }

    private static void Hook_IEnumeratorPrefix(IEnumerator enumerator)
    {
        // Remember, enumerator.Current will be null here since we are in a prefix!
        order.Enqueue(0);
    }

    private static void Hook_IEnumeratorPostfix(IEnumerator enumerator)
    {
        var current = (int)enumerator.Current;
        order.Enqueue(current);
    }
}
