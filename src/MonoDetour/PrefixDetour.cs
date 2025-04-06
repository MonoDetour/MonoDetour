using MonoMod.Cil;

namespace MonoDetour;

public class PrefixDetour : IMonoDetourHookEmitter
{
    public MonoDetourInfo Info { get; set; } = null!;

    public void ILHookManipulator(ILContext il) => GenericDetour.Manipulator(il, Info);
}
