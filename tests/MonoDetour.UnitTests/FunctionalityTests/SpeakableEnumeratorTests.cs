using MonoDetour.Reflection.Unspeakable;

namespace MonoDetour.UnitTests.FunctionalityTests;

public class SpeakableEnumeratorTests
{
    public int Number { get; set; } = 0;

    [Fact]
    void CanUseSpeakableEnumerator()
    {
        var stateMachineTarget = ((Delegate)GetEnumerator).Method.GetStateMachineTarget()!;

        using var m = DefaultMonoDetourManager.New();
        m.Hook<PrefixDetour>(stateMachineTarget, Prefix);

        GetEnumerator().MoveNext();

        Assert.Equal(2, Number);
    }

    static void Prefix(IEnumerator<int> enumerator)
    {
        var self = SpeakableEnumerator<int, SpeakableEnumeratorTests>.GetOrCreate(enumerator);
        self.This.Number++;
    }

    public IEnumerator<int> GetEnumerator()
    {
        Number++;
        yield break;
    }
}
