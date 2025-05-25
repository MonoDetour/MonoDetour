using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonoDetour.Reflection.Unspeakable;

/// <summary>
/// A class that wraps an <see cref="IEnumerator{T}"/> object.
/// Allows access to basic fields of the IEnumerator.
/// </summary>
/// <typeparam name="TCurrent">The type of Current.</typeparam>
/// <typeparam name="TThis">The declaring type.</typeparam>
public sealed class SpeakableEnumerator<TCurrent, TThis>
{
    /// <summary>
    /// Gets the instance of the class that constructed this enumerator.
    /// </summary>
    public TThis DeclaringThis => thisRef(instance);

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.This"/>
    public IEnumerator<TCurrent> This => instance;

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.Current"/>
    public TCurrent Current
    {
        get => currentRef(instance);
        set => currentRef(instance) = value;
    }

    /// <inheritdoc cref="SpeakableEnumerator{TCurrent}.State"/>
    public int State
    {
        get => stateRef(instance);
        set => stateRef(instance) = value;
    }

    readonly ReferenceField<TThis> thisRef;
    readonly ReferenceField<TCurrent> currentRef;
    readonly ReferenceField<int> stateRef;
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
        currentRef = type.EnumeratorFastFieldReferenceCurrent<TCurrent>();
        stateRef = type.EnumeratorFastFieldReferenceState();
        thisRef = type.EnumeratorFastFieldReferenceThis<TThis>();
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
public sealed class SpeakableEnumerator<TCurrent>
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
        get => currentRef(instance);
        set => currentRef(instance) = value;
    }

    /// <summary>
    /// Gets the state value of this enumerator.
    /// </summary>
    public int State
    {
        get => stateRef(instance);
        set => stateRef(instance) = value;
    }

    readonly ReferenceField<TCurrent> currentRef;
    readonly ReferenceField<int> stateRef;
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
        currentRef = type.EnumeratorFastFieldReferenceCurrent<TCurrent>();
        stateRef = type.EnumeratorFastFieldReferenceState();
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
