using MonoDetour.Reflection;

namespace MonoDetour.UnitTests.FunctionalityTests;

public class IEnumeratorReflectionTests
{
    public int Number { get; set; } = 0;

    static EnumeratorFieldGetter<IEnumeratorReflectionTests> getInstance = null!;

    [Fact]
    void CanGetThisField()
    {
        var stateMachineTarget = ((Delegate)GetEnumerator).Method.GetStateMachineTarget()!;
        stateMachineTarget.EnumeratorFastThisFieldGetter(ref getInstance);

        using var m = DefaultMonoDetourManager.New();
        m.Hook<PrefixDetour>(stateMachineTarget, Prefix);

        GetEnumerator().MoveNext();

        Assert.Equal(2, Number);
    }

    static void Prefix(object self)
    {
        getInstance(self).Number++;
    }

    public IEnumerator<int> GetEnumerator()
    {
        Number++;
        yield break;
    }
}
