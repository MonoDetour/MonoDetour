using HarmonyLib;
using MonoDetour.Cil;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class MatchResolutionTests
{
    static int runCount = 0;

    [Fact]
    public static void CanResolveMatchAfterSameInstructionsAreModified()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(CallStub, ILHook_IncrementNumber, new(1));
        m.ILHook(CallStub, ILHook_IncrementNumber, new(-1));

        CallStub();

        Assert.Equal(2, runCount);
        runCount = 0;

        // HarmonyX would normally mess the resolution feature by rewriting all method
        // instructions when a transpiler is written. Let's test HarmonyX interop.
        Interop.HarmonyX.Initialize.Apply();

        using (var scope = new DetourConfigContext(new(id: "detourContext", priority: 0)).Use())
        {
            using var harmony = new Harmony("test");
            harmony.Patch(((Delegate)CallStub).Method, transpiler: new(Transpiler));

            CallStub();
        }

        Interop.HarmonyX.Initialize.Undo();

        Assert.Equal(2, runCount);
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        instructions;

    [Fact]
    public static void CanFailMatchAfterResolutionWhenImportantInstructionWasRemoved()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(CallStub2, ILHook_RemoveStubCall, new(1));
        m.ILHook(CallStub2, ILHook_IncrementNumberButIntendedToFail, new(-1));

        // This should throw when the test fails.
        CallStub();
    }

    static void ILHook_IncrementNumber(ILManipulationInfo info)
    {
        ILWeaver w = new(info) { LogFilter = MonoDetourLogger.LogChannel.None };
        w.MatchRelaxed(
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

        w.MatchRelaxed(x => x.MatchCall(((Delegate)Stub).Method) && w.SetCurrentTo(x))
            .ThrowIfFailure()
            .ReplaceCurrent(w.CreateCall(ReplacementStub));
    }

    static void ILHook_IncrementNumberButIntendedToFail(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // We need to make sure we evaluate this test
        // when the stub call has actually been removed.
        if (!w.MatchStrict(x => x.MatchLdstr("removed stub")).IsValid)
        {
            return;
        }

        w.MatchRelaxed(
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
