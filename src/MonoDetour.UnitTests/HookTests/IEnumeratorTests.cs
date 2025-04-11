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

        EnumerateRange.ILHook(ILHook_EnumerateRange);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([0, 1, 2, 3, 4], order);
    }

    private static void ILHook_EnumerateRange(ILContext il)
    {
        ILCursor c = new(il);
        c.Index -= 1;

        c.Emit(OpCodes.Call, new Func<IEnumerator, IEnumerator>(Hook_EnumerateRange).Method);

        c.Method.RecalculateILOffsets();
        Console.WriteLine(il);
    }

    private static IEnumerator Hook_EnumerateRange(IEnumerator enumerator)
    {
        int i = 0;
        while (enumerator.MoveNext())
        {
            order.Enqueue(i);
            i++;
            yield return enumerator.Current;
        }
    }
}
