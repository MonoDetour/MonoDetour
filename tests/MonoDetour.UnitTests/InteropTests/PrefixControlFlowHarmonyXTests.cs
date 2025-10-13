using HarmonyLib;

namespace MonoDetour.UnitTests.InteropTests;

public static class PrefixControlFlowHarmonyXTests
{
    static int runCount = 0;
    static int runCount2 = 0;

    [Fact]
    public static void CanPrefixControlFlowWithHarmonyX()
    {
        using var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Stub, Postfix_Stub, new(1));

        using var m2 = DefaultMonoDetourManager.New();
        m2.Hook<PrefixDetour>(Stub, Prefix_Stub, new(-1));

        Interop.HarmonyX.Support.Initialize();

        var scope = new DetourConfigContext(new(id: "detourContext", priority: 0));

        using (scope.Use())
        {
            using var harmony = new Harmony("test2");
            harmony.Patch(((Delegate)Stub).Method, transpiler: new(Transpiler));

            Stub();
        }

        Assert.Equal(2, runCount);
        runCount = 0;

        using (scope.Use())
        {
            using var harmony = new Harmony("test2");
            harmony.Patch(
                ((Delegate)Stub).Method,
                prefix: new(HarmonyPrefix),
                postfix: new(HarmonyPostfix),
                transpiler: new(Transpiler)
            );

            Stub();
        }

        Assert.Equal(4, runCount);

        Interop.HarmonyX.Support.Dispose();
    }

    [Fact]
    public static void CanJumpFromMonoDetourPrefixToHarmonyXPostfix()
    {
        using var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Stub2, Postfix_Stub2, new(1));

        using var m2 = DefaultMonoDetourManager.New();
        m2.Hook<PrefixDetour>(Stub2, Prefix_Stub2, new(-1));

        var scope = new DetourConfigContext(new(id: "detourContext", priority: 2));

        Interop.HarmonyX.Support.Initialize();

        using (scope.Use())
        {
            using var harmony = new Harmony("test2");
            harmony.Patch(
                ((Delegate)Stub2).Method,
                postfix: new(HarmonyPostfix2),
                transpiler: new(Transpiler)
            );

            Stub2();
        }

        Assert.Equal(3, runCount2);

        Interop.HarmonyX.Support.Dispose();
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        instructions;

    static bool HarmonyPrefix(ref int __result)
    {
        runCount++;
        __result = 1;
        return false;
    }

    static void HarmonyPostfix()
    {
        runCount++;
    }

    static void HarmonyPostfix2()
    {
        runCount2++;
    }

    static ReturnFlow Prefix_Stub(ref int returnValue)
    {
        runCount++;
        returnValue = 2;
        return ReturnFlow.SkipOriginal;
    }

    static void Postfix_Stub()
    {
        runCount++;
    }

    static ReturnFlow Prefix_Stub2(ref int returnValue)
    {
        runCount2++;
        returnValue = 2;
        return ReturnFlow.SkipOriginal;
    }

    static void Postfix_Stub2()
    {
        runCount2++;
    }

    static int Stub()
    {
        runCount -= 10;
        return 0;
    }

    static int Stub2()
    {
        runCount2 -= 10;
        return 0;
    }
}
