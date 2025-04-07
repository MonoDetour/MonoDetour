using System;
using System.Reflection;
using MonoMod.Cil;

namespace MonoDetour;

/// <summary>
/// Implements MonoDetour support for a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level.
/// </summary>
public class ILHookDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info
    {
        get;
        set
        {
            field = value;
            manipulator = (ILContext.Manipulator)
                Delegate.CreateDelegate(
                    typeof(ILContext.Manipulator),
                    (MethodInfo)value.Data.Manipulator!
                );
        }
    } = null!;

    ILContext.Manipulator manipulator = null!;

    /// <inheritdoc/>
    public void ILHookManipulator(ILContext il) => manipulator(il);
}
