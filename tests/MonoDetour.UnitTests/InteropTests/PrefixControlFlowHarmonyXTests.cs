using HarmonyLib;
using MonoDetour.Cil;

namespace MonoDetour.UnitTests.InteropTests;

public static class PrefixControlFlowHarmonyXTests
{
    static int runCount = 0;

    [Fact]
    public static void CanPrefixControlFlowWithHarmonyX()
    {
        using var m = DefaultMonoDetourManager.New();
        m.Hook<PostfixDetour>(Stub, Postfix_Stub, new(1));
        m.Hook<PrefixDetour>(Stub, Prefix_Stub, new(-1));

        Interop.HarmonyX.Support.Initialize();

        using (var scope = new DetourConfigContext(new(id: "detourContext", priority: 0)).Use())
        {
            using var harmony = new Harmony("test2");
            harmony.Patch(((Delegate)Stub).Method, transpiler: new(Transpiler));

            Stub();
        }

        Interop.HarmonyX.Support.Dispose();

        Assert.Equal(2, runCount);
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        instructions;

    static ReturnFlow Prefix_Stub()
    {
        runCount++;
        return ReturnFlow.SkipOriginal;
    }

    static void Postfix_Stub()
    {
        runCount++;
    }

    static void Stub()
    {
        runCount++;
    }
}
