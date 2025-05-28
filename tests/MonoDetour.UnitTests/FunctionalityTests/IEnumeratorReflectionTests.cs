namespace MonoDetour.UnitTests.FunctionalityTests;

public class IEnumeratorReflectionTests
{
    public int Number { get; set; } = 0;

    static EnumeratorFieldReferenceGetter<IEnumeratorReflectionTests> instanceRef = null!;

    [Fact]
    void CanGetThisField()
    {
        var stateMachineTarget = ((Delegate)GetEnumerator).Method.GetStateMachineTarget()!;
        stateMachineTarget.EnumeratorFastFieldReferenceThis(ref instanceRef);

        using var m = DefaultMonoDetourManager.New();
        m.Hook<PrefixDetour>(stateMachineTarget, Prefix);

        GetEnumerator().MoveNext();

        Assert.Equal(2, Number);
    }

    static void Prefix(IEnumerator<int> self)
    {
        instanceRef(self).Number++;
    }

    public IEnumerator<int> GetEnumerator()
    {
        Number++;
        yield break;
    }
}
