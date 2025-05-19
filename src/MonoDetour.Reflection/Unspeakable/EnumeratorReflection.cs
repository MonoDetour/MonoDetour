using System;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace MonoDetour.Reflection.Unspeakable;

/// <summary>
/// Enumerator related reflection helper methods.
/// </summary>
public static class EnumeratorReflection
{
    /// <summary>
    /// Builds and sets a fast field reference getter method for the <c>&lt;&gt;4__this</c>
    /// field on an IEnumerator to <paramref name="enumeratorFieldReference"/>.
    /// </summary>
    /// <param name="enumeratorFieldReference">The field to set.</param>
    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceThis{T}(MethodInfo)"/>
    /// <param name="methodInfo"></param>
    public static void EnumeratorFastFieldReferenceThis<T>(
        this MethodInfo methodInfo,
        ref FieldReference<T> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceThis<T>();

    /// <summary>
    /// Builds and returns a fast field reference getter method for the <c>&lt;&gt;4__this</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="methodInfo">A method of the enumerator.</param>
    /// <returns>A fast field field reference getter method.</returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static FieldReference<T> EnumeratorFastFieldReferenceThis<T>(this MethodInfo methodInfo)
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

        return CreateFastFieldReference<T>(thisField);
    }

    /// <summary>
    /// Builds and sets a fast reference getter method for the <c>&lt;&gt;2__current</c>
    /// field on an IEnumerator to <paramref name="enumeratorFieldReference"/>.
    /// </summary>
    /// <param name="enumeratorFieldReference">The field to set.</param>
    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceCurrent{T}(MethodInfo)"/>
    /// <param name="methodInfo"></param>
    public static void EnumeratorFastFieldReferenceCurrent<T>(
        this MethodInfo methodInfo,
        ref FieldReference<T> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceCurrent<T>();

    /// <summary>
    /// Builds and returns a fast reference getter method for the <c>&lt;&gt;2__current</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="methodInfo">A method of the enumerator.</param>
    /// <returns>A fast field Reference method.</returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static FieldReference<T> EnumeratorFastFieldReferenceCurrent<T>(
        this MethodInfo methodInfo
    )
    {
        FieldInfo? currentField =
            methodInfo.DeclaringType.GetField(
                "<>2__current",
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            ?? throw new NullReferenceException(
                $"'<>2__current' field not found on type {methodInfo.DeclaringType}."
            );

        if (!typeof(T).IsAssignableFrom(currentField.FieldType))
        {
            throw new InvalidCastException(
                $"{typeof(T)} is not assignable from '<>2__current' field type {currentField.FieldType}"
            );
        }

        return CreateFastFieldReference<T>(currentField);
    }

    /// <summary>
    /// Builds and sets a fast field reference getter method for the <c>&lt;&gt;1__state</c>
    /// field on an IEnumerator to <paramref name="enumeratorFieldReference"/>.
    /// </summary>
    /// <param name="enumeratorFieldReference">The field to set.</param>
    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceState(MethodInfo)"/>
    /// <param name="methodInfo"></param>
    public static void EnumeratorFastFieldReferenceState(
        this MethodInfo methodInfo,
        ref FieldReference<int> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceState();

    /// <summary>
    /// Builds and returns a fast field reference getter method for the <c>&lt;&gt;1__state</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <param name="methodInfo">A method of the enumerator.</param>
    /// <returns>A fast field field reference getter method.</returns>
    /// <exception cref="NullReferenceException"></exception>
    public static FieldReference<int> EnumeratorFastFieldReferenceState(this MethodInfo methodInfo)
    {
        FieldInfo? stateField =
            methodInfo.DeclaringType.GetField(
                "<>1__state",
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            ?? throw new NullReferenceException(
                $"'<>1__state' field not found on type {methodInfo.DeclaringType}."
            );

        return CreateFastFieldReference<int>(stateField);
    }

    static FieldReference<T> CreateFastFieldReference<T>(FieldInfo fieldInfo)
    {
        var dmd = new DynamicMethodDefinition(
            "FastFieldReference",
            typeof(T).MakeByRefType(),
            [typeof(object)]
        );
        var il = dmd.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, fieldInfo);
        il.Emit(OpCodes.Ret);
        var referenceGetter = dmd.Generate().CreateDelegate<FieldReference<T>>();
        return referenceGetter;
    }
}
