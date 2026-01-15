using HarmonyLib;

namespace MonoDetour.UnitTests.InteropTests;

// Test for: MonoDetour interprets HarmonyX's runOriginal local var incorrectly
// https://github.com/MonoDetour/MonoDetour/issues/24
public static class HarmonyXIncorrectReturnTests
{
    static bool originalRan;
    static bool harmonyXPrefixRan;
    static bool runOriginalValue = true;
    static bool monoDetourPrefixRan;

    [Fact]
    public static void CanPrefixMonoDetourAndHarmonyX()
    {
        Interop.HarmonyX.HarmonyXInterop.Initialize();

        using var m = DefaultMonoDetourManager.New();
        m.Hook<PrefixDetour>(Original, MonoDetourPrefix_Stub, new(1));

        var scope = new DetourConfigContext(new(id: "detourContext", priority: -1));

        using (scope.Use())
        {
            using var harmony = new Harmony("test_incorrect_return");
            harmony.Patch(((Delegate)Original).Method, prefix: new(HarmonyPrefix_Stub));

            Original();
        }

        // MonoDetour should not interpret HarmonyX prefix not cancelling the method as
        // having to cancel the method.
        Assert.True(originalRan);

        originalRan = false;
        harmonyXPrefixRan = false;
        monoDetourPrefixRan = false;

        using (scope.Use())
        {
            using var harmony = new Harmony("test_incorrect_return2");
            harmony.Patch(((Delegate)Original).Method, prefix: new(HarmonyPrefix_SkipOriginal));

            Original();
        }

        // HarmonyX cancelling the method should not cancel MonoDetour,
        // but MonoDetour needs to cancel the original method.
        Assert.False(originalRan);
        Assert.True(harmonyXPrefixRan);
        Assert.True(monoDetourPrefixRan);

        originalRan = false;
        harmonyXPrefixRan = false;
        monoDetourPrefixRan = false;

        using (scope.Use())
        {
            using var harmony = new Harmony("test_incorrect_return3");
            harmony.Patch(((Delegate)Original).Method, prefix: new(HarmonyPrefix_NoSkipOriginal));

            using var m2 = DefaultMonoDetourManager.New();
            m2.Hook<PrefixDetour>(Original, MonoDetourPrefix_SkipOriginal, new(-2));

            Original();

            // Checking that a HarmonyX prefix can actually read runOriginal
            // from a MonoDetour ControlFlow Prefix.
            Assert.False(originalRan);
            Assert.True(harmonyXPrefixRan);
            Assert.False(runOriginalValue);
            Assert.True(monoDetourPrefixRan);
        }

        Interop.HarmonyX.HarmonyXInterop.Dispose();
    }

    static ReturnFlow MonoDetourPrefix_SkipOriginal() => ReturnFlow.SkipOriginal;

    static void MonoDetourPrefix_Stub() => monoDetourPrefixRan = true;

    static bool HarmonyPrefix_SkipOriginal()
    {
        harmonyXPrefixRan = true;
        return false;
    }

    static bool HarmonyPrefix_NoSkipOriginal(bool __runOriginal)
    {
        harmonyXPrefixRan = true;
        runOriginalValue = __runOriginal;
        return true;
    }

    static bool HarmonyPrefix_Stub() => harmonyXPrefixRan = true;

    static void Original() => originalRan = true;
}
