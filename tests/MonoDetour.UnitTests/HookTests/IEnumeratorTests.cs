using System.Collections;
using MonoDetour.Reflection.Unspeakable;

namespace MonoDetour.UnitTests.HookTests;

public static partial class IEnumeratorTests
{
    private static readonly Queue<int> order = [];

    static readonly ReferenceField<int> stateRef = EnumerateRange
        .StateMachineTarget()
        .EnumeratorFastFieldReferenceState();

    static readonly ReferenceField<object> currentRef = EnumerateRange
        .StateMachineTarget()
        .EnumeratorFastFieldReferenceCurrent<object>();

    [Fact]
    public static void CanHookIEnumerator()
    {
        order.Clear();

        using var m = DefaultMonoDetourManager.New();
        EnumerateRange.PrefixMoveNext(Hook_MoveNextPrefix, manager: m);
        EnumerateRange.PostfixMoveNext(Hook_MoveNextPostfix, manager: m);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([0, 2, 4, 6, 8], order);
    }

    private static void Hook_MoveNextPrefix(IEnumerator self)
    {
        if (stateRef(self) != 0)
        {
            return;
        }
        order.Enqueue(0);
    }

    private static void Hook_MoveNextPostfix(IEnumerator self, ref bool continueEnumeration)
    {
        if (!continueEnumeration)
        {
            return;
        }
        ref var current = ref currentRef(self);
        current = (int)current * 2;

        order.Enqueue((int)self.Current);
    }
}
