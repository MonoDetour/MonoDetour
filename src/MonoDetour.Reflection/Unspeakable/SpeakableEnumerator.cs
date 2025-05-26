using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonoDetour.Reflection.Unspeakable;

/// <summary>
/// Wraps an <see cref="IEnumerator{T}"/> object.
/// </summary>
public interface ISpeakableEnumerator;

/// <summary>
/// A class that wraps an <see cref="IEnumerator{T}"/> object.
/// Allows access to basic fields of the IEnumerator.
/// </summary>
/// <typeparam name="TCurrent">The type of Current.</typeparam>
/// <typeparam name="TThis">The declaring type.</typeparam>
public sealed class SpeakableEnumerator<TCurrent, TThis> : ISpeakableEnumerator
{
    /// <summary>
    /// Gets the instance of the class that constructed this enumerator.
    /// </summary>
    public TThis DeclaringThis => getThisRef(instance);

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.This"/>
    public IEnumerator<TCurrent> This => instance;

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.Current"/>
    public TCurrent Current
    {
        get => getCurrentRef(instance);
        set => getCurrentRef(instance) = value;
    }

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.State"/>
    public int State
    {
        get => getStateRef(instance);
        set => getStateRef(instance) = value;
    }

    readonly FieldReferenceGetter<TThis> getThisRef;
    readonly FieldReferenceGetter<TCurrent> getCurrentRef;
    readonly FieldReferenceGetter<int> getStateRef;
    readonly IEnumerator<TCurrent> instance;

    static readonly ConditionalWeakTable<
        IEnumerator<TCurrent>,
        SpeakableEnumerator<TCurrent, TThis>
    > s_EnumeratorToSpeakable = new();

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent, TThis}"/>
    /// <param name="instance">An enumerator instance.</param>
    public SpeakableEnumerator(IEnumerator<TCurrent> instance)
    {
        var type = instance.GetType();
        this.instance = instance;
        getCurrentRef = type.EnumeratorFastFieldReferenceCurrent<TCurrent>();
        getStateRef = type.EnumeratorFastFieldReferenceState();
        getThisRef = type.EnumeratorFastFieldReferenceThis<TThis>();
    }

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.PreBuildFieldReferenceGetters(Type)"/>
    public static void PreBuildFieldReferenceGetters(Type type)
    {
        SpeakableEnumerator<TCurrent>.PreBuildFieldReferenceGetters(type);
        type.EnumeratorFastFieldReferenceThis<TThis>();
    }

    /// <summary>
    /// Gets or creates a <see cref="SpeakableEnumerator{TCurrent, TThis}"/> for the
    /// specified instance.
    /// </summary>
    /// <param name="instance">An enumerator instance.</param>
    /// <returns>A new or existing <see cref="SpeakableEnumerator{TCurrent, TThis}"/>.</returns>
    public static SpeakableEnumerator<TCurrent, TThis> GetOrCreate(IEnumerator<TCurrent> instance)
    {
        if (s_EnumeratorToSpeakable.TryGetValue(instance, out var value))
        {
            return value;
        }

        value = new(instance);
        s_EnumeratorToSpeakable.Add(instance, value);
        return value;
    }
}

/// <inheritdoc cref="SpeakableEnumerator{TCurrent, TThis}"/>
public sealed class SpeakableEnumerator<TCurrent> : ISpeakableEnumerator
{
    /// <summary>
    /// Gets the instance of this IEnumerator. Note that this is
    /// not the instance of the class that constructed this enumerator.
    /// </summary>
    public IEnumerator<TCurrent> This => instance;

    /// <summary>
    /// Gets the current value of this enumerator.
    /// </summary>
    public TCurrent Current
    {
        get => getCurrentRef(instance);
        set => getCurrentRef(instance) = value;
    }

    /// <summary>
    /// Gets the state value of this enumerator.
    /// </summary>
    public int State
    {
        get => getStateRef(instance);
        set => getStateRef(instance) = value;
    }

    readonly FieldReferenceGetter<TCurrent> getCurrentRef;
    readonly FieldReferenceGetter<int> getStateRef;
    readonly IEnumerator<TCurrent> instance;

    static readonly ConditionalWeakTable<
        IEnumerator<TCurrent>,
        SpeakableEnumerator<TCurrent>
    > s_EnumeratorToSpeakable = new();

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent, TThis}"/>
    /// <param name="instance">An enumerator instance.</param>
    public SpeakableEnumerator(IEnumerator<TCurrent> instance)
    {
        var type = instance.GetType();
        this.instance = instance;
        getCurrentRef = type.EnumeratorFastFieldReferenceCurrent<TCurrent>();
        getStateRef = type.EnumeratorFastFieldReferenceState();
    }

    /// <summary>
    /// Can be used for builds field reference getter methods ahead of time
    /// to prevent freezes after hook initialization.
    /// </summary>
    /// <remarks>
    /// MonoDetour already uses this where it uses SpeakableEnumerator types.
    /// </remarks>
    /// <param name="type">The type of Enumerator to build field reference getters for.</param>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static void PreBuildFieldReferenceGetters(Type type)
    {
        type.EnumeratorFastFieldReferenceCurrent<TCurrent>();
        type.EnumeratorFastFieldReferenceState();
    }

    /// <summary>
    /// Gets or creates a <see cref="SpeakableEnumerator{TCurrent}"/> for the
    /// specified instance.
    /// </summary>
    /// <param name="instance">An enumerator instance.</param>
    /// <returns>A new or existing <see cref="SpeakableEnumerator{TCurrent}"/>.</returns>
    public static SpeakableEnumerator<TCurrent> GetOrCreate(IEnumerator<TCurrent> instance)
    {
        if (s_EnumeratorToSpeakable.TryGetValue(instance, out var value))
        {
            return value;
        }

        value = new(instance);
        s_EnumeratorToSpeakable.Add(instance, value);
        return value;
    }
}
