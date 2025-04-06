using System;
using System.Reflection;
using MonoMod.Cil;

namespace MonoDetour;

public class ILHookDetour : IMonoDetourHookEmitter
{
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

    public void ILHookManipulator(ILContext il) => manipulator(il);
}
