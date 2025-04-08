using System.Text;
using MonoDetour.UnitTests.HookGen;
using MonoMod.HookGen;
using MonoMod.RuntimeDetour;
using On.TestApp.PlatformerController;
using TestApp;

[assembly: GenerateHookHelpers(typeof(TestApp.PlatformerController))]

namespace MonoDetour.UnitTests.HookGen;

public partial class HookGenTests
{
    private static readonly MonoDetourManager m = new();

    [Fact]
    public void Test1()
    {
        SpinBounce.Prefix(m, PlatformerController_SpinBounce);
        _DoStuff_d__3._ctor.Prefix(m, MoveNext_ctor);
        Assert.True(true);
    }

    private static void MoveNext_ctor(ref _DoStuff_d__3._ctor.Params args)
    {
        Console.WriteLine("Hello from MoveNext ctor!");
    }

    private static void PlatformerController_SpinBounce(ref SpinBounce.Params args)
    {
        args.self.Foo();
        args.self.DoStuff();
    }
}
