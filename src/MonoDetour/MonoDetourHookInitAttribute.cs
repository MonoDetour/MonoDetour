using System;

namespace MonoDetour;

/// <summary>
/// Specifies that MonoDetour will call this method if the type it is in is
/// marked with <see cref="MonoDetourTargetsAttribute"/> and
/// <see cref="MonoDetourManager.InvokeHookInitializers(System.Reflection.Assembly)"/>
/// is called.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookInitAttribute : Attribute { }
