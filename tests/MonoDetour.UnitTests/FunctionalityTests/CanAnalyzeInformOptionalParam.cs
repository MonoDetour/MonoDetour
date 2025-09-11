/* using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoMod.Core.Platforms;
using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.FunctionalityTests;

public static class CanAnalyzeInformOptionalParam
{
    [Fact]
    public static void AnalyzeOptionalParam()
    {
        using var dmd = new DynamicMethodDefinition("TestParam", typeof(void), []);
        {
            var il = dmd.GetILProcessor();
            il.Emit(Op.Ldc_I4_0);
            il.Emit(Op.Call, ((Delegate)OptionalParams).Method);
            il.Emit(Op.Ret);
        }

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.Warning,
            dmd.Definition.Body.CreateInformationalSnapshotJIT()
                .AnnotateErrors()
                .ToStringWithAnnotations()
        );
        PlatformTriple.Current.Compile(dmd.Generate());
    }

    [Fact]
    public static void AnalyzeNoOptionalParam()
    {
        using var dmd = new DynamicMethodDefinition("TestNoParam", typeof(void), []);
        {
            var il = dmd.GetILProcessor();
            il.Emit(Op.Ldc_I4_0);
            il.Emit(Op.Call, ((Delegate)NoOptionalParams).Method);
            il.Emit(Op.Ret);
        }

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.Warning,
            dmd.Definition.Body.CreateInformationalSnapshotJIT()
                .AnnotateErrors()
                .ToStringWithAnnotations()
        );
        PlatformTriple.Current.Compile(dmd.Generate());
    }

    static void OptionalParams(bool value, int num = 0, string name = "foo") { }

    static void NoOptionalParams(bool value, int num, string name) { }
}
 */
