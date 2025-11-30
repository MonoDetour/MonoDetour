using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Bindings.Reorg.MonoModUtils;

static class ReorgILContext
{
    delegate int Signature<T>(ILContext context, in T? value);
    static MethodInfo Self_GetValueT_ii = null!;
    static MethodInfo addReferenceMethod = null!;
    static MethodInfo cellRef_get_Index = null!;
    static MethodInfo cellRef_get_Hash = null!;
    static MethodInfo getReferenceCell = null!;

    static class Container<T>
    {
        internal static Signature<T> AddReference =>
            field ??= addReferenceMethod
                .MakeGenericMethod([typeof(T)])
                .CreateDelegate<Signature<T>>();
    }

    internal static void Init()
    {
        addReferenceMethod =
            typeof(ILContext).GetMethod(nameof(ILContext.AddReference))
            ?? throw new NullReferenceException();

        var dynamicReferenceManager = Type.GetType(
            "MonoMod.Utils.DynamicReferenceManager, MonoMod.Utils"
        )!;

        Self_GetValueT_ii =
            dynamicReferenceManager.GetMethod(
                "GetValueT",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                [typeof(int), typeof(int)],
                null
            ) ?? throw new InvalidOperationException("GetValueT doesn't exist?!?!?!?");

        var dynamicReferenceCellType = Type.GetType(
            "MonoMod.Utils.DynamicReferenceCell, MonoMod.Utils"
        )!;

        getReferenceCell = typeof(ILContext).GetMethod("GetReferenceCell")!;
        cellRef_get_Index = dynamicReferenceCellType.GetProperty("Index")!.GetGetMethod()!;
        cellRef_get_Hash = dynamicReferenceCellType.GetProperty("Hash")!.GetGetMethod()!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int AddReference<T>(ILContext context, in T value) =>
        Container<T>.AddReference(context, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerable<Instruction> GetReference(Type type, ILContext context, int id)
    {
        object cellRef = getReferenceCell.Invoke(context, [id])!;
        var il = context.IL;

        yield return il.Create(OpCodes.Ldc_I4, cellRef_get_Index.Invoke(cellRef, [])!);
        yield return il.Create(OpCodes.Ldc_I4, cellRef_get_Hash.Invoke(cellRef, [])!);
        yield return il.Create(
            OpCodes.Call,
            il.Body.Method.Module.ImportReference(Self_GetValueT_ii.MakeGenericMethod(type))
        );
    }
}
