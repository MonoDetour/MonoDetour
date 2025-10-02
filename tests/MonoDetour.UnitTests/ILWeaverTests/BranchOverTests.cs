using MonoDetour.Cil;
using Op = Mono.Cecil.Cil.OpCodes;

namespace MonoDetour.UnitTests.ILWeaverTests;

public static class BranchOverTests
{
    [Fact]
    public static void CanBranchOverProperly()
    {
        using var dmd = new DynamicMethodDefinition("BranchOverTest", typeof(int), [typeof(bool)]);

        var il = dmd.GetILProcessor();
        var instrs = dmd.Definition.Body.Instructions;

        il.Emit(Op.Ldarg_0);

        var start = il.Create(Op.Ldc_I4_1);
        il.Append(start);
        var end = il.Create(Op.Add);
        il.Append(end);

        il.Emit(Op.Ret);

        var method = dmd.Generate().CreateDelegate<Func<bool, int>>();

        Assert.Equal(1, method(false));
        Assert.Equal(2, method(true));

        new ILContext(dmd.Definition).Invoke(il =>
        {
            ILWeaver w = new(new(il));
            w.InsertBranchOverIfFalse(start, end, w.Create(Op.Dup));
        });

        method = dmd.Generate().CreateDelegate<Func<bool, int>>();

        Assert.Equal(0, method(false));
        Assert.Equal(2, method(true));
    }
}
