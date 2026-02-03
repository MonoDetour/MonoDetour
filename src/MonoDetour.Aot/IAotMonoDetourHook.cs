using System.Reflection;
using Mono.Cecil;

namespace MonoDetour.Aot;

/// <summary>
/// An interface for an AOT (ahead-of-time) hook.
/// </summary>
public interface IReadOnlyAotMonoDetourHook
{
    /// <summary>
    /// The method to hook.
    /// </summary>
    MethodDefinition Target { get; }

    /// <summary>
    /// The hook or manipulator method.
    /// </summary>
    MethodBase? ManipulatorBase { get; }

    /// <summary>
    /// Manipulator method as a <see cref="MethodDefinition"/>.
    /// </summary>
    MethodDefinition? ManipulatorDefinition { get; }

    /// <summary>
    /// The owner <see cref="AotMonoDetourManager"/> of this hook.
    /// </summary>
    AotMonoDetourManager Owner { get; }

    /// <inheritdoc cref="MonoDetourConfig"/>
    MonoDetourConfig? Config { get; }

    /// <summary>
    /// Gets whether or not this hook is applied.
    /// </summary>
    bool IsApplied { get; }
}
