using System;
using System.ComponentModel;

namespace MonoDetour;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookAttribute<T>() : MonoDetourHookAttribute(typeof(T))
    where T : IMonoDetourHookEmitter { }

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookAttribute : Attribute, IMonoDetourHookAttribute
{
    public MonoDetourInfo Info { get; set; }

    public MonoDetourHookAttribute(DetourType detourType)
        : this(MonoDetourInfo.GetTypeFromDetourType(detourType)) { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public MonoDetourHookAttribute(Type detourType)
    {
        Info = new(detourType);
    }
}

public interface IMonoDetourHookAttribute { }
