namespace MonoDetour.UnitTests.ILWeaverTests;

public static partial class CreateDelegateCallTests
{
    static int ran;

    [Fact]
    public static void CanCreateDelegateCall()
    {
        using var m = DefaultMonoDetourManager.New();
        m.ILHook(Stub, ILHook_Stub);

        Stub();

        Assert.Equal(11, ran);
    }

    private static void ILHook_Stub(ILManipulationInfo info)
    {
        ILWeaver w = new(info);
        w.InsertBeforeCurrent(
            w.CreateDelegateCall(() => // No args → No FastDelegateInvokers.
            {
                ran++;
            })
        );
        w.InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4_1), w.Create(OpCodes.Ldc_I4, 10));
        w.InsertBeforeCurrent(
            w.CreateDelegateCall(
                (bool _, int num) => // Args → Yes FastDelegateInvokers.
                {
                    ran += num;
                }
            )
        );
    }

    static void Stub() { }
}
