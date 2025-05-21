using MonoDetour.Cil;
using On.MonoDetour.UnitTests.TestLib.ControlFlowLib;

namespace MonoDetour.UnitTests.HookTests;

[MonoDetourTargets(typeof(ControlFlowLib), GenerateControlFlowVariants = true)]
public static class ControlFlowTests
{
    [Fact]
    public static void CanChangeControlFlow()
    {
        using var m = DefaultMonoDetourManager.New();

        var lib = new ControlFlowLib();

        SetStringToHello.ControlFlowPrefix(ControlFlowPrefixSkipOriginal, manager: m);
        SetStringToHello.Postfix(Postfix, manager: m);
        // SetStringToHello.ILHook(ILHook_Print, new(priority: -4), manager: m);

        string? message = null;
        lib.SetStringToHello(ref message);
        Assert.Equal("foo bar", message);

        SetStringToHello.ControlFlowPrefix(ControlFlowPrefixHardReturn, new(-1), manager: m);
        SetStringToHello.ControlFlowPrefix(ControlFlowPrefixNone, new(-3), manager: m);

        message = null;
        lib.SetStringToHello(ref message);
        Assert.Equal("none baz", message);

        SetStringToHello.ControlFlowPrefix(ControlFlowPrefixSkipOriginal, new(-2), manager: m);

        message = null;
        lib.SetStringToHello(ref message);
        Assert.Equal("none foo baz", message);
    }

    private static ReturnFlow ControlFlowPrefixNone(ControlFlowLib self, ref string message)
    {
        message += "none ";
        return ReturnFlow.None;
    }

    private static ReturnFlow ControlFlowPrefixHardReturn(ControlFlowLib self, ref string message)
    {
        message += "baz";
        return ReturnFlow.HardReturn;
    }

    // private static void ILHook_Print(ILManipulationInfo info)
    // {
    //     Console.WriteLine(info.ManipulationContext);
    // }

    private static ReturnFlow ControlFlowPrefixSkipOriginal(ControlFlowLib self, ref string message)
    {
        message += "foo ";
        return ReturnFlow.SkipOriginal;
    }

    private static void Postfix(ControlFlowLib self, ref string message)
    {
        message += "bar";
    }
}
