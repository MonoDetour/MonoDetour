using MonoDetour.Cil;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class MatchResolutionTests
{
    static int runCount = 0;

    [Fact]
    public static void CanResolveMatchAfterSameInstructionsAreModified()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(CallStub, ILHook_IncrementNumber);
        m.ILHook(CallStub, ILHook_IncrementNumber);

        CallStub();

        Assert.Equal(2, runCount);
    }

    static void ILHook_IncrementNumber(ILManipulationInfo info)
    {
        ILWeaver w = new(info);
        w.Match(
                x => x.MatchLdcI4(0),
                x => x.MatchCall(((Delegate)Stub).Method) && w.SetCurrentTo(x)
            )
            .ThrowIfFailure()
            .InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4_1), w.Create(OpCodes.Add));
    }

    static void CallStub()
    {
        Stub(0);
    }

    static void Stub(int num)
    {
        runCount = num;
    }
}
