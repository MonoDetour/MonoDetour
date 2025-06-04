using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoMod.Core.Platforms;
using Op = System.Reflection.Emit.OpCodes;

namespace MonoDetour.UnitTests.FunctionalityTests;

public static class CanAnalyzeStackSizeTests
{
    [Fact]
    public static void CanAnalyzeStackSize()
    {
        var m = DefaultMonoDetourManager.New();
        m.LogFilter = MonoDetourLogger.LogChannel.None;

        m.ILHook(Stub, WriteLogic);
        m.ILHook(Stub2, WriteLogic2);
    }

    [Fact]
    public static void AnalyzeTryCatch()
    {
        using var dmd = new DynamicMethodDefinition("Test", typeof(void), []);
        {
            var il = dmd.GetILGenerator();

            // TODO: Maybe make it find the shortest path
            var l1 = il.DefineLabel();
            var l2 = il.DefineLabel();
            var l3 = il.DefineLabel();
            var l4 = il.DefineLabel();
            var l5 = il.DefineLabel();
            var leave = il.DefineLabel();
            il.Emit(Op.Ldc_I4_1);
            il.Emit(Op.Ldc_I4_1);
            il.Emit(Op.Brtrue, l1);
            il.Emit(Op.Br, leave);

            il.MarkLabel(l2);
            il.Emit(Op.Ldstr, "3");
            il.Emit(Op.Br, l3);

            il.MarkLabel(l4);
            il.Emit(Op.Ldc_I4_5);
            il.Emit(Op.Pop);
            il.Emit(Op.Br, l5);

            il.MarkLabel(l3);
            il.Emit(Op.Ldc_I4_4);
            il.Emit(Op.Br, l4);

            il.MarkLabel(l1);
            il.Emit(Op.Ldc_I4_2);
            il.Emit(Op.Br, l2);

            il.MarkLabel(l5);
            il.Emit(Op.Pop);
            il.Emit(Op.Pop);
            il.Emit(Op.Pop);
            il.MarkLabel(leave);
            il.Emit(Op.Ret);
        }

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.Info,
            dmd.Definition.Body.Analyze().ToStringWithAnnotations()
        );
        PlatformTriple.Current.Compile(dmd.Generate());
    }

    private static void WriteLogic(ILManipulationInfo info)
    {
        //  1 | IL_004e: ldarg.0
        //  1 | IL_004f: ldfld MonoMod.RuntimeDetour.Detour MonoMod.RuntimeDetour.ILHook/Context::Detour
        //  2 | IL_0054: dup
        //  1 | IL_0055: brtrue.s IL_005a
        //  0 | IL_0057: pop
        //  0 | IL_0058: br.s IL_005f
        // -1 | IL_005a: call System.Void MonoMod.RuntimeDetour.Detour::Dispose()
        //  └── ERROR: Negative stack size; cannot be -1
        //    ¦ │ INFO: Stack imbalance starts at:
        //    ¦ └ -1 | IL_005a: call System.Void MonoMod.RuntimeDetour.Detour::Dispose()
        //    ¦
        //  0 | IL_005f: ldarg.0

        // ILWeaver w = new(info);

        // w.DefineLabel(out var label1);
        // w.DefineLabel(out var label2);

        // w.InsertBeforeCurrent(
        //     w.Create(OpCodes.Ldc_I4_1),
        //     w.Create(OpCodes.Dup),
        //     w.Create(OpCodes.Brtrue, label1),
        //     w.Create(OpCodes.Pop),
        //     w.Create(OpCodes.Br, label2),
        //     w.Create(OpCodes.Pop)
        // );

        // w.MarkLabelTo(w.Previous, label1);
        // w.MarkLabelTo(w.Current, label2);

        // CilAnalyzer.Analyze(info.Context.Body);
    }

    private static void WriteLogic2(ILManipulationInfo info)
    {
        // TODO: proper tests

        // ILWeaver w = new(info);

        // w.DefineLabel(out var labelEndLdc);
        // w.InsertBeforeCurrent(w.Create(OpCodes.Br, labelEndLdc));

        // w.MarkLabelToFutureNextInsert(out var label2);
        // w.InsertBeforeCurrent(
        //     w.Create(OpCodes.Ldc_I4_1),
        //     w.Create(OpCodes.Ret),
        //     w.Create(OpCodes.Pop),
        //     w.Create(OpCodes.Ldc_I4_1),
        //     w.Create(OpCodes.Ldc_I4_1),
        //     w.Create(OpCodes.Brtrue, label2)
        // );

        // w.MarkLabelTo(w.Previous.Previous.Previous, labelEndLdc);

        // CilAnalyzer.Analyze(info.Context.Body);
    }

    static void Stub()
    {
        return;
    }

    static int Stub2()
    {
        return 1;
    }
}
