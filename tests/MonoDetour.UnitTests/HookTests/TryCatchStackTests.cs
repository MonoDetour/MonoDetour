using MonoDetour.Cil;

namespace MonoDetour.UnitTests.HookTests;

public static class TryCatchStackTests
{
    [Fact]
    public static void CanFixStackNotEmptyBeforePrefixTry()
    {
        var m = DefaultMonoDetourManager.New();

        // m.Hook<PrefixDetour>(Stub, Prefix_DoNothing, new(priority: 1));
        m.ILHook(Stub, PassIntFromStartToEnd, new(priority: 0));

        // This throws if the hooks produce invalid IL.
        Stub();
    }

    private static void PassIntFromStartToEnd(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // w.InsertBefore(w.First, w.Create(OpCodes.Ldc_I4_0));

        w.MatchMultipleStrict(
            (match) =>
            {
                match.InsertBeforeCurrentStealLabels(
                    w.Create(OpCodes.Ldc_I4_1),
                    w.Create(OpCodes.Pop)
                );
            },
            x => x.MatchRet() && w.SetCurrentTo(x)
        );
    }

    static void Prefix_DoNothing() { }

    static void Stub() { }
}
