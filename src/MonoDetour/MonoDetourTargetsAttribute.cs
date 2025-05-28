using System.Reflection;

namespace MonoDetour;

/// <summary>
/// An attribute with this interface will be used by
/// <see cref="MonoDetourManager.InvokeHookInitializers(Assembly)"/>
/// to search that class for static methods marked with
/// <see cref="MonoDetourHookInitializeAttribute"/> to invoke them.
/// </summary>
public interface IMonoDetourTargets;
