using System;

namespace MonoDetour;

/// <summary>
/// Tells MonoDetour to analyze this method for HookGen usage (On.Namespace.Type.Method...)
/// so it can generate the hook methods for those hooks.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MonoDetourHookAnalyzeAttribute : Attribute { }
