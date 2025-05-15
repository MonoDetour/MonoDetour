using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the end of the target method.
/// </summary>
public class PostfixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public MonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => GeneralDetour.Manipulator(il, Hook);
}
