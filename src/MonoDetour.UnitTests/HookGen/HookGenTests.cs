using MonoDetour.UnitTests.HookGen;
using MonoMod.HookGen;
using MonoMod.RuntimeDetour;

[assembly: GenerateHookHelpers(typeof(MonoDetour.MonoDetourManager), Kind = DetourKind.Both)]

namespace MonoDetour.UnitTests.HookGen;

public partial class HookGenTests
{
    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }
}
