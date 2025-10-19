namespace MonoDetour.UnitTests.FunctionalityTests;

public class NonStaticManipulators
{
    static int ran;

    [Fact]
    void CanHookWithNonStaticManipulators()
    {
        using var m = DefaultMonoDetourManager.New();
        using var m2 = DefaultMonoDetourManager.New();
        // m2.LogFilter = MonoDetourLogger.LogChannel.IL;

        m.ILHook(Stub, ILHook_Stub);
        m.Hook<PrefixDetour>(Stub, Prefix_InstanceIncrementRan);
        m2.Hook<PostfixDetour>(Stub, Postfix_InstanceIncrementRan);

        Stub(true);

        Assert.Equal(3, ran);
    }

    private void ILHook_Stub(ILManipulationInfo info)
    {
        ILWeaver w = new(info);
        w.InsertBeforeCurrent(w.CreateCall(StaticIncrementRan));
    }

    void Prefix_InstanceIncrementRan(ref bool flag) => StaticIncrementRan();

    void Postfix_InstanceIncrementRan(ref bool flag, ref bool returnValue) => StaticIncrementRan();

    static void StaticIncrementRan()
    {
        ran++;
    }

    static bool Stub(bool flag) => flag;
}
