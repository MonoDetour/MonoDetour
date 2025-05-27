using System;

namespace MonoDetour;

/// <summary>
/// Specifies that MonoDetour will call this method if the type it is in is
/// marked with an attribute implementing <see cref="IMonoDetourTargets"/> and
/// <see cref="MonoDetourManager.InvokeHookInitializers()"/> is called.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookInitializeAttribute : Attribute { }
