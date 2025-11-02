using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgILContext
{
    private static MethodInfo? Self_GetValueT_ii;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int AddReference<T>(ILContext context, in T value) =>
        context.AddReference(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerable<Instruction> GetReference(Type type, ILContext context, int id)
    {
        Self_GetValueT_ii ??=
            typeof(DynamicReferenceManager).GetMethod(
                "GetValueT",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                [typeof(int), typeof(int)],
                null
            ) ?? throw new InvalidOperationException("GetValueT doesn't exist?!?!?!?");

        // Dirty workaround to avoid TypeLoadExceptions
        object cellRef = context.GetReferenceCell(id);
        var il = context.IL;

        yield return il.Create(OpCodes.Ldc_I4, ((DynamicReferenceCell)cellRef).Index);
        yield return il.Create(OpCodes.Ldc_I4, ((DynamicReferenceCell)cellRef).Hash);
        yield return il.Create(
            OpCodes.Call,
            il.Body.Method.Module.ImportReference(Self_GetValueT_ii.MakeGenericMethod(type))
        );
    }
}
