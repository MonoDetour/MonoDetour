using HarmonyLib;

namespace MonoDetour.UnitTests.InteropTests;

public static class PrefixControlFlowHarmonyXTests
{
    static int runCount;
    static int runCount2;

    [Fact]
    public static void CanPrefixControlFlowWithHarmonyX()
    {
        using var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Stub, Postfix_Stub, new(1));

        using var m2 = DefaultMonoDetourManager.New();
        m2.Hook<PrefixDetour>(Stub, Prefix_Stub, new(-1));

        Interop.HarmonyX.HarmonyXInterop.Initialize();

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

        Interop.HarmonyX.HarmonyXInterop.Dispose();
    }

    [Fact]
    public static void CanJumpFromMonoDetourPrefixToHarmonyXPostfix()
    {
        using var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Stub2, Postfix_Stub2, new(1));

        using var m2 = DefaultMonoDetourManager.New();
        m2.Hook<PrefixDetour>(Stub2, Prefix_Stub2, new(-1));

        var scope = new DetourConfigContext(new(id: "detourContext", priority: 2));

        Interop.HarmonyX.HarmonyXInterop.Initialize();

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

        Interop.HarmonyX.HarmonyXInterop.Dispose();
    }

    [Fact]
    public static void CanHardReturnWithHarmonyX()
    {
        // In this test, we insert a control flow prefix with HardReturn.
        // Normally, HarmonyX would redirect its return values,
        // and the HarmonyPostfixThrowMethod would run.
        // Additionally, we include a transpiler method to make HarmonyX
        // rewrite all instructions in the target method.
        // So, we:
        // 1. insert and mark ret labels as persistent
        // 1. map old instructions to new ones
        // 2. prevent HarmonyX from redirecting our persistent ret instructions.

        using var m = DefaultMonoDetourManager.New();
        m.Hook<PrefixDetour>(Stub3, Prefix_HardReturnStub3, new(1));

        var scope = new DetourConfigContext(new(id: "detourContext", priority: 0));

        Interop.HarmonyX.HarmonyXInterop.Initialize();

        using (scope.Use())
        {
            using var harmony = new Harmony("test3");
            harmony.Patch(
                ((Delegate)Stub3).Method,
                postfix: new(HarmonyPostfixThrow),
                transpiler: new(Transpiler)
            );

            Stub3();
        }

        Interop.HarmonyX.HarmonyXInterop.Dispose();
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

    static ReturnFlow Prefix_HardReturnStub3() => ReturnFlow.HardReturn;

    static void HarmonyPostfixThrow() => throw new Exception("This should never run.");

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

    static void Stub3() { }
}
