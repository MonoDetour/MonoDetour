using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.UnitTests.RuntimeAsyncLib;

namespace MonoDetour.UnitTests.FunctionalityTests;

// These tests cover MonoDetour's core runtime-async handling. HarmonyX interop
// consumes the same code path but is verified separately.
public static class RuntimeAsyncTests
{
    public delegate void StringPostfixSignature(ref string returnValue);

    public delegate void IntPostfixSignature(ref int returnValue);

    public delegate void VoidPostfixSignature();

    public delegate ReturnFlow IntPrefixSignature(ref int returnValue);

    static int voidPostfixCount;

    [Fact]
    public static async Task PostfixCanChangeRuntimeAsyncValueTaskResult()
    {
        using var m = DefaultMonoDetourManager.New();

        m.Hook<PostfixDetour>(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.GatherAsync))!,
            new StringPostfixSignature(StringPostfix)
        );

        Assert.Equal("value!", await RuntimeAsyncMethods.GatherAsync("value"));
    }

    [Fact]
    public static async Task PostfixCanChangeRuntimeAsyncTaskResult()
    {
        using var m = DefaultMonoDetourManager.New();

        m.Hook<PostfixDetour>(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.AddAsync))!,
            new IntPostfixSignature(IntPostfix)
        );

        Assert.Equal(105, await RuntimeAsyncMethods.AddAsync(2, 3));
    }

    [Fact]
    public static async Task PostfixRunsOnRuntimeAsyncVoidMethods()
    {
        using var m = DefaultMonoDetourManager.New();
        voidPostfixCount = 0;

        m.Hook<PostfixDetour>(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.DoTaskAsync))!,
            new VoidPostfixSignature(VoidPostfix)
        );
        m.Hook<PostfixDetour>(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.DoValueTaskAsync))!,
            new VoidPostfixSignature(VoidPostfix)
        );

        await RuntimeAsyncMethods.DoTaskAsync();
        await RuntimeAsyncMethods.DoValueTaskAsync();

        Assert.Equal(2, voidPostfixCount);
    }

    [Fact]
    public static async Task ControlFlowPrefixCanReplaceRuntimeAsyncResult()
    {
        using var m = DefaultMonoDetourManager.New();

        m.Hook<PrefixDetour>(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.AddAsync))!,
            new IntPrefixSignature(IntPrefix)
        );

        Assert.Equal(99, await RuntimeAsyncMethods.AddAsync(2, 3));
    }

    [Fact]
    public static void ReturnValueLocalUsesILReturnType()
    {
        using var m = DefaultMonoDetourManager.New();

        HookTargetRecords.HookTargetInfo? normalInfo = null;
        HookTargetRecords.HookTargetInfo? valueTaskInfo = null;
        HookTargetRecords.HookTargetInfo? taskInfo = null;

        using var normalHook = m.ILHook(
            typeof(LibraryMethods).GetMethod(nameof(LibraryMethods.TakeAndReturnInt))!,
            info => normalInfo = HookTargetRecords.GetHookTargetInfo(info.Context)
        );
        using var valueTaskHook = m.ILHook(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.GatherAsync))!,
            info => valueTaskInfo = HookTargetRecords.GetHookTargetInfo(info.Context)
        );
        using var taskHook = m.ILHook(
            typeof(RuntimeAsyncMethods).GetMethod(nameof(RuntimeAsyncMethods.DoTaskAsync))!,
            info => taskInfo = HookTargetRecords.GetHookTargetInfo(info.Context)
        );

        Assert.NotNull(normalInfo);
        Assert.NotNull(valueTaskInfo);
        Assert.NotNull(taskInfo);
        Assert.Equal("System.Int32", normalInfo.ReturnValue?.VariableType.FullName);
        Assert.Equal("System.String", valueTaskInfo.ReturnValue?.VariableType.FullName);
        Assert.Null(taskInfo.ReturnValue);
    }

    [Fact]
    public static void AnalysisDoesNotFlagRuntimeAsyncVoidReturn()
    {
        using var m = DefaultMonoDetourManager.New();

        foreach (
            var methodName in new[]
            {
                nameof(RuntimeAsyncMethods.DoTaskAsync),
                nameof(RuntimeAsyncMethods.DoValueTaskAsync),
            }
        )
        {
            string? analysis = null;
            using var hook = m.ILHook(
                typeof(RuntimeAsyncMethods).GetMethod(methodName)!,
                info => analysis = info.Context.ToAnalyzedString()
            );

            Assert.NotNull(analysis);
            Assert.DoesNotContain("Popping more than stack size", analysis);
        }
    }

    static void StringPostfix(ref string returnValue) => returnValue += "!";

    static void IntPostfix(ref int returnValue) => returnValue += 100;

    static void VoidPostfix() => voidPostfixCount++;

    static ReturnFlow IntPrefix(ref int returnValue)
    {
        returnValue = 99;
        return ReturnFlow.SkipOriginal;
    }
}
