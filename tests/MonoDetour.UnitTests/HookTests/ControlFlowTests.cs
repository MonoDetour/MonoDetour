using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
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
        SetStringToHello.ILHook(ILHook_Print, new(priority: -4), manager: m);

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

    [Fact]
    public static void CanChangeControlFlowReturnValue()
    {
        using var m = DefaultMonoDetourManager.New();
        var lib = new ControlFlowLib();

        ReturnHello.ControlFlowPrefix(ReturnHookedRunOriginal);

        // A control flow prefix hook which does not change control flow
        // cannot override the return value if the control flow remains normal.
        Assert.Equal("hello", lib.ReturnHello());

        ReturnHello.ControlFlowPrefix(ReturnSkipOriginal, new(-1));

        // But here the changes will show since control flow is modified.
        Assert.Equal("skipped hooked", lib.ReturnHello());
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

    private static void ILHook_Print(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        w.InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4_0));
        w.CurrentTo(w.Last);
        w.InsertBeforeCurrent(w.CreateCall(Foo));
    }

    static void Foo(int i)
    {
        return;
    }

    private static ReturnFlow ControlFlowPrefixSkipOriginal(ControlFlowLib self, ref string message)
    {
        message += "foo ";
        return ReturnFlow.SkipOriginal;
    }

    private static void Postfix(ControlFlowLib self, ref string message)
    {
        message += "bar";
    }

    private static ReturnFlow ReturnHookedRunOriginal(ControlFlowLib self, ref string returnValue)
    {
        returnValue += "hooked";
        return ReturnFlow.None;
    }

    private static ReturnFlow ReturnSkipOriginal(ControlFlowLib self, ref string returnValue)
    {
        returnValue += "skipped ";
        return ReturnFlow.SkipOriginal;
    }
}
