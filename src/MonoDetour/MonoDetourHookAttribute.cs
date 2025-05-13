using System;

namespace MonoDetour;

/// <summary>
/// Specifies that MonoDetour will call this method if the type it is in is
/// marked with <see cref="MonoDetourTargetsAttribute"/> and
/// <see cref="MonoDetourManager.InvokeHookInitializers(System.Reflection.Assembly)"/>
/// is called.<br/>
/// <br/>
/// MonoDetour's HookGen will also analyze this method for HookGen usage
/// (On.Namespace.Type.Method...) just like <see cref="MonoDetourHookAnalyzeAttribute"/>
/// so that it can generate the hook methods for those hooks.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookInitAttribute : Attribute { }
