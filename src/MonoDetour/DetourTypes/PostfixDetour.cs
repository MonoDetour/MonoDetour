using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the end of the target method.
/// </summary>
public class PostfixDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info { get; set; } = null!;

    /// <inheritdoc/>
    public void Manipulator(ILContext il) => GeneralDetour.Manipulator(il, Info);
}
