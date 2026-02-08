using Mono.Cecil;

namespace MonoDetour.Aot.DetourTypes;

/// <summary>
/// A type which implements this interface can be used as an AOT
/// detour type.
/// </summary>
/// <remarks>
/// For MonoDetour to be able to use a type which implements this
/// interface, the type must have a parameterless constructor.
/// </remarks>
public interface IAotMonoDetourHookApplier
{
    /// <summary>
    /// All the available metadata for the <see cref="AotMonoDetourHook"/>.
    /// </summary>
    IReadOnlyAotMonoDetourHook AotHook { get; set; }

    /// <summary>
    /// The manipulator method that is called
    /// when the <see cref="AotMonoDetourHook"/> is applied.
    /// </summary>
    /// <param name="method">The target method to manipulate.</param>
    void ApplierManipulator(MethodDefinition method);
}
