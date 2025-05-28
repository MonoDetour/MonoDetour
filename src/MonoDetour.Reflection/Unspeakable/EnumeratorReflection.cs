using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace MonoDetour.Reflection.Unspeakable;

file static class EnumeratorExtensionsCache<T>
{
    /// <summary>
    /// Cache for fields whose id corresponds to a constant field name.
    /// </summary>
    internal static readonly ConcurrentDictionary<
        (Type, int),
        EnumeratorFieldReferenceGetter<T>
    > s_FieldToRef = [];

    /// <summary>
    /// Cache for field id 3 whose names are unknown.
    /// </summary>
    internal static readonly ConcurrentDictionary<
        (Type, string),
        EnumeratorFieldReferenceGetter<T>
    > s_3ToRef = [];
}

/// <summary>
/// Enumerator related reflection helper methods.
/// </summary>
public static class EnumeratorReflection
{
    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceThis{T}(MethodInfo)"/>
    public static void EnumeratorFastFieldReferenceThis<T>(
        this MethodInfo methodInfo,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceThis<T>();

    /// <inheritdoc cref="EnumeratorFastFieldReferenceThis{T}(Type)"/>
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReferenceThis<T>(
        this MethodInfo methodInfo
    ) => methodInfo.DeclaringType.EnumeratorFastFieldReferenceThis<T>();

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceThis{T}(Type)"/>
    public static void EnumeratorFastFieldReferenceThis<T>(
        this Type enumeratorType,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = enumeratorType.EnumeratorFastFieldReferenceThis<T>();

    /// <summary>
    /// Builds or gets a fast field reference getter method for the <c>&lt;&gt;4__this</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(Type, string)"/>
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReferenceThis<T>(
        this Type enumeratorType
    ) => EnumeratorFastFieldReference<T>(enumeratorType, 4);

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceCurrent{T}(MethodInfo)"/>
    public static void EnumeratorFastFieldReferenceCurrent<T>(
        this MethodInfo methodInfo,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceCurrent<T>();

    /// <inheritdoc cref="EnumeratorFastFieldReferenceCurrent{T}(Type)"/>
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReferenceCurrent<T>(
        this MethodInfo methodInfo
    ) => methodInfo.DeclaringType.EnumeratorFastFieldReferenceCurrent<T>();

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceCurrent{T}(Type)"/>
    public static void EnumeratorFastFieldReferenceCurrent<T>(
        this Type enumeratorType,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = enumeratorType.EnumeratorFastFieldReferenceCurrent<T>();

    /// <summary>
    /// Builds or gets and returns a fast field reference getter method for the <c>&lt;&gt;2__current</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(Type, string)"/>
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReferenceCurrent<T>(
        this Type enumeratorType
    ) => EnumeratorFastFieldReference<T>(enumeratorType, 2);

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceState(MethodInfo)"/>
    public static void EnumeratorFastFieldReferenceState(
        this MethodInfo methodInfo,
        ref EnumeratorFieldReferenceGetter<int> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReferenceState();

    /// <inheritdoc cref="EnumeratorFastFieldReferenceState(Type)"/>
    public static EnumeratorFieldReferenceGetter<int> EnumeratorFastFieldReferenceState(
        this MethodInfo methodInfo
    ) => methodInfo.DeclaringType.EnumeratorFastFieldReferenceState();

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReferenceState(Type)"/>
    public static void EnumeratorFastFieldReferenceState(
        this Type enumeratorType,
        ref EnumeratorFieldReferenceGetter<int> enumeratorFieldReference
    ) => enumeratorFieldReference = enumeratorType.EnumeratorFastFieldReferenceState();

    /// <summary>
    /// Builds or gets a fast field reference getter method for the <c>&lt;&gt;1__state</c>
    /// field on an IEnumerator.
    /// </summary>
    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(Type, string)"/>
    public static EnumeratorFieldReferenceGetter<int> EnumeratorFastFieldReferenceState(
        this Type enumeratorType
    ) => EnumeratorFastFieldReference<int>(enumeratorType, 1);

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(MethodInfo, string)"/>
    public static void EnumeratorFastFieldReference<T>(
        this MethodInfo methodInfo,
        string fieldName,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = methodInfo.EnumeratorFastFieldReference<T>(fieldName);

    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(Type, string)"/>
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReference<T>(
        this MethodInfo methodInfo,
        string fieldName
    ) => methodInfo.DeclaringType.EnumeratorFastFieldReference<T>(fieldName);

    /// <returns></returns>
    /// <inheritdoc cref="EnumeratorFastFieldReference{T}(Type, string)"/>
    public static void EnumeratorFastFieldReference<T>(
        this Type enumeratorType,
        string fieldName,
        ref EnumeratorFieldReferenceGetter<T> enumeratorFieldReference
    ) => enumeratorFieldReference = enumeratorType.EnumeratorFastFieldReference<T>(fieldName);

    static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReference<T>(
        this Type enumeratorType,
        int fieldId
    )
    {
        if (
            EnumeratorExtensionsCache<T>.s_FieldToRef.TryGetValue(
                (enumeratorType, fieldId),
                out var fieldRef
            )
        )
        {
            return fieldRef;
        }

        var name = fieldId switch
        {
            1 => "<>1__state",
            2 => "<>2__current",
            3 => throw new ArgumentException("field id 3 is not constant."),
            4 => "<>4__this",
            _ => throw new ArgumentOutOfRangeException(),
        };

        FieldInfo? field =
            enumeratorType.GetField(name, (BindingFlags)~0)
            ?? throw new NullReferenceException(
                $"'{name}' field not found on type {enumeratorType}."
            );

        if (!typeof(T).IsAssignableFrom(field.FieldType))
        {
            throw new InvalidCastException(
                $"{typeof(T)} is not assignable from '{name}' field type {field.FieldType}"
            );
        }

        fieldRef = CreateFastFieldReference<T>(field);
        EnumeratorExtensionsCache<T>.s_FieldToRef.TryAdd((enumeratorType, fieldId), fieldRef);
        return fieldRef;
    }

#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name
    /// <summary>
    /// Builds or gets a fast field reference getter method for a field with the specified
    /// name on an IEnumerator.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="enumeratorType">The type of the enumerator.</param>
    /// <param name="methodInfo">A method of the enumerator.</param>
    /// <param name="fieldName">The exact name of the field.</param>
    /// <param name="enumeratorFieldReference">The field to set.</param>
    /// <returns>A fast field field reference getter method.</returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="InvalidCastException"></exception>
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name
    public static EnumeratorFieldReferenceGetter<T> EnumeratorFastFieldReference<T>(
        this Type enumeratorType,
        string fieldName
    )
    {
        if (
            EnumeratorExtensionsCache<T>.s_3ToRef.TryGetValue(
                (enumeratorType, fieldName),
                out var fieldRef
            )
        )
        {
            return fieldRef;
        }

        FieldInfo? field =
            enumeratorType.GetField(fieldName, (BindingFlags)~0)
            ?? throw new NullReferenceException(
                $"'{fieldName}' field not found on type {enumeratorType}."
            );

        if (!typeof(T).IsAssignableFrom(field.FieldType))
        {
            throw new InvalidCastException(
                $"{typeof(T)} is not assignable from '{fieldName}' field type {field.FieldType}"
            );
        }

        fieldRef = CreateFastFieldReference<T>(field);
        EnumeratorExtensionsCache<T>.s_3ToRef.TryAdd((enumeratorType, fieldName), fieldRef);
        return fieldRef;
    }

    static EnumeratorFieldReferenceGetter<T> CreateFastFieldReference<T>(FieldInfo fieldInfo)
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
        var referenceGetter = dmd.Generate().CreateDelegate<EnumeratorFieldReferenceGetter<T>>();
        return referenceGetter;
    }
}
