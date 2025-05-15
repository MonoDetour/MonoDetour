using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level.
/// </summary>
public class ILHookDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public MonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => Hook.Manipulator.Invoke(null, [il]);
}
