using Mono.Cecil;
using MonoMod.Cil;

namespace MonoDetour.Cil;

/// <summary>
/// Extension methods for <see cref="MethodDefinition"/>.
/// </summary>
public static class MethodDefinitionExtensions
{
    /// <summary>
    /// Manipulate a <see cref="MethodDefinition"/> in an ILHook-like environment
    /// without being in a real ILHook. Useful when runtime detouring is not possible,
    /// such as when writing IL modifications in BepInEx preloader patchers.<br/>
    /// <br/>
    /// Using this method while already in an ILHook is pointless.
    /// </summary>
    /// <param name="method">The target method to manipulate.</param>
    /// <param name="manipulator">An IL manipulator method accepting a <see cref="ILManipulationInfo"/>.</param>
    public static void ILWeave(
        this MethodDefinition method,
        ILManipulationInfo.Manipulator manipulator
    )
    {
        using var context = new ILContext(method);

        context.Invoke(il =>
        {
            ILManipulationInfo info = new(il);
            manipulator(info);
        });
    }
}
