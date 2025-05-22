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

    [Fact]
    public static void CanFailMatchAfterResolutionWhenImportantInstructionWasRemoved()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(CallStub2, ILHook_RemoveStubCall, new(1));
        m.ILHook(CallStub2, ILHook_IncrementNumberButIntendedToFail, new(0));

        // This should throw when the test fails.
        CallStub();
    }

    static void ILHook_IncrementNumber(ILManipulationInfo info)
    {
        ILWeaver w = new(info) { LogFilter = MonoDetourLogger.LogChannel.None };
        w.Match(
                x => x.MatchLdcI4(0),
                x => x.MatchCall(((Delegate)Stub).Method) && w.SetCurrentTo(x)
            )
            .ThrowIfFailure()
            .InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4_1), w.Create(OpCodes.Add));
    }

    private static void ILHook_RemoveStubCall(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        w.InsertBeforeCurrent(w.Create(OpCodes.Ldstr, "removed stub"), w.Create(OpCodes.Pop));

        w.Match(x => x.MatchCall(((Delegate)Stub).Method) && w.SetCurrentTo(x))
            .ThrowIfFailure()
            .ReplaceCurrent(w.CreateCall(ReplacementStub));
    }

    static void ILHook_IncrementNumberButIntendedToFail(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // We need to make sure we evaluate this test
        // when the stub call has actually been removed.
        if (!w.Match(x => x.MatchLdstr("removed stub")).IsValid)
        {
            return;
        }

        w.Match(
                x => x.MatchLdcI4(0),
                x => x.MatchCall(((Delegate)Stub).Method) && w.SetCurrentTo(x)
            )
            .Extract(out var result);

        if (result.IsValid)
        {
            throw new Exception("Result must be invalid.");
        }
    }

    static void CallStub()
    {
        Stub(0);
    }

    static void CallStub2()
    {
        Stub(0);
    }

    static void Stub(int num)
    {
        runCount = num;
    }

    static void ReplacementStub(int num)
    {
        runCount = num;
    }
}
