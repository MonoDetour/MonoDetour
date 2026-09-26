using System.Reflection;

namespace MonoDetour.UnitTests.HookTests;

public static partial class IEnumeratorTests
{
    private static readonly Queue<int> order = [];

    // FIXME: MoveNext.Target() should return MethodInfo
    static readonly EnumeratorFieldReferenceGetter<int> stateRef = (
        (MethodInfo)EnumerateRange.MoveNext.Target()
    ).EnumeratorFastFieldReferenceState();

    static readonly EnumeratorFieldReferenceGetter<object> currentRef = (
        (MethodInfo)EnumerateRange.MoveNext.Target()
    ).EnumeratorFastFieldReferenceCurrent<object>();

    [Fact]
    public static void CanHookIEnumerator()
    {
        order.Clear();

        using var m = DefaultMonoDetourManager.New();
        EnumerateRange.MoveNext.Prefix(Hook_MoveNextPrefix, manager: m);
        EnumerateRange.MoveNext.Postfix(Hook_MoveNextPostfix, manager: m);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([0, 2, 4, 6, 8], order);
    }

    private static void Hook_MoveNextPrefix(SpeakableEnumerator<object, LibraryMethods> self)
    {
        if (self.State != 0 && stateRef(self.Enumerator) != 0)
        {
            return;
        }
        order.Enqueue(0);
    }

    private static void Hook_MoveNextPostfix(
        SpeakableEnumerator<object, LibraryMethods> self,
        ref bool continueEnumeration
    )
    {
        if (!continueEnumeration)
        {
            return;
        }
        self.Current = (int)currentRef(self.Enumerator) * 2;

        order.Enqueue((int)self.Current);
    }
}
