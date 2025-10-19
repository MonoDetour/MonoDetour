using MonoDetour.Cil;

namespace MonoDetour.UnitTests.FunctionalityTests;

public class NonStaticManipulators
{
    static bool ran;

    [Fact]
    void CanHookWithNonStaticManipulators()
    {
        using var m = DefaultMonoDetourManager.New();

        m.ILHook(Stub, ILHook_Stub);

        Stub();

        Assert.True(ran);
    }

    private void ILHook_Stub(ILManipulationInfo info)
    {
        ILWeaver w = new(info);
        w.InsertBeforeCurrent(w.CreateCall(SetRan));
    }

    static void SetRan()
    {
        ran = true;
    }

    static void Stub() { }
}
