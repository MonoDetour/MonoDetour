using System.Collections;
using MonoDetour.Reflection.Unspeakable;

namespace MonoDetour.UnitTests.HookTests;

public static partial class IEnumeratorTests
{
    private static readonly Queue<int> order = [];

    static readonly FieldReferenceGetter<int> stateRef = EnumerateRange
        .StateMachineTarget()
        .EnumeratorFastFieldReferenceState();

    static readonly FieldReferenceGetter<object> currentRef = EnumerateRange
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

    private static void Hook_MoveNextPrefix(SpeakableEnumerator<object, LibraryMethods> self)
    {
        if (self.State != 0 && stateRef(self.This) != 0)
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
        self.Current = (int)currentRef(self.This) * 2;

        order.Enqueue((int)self.Current);
    }
}
