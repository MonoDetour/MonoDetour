namespace MonoDetour.UnitTests.FunctionalityTests;

public static partial class InvokeHookInitializersTests
{
    private static int count = 0;

    [Fact]
    public static void CanInvokeHookInitializersProperly()
    {
        Assert.Equal(0, count);

        MonoDetourManager.InvokeHookInitializers(typeof(InvokeHookInitializersTests).Assembly);

        Assert.Equal(2, count);

        MonoDetourManager.InvokeHookInitializers(typeof(HookDoNotInitWithoutDirectReference));

        Assert.Equal(3, count);
    }

    [MonoDetourTargets]
    private class HookInits
    {
        [MonoDetourHookInit]
        static void Init()
        {
            count++;
        }

        [MonoDetourHookInit]
        static void Init2()
        {
            count++;
        }
    }

    private class HookDoNotInitWithoutDirectReference
    {
        [MonoDetourHookInit]
        static void Init()
        {
            count++;
        }
    }
}
