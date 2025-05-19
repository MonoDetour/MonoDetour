using System;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace MonoDetour.Reflection;

/// <summary>
/// A Method which takes in an object and returns <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Field type.</typeparam>
/// <param name="instance">Enumerator instance.</param>
/// <returns>The field value.</returns>
public delegate T EnumeratorFieldGetter<T>(object instance);

/// <summary>
/// Enumerator related reflection helper methods.
/// </summary>
public static class EnumeratorReflection
{
    /// <summary>
    /// Builds and sets a fast getter method for the <c>&lt;&gt;4__this</c>
    /// field on an IEnumerator to <paramref name="enumeratorFieldGetter"/>.
    /// </summary>
    /// <param name="enumeratorFieldGetter">The field to set.</param>
    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastThisFieldGetter{T}(MethodInfo)"/>
    /// <param name="methodInfo"></param>
    public static void EnumeratorFastThisFieldGetter<T>(
        this MethodInfo methodInfo,
        ref EnumeratorFieldGetter<T> enumeratorFieldGetter
    ) => enumeratorFieldGetter = methodInfo.EnumeratorFastThisFieldGetter<T>();

    /// <summary>
    /// Builds and returns a fast getter method for the <c>&lt;&gt;4__this</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="methodInfo">A method of the enumerator.</param>
    /// <returns>A fast field getter method.</returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="Exception"></exception>
    public static EnumeratorFieldGetter<T> EnumeratorFastThisFieldGetter<T>(
        this MethodInfo methodInfo
    )
    {
        FieldInfo? thisField =
            methodInfo.DeclaringType.GetField("<>4__this")
            ?? throw new NullReferenceException(
                $"'<>4__this' field not found on type {methodInfo.DeclaringType}."
            );

        if (!typeof(T).IsAssignableFrom(thisField.FieldType))
        {
            throw new InvalidCastException(
                $"{typeof(T)} is not assignable from '<>4__this' field type {thisField.FieldType}"
            );
        }

        var dmd = new DynamicMethodDefinition("FastFieldGetter", typeof(T), [typeof(object)]);
        var il = dmd.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, thisField);
        il.Emit(OpCodes.Ret);
        var getter = dmd.Generate().CreateDelegate<EnumeratorFieldGetter<T>>();
        return getter;
    }
}
