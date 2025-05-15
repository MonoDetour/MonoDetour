using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the start of the target method.
/// </summary>
public class PrefixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public MonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => GeneralDetour.Manipulator(il, Hook);
}
