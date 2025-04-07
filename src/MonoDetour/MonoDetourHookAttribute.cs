using System;
using System.ComponentModel;

namespace MonoDetour;

/// <typeparam name="T">The type which specifies how to apply and treat this hook.</typeparam>
/// <inheritdoc/>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookAttribute<T>() : MonoDetourHookAttribute(typeof(T))
    where T : IMonoDetourHookEmitter { }

/// <summary>
/// Specifies that MonoDetour will hook the method if the type it is in is
/// marked with <see cref="MonoDetourTargetsAttribute"/> and
/// <see cref="MonoDetourManager.HookAllInAssembly(System.Reflection.Assembly)"/>
/// is called.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookAttribute : Attribute
{
    /// <inheritdoc cref="MonoDetourInfo"/>
    public MonoDetourInfo Info { get; set; }

    /// <inheritdoc cref="MonoDetourHookAttribute"/>
    public MonoDetourHookAttribute(DetourType detourType)
        : this(MonoDetourInfo.GetTypeFromDetourType(detourType)) { }

    /// <remarks>
    /// Only types which implement <see cref="IMonoDetourHookEmitter"/>
    /// are valid values for this constructor.
    /// </remarks>
    /// <param name="detourType">The type which specifies how to apply and treat this hook.</param>
    /// <inheritdoc cref="MonoDetourHookAttribute"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public MonoDetourHookAttribute(Type detourType)
    {
        Info = new(detourType);
    }
}
