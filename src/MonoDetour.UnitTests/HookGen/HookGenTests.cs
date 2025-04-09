using Mono.Cecil.Cil;
using MonoDetour.HookGen;
using MonoMod.Cil;
using On.SomeNamespace.SomeType;
using SomeNamespace;

[assembly: GenerateHookHelpers(typeof(SomeType))]

namespace MonoDetour.UnitTests.HookGen;

[MonoDetourTargets]
public partial class HookGenTests
{
    static readonly Queue<int> order = [];

    [Fact]
    public void CanFixEarlyReturn()
    {
        order.Clear();
        SomeMethod.Postfix(Postfix1_2nd);
        SomeMethod.ILHook(ILHook2_1st_Returns);
        SomeMethod.ILHook(ILHook3_3rd_Returns);
        SomeMethod.Postfix(Postfix4_4th);

        var someType = new SomeType();
        someType.SomeMethod(1);
        HookGenManager.Instance.DisposeHooks();

        Assert.Equal([1, 2, 3, 4], order);
    }

    private static void Postfix1_2nd(ref SomeMethod.Params args)
    {
        order.Enqueue(2);
    }

    private static void ILHook2_1st_Returns(ILContext il)
    {
        ILCursor c = new(il);
        c.EmitDelegate(() =>
        {
            order.Enqueue(1);
        });
        c.Emit(OpCodes.Ret);
    }

    private static void ILHook3_3rd_Returns(ILContext il)
    {
        ILCursor c = new(il);
        c.Index -= 1;
        c.EmitDelegate(() =>
        {
            order.Enqueue(3);
        });
        c.Emit(OpCodes.Ret);
    }

    private static void Postfix4_4th(ref SomeMethod.Params args)
    {
        order.Enqueue(4);
    }
}
