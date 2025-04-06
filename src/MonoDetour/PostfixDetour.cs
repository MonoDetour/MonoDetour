using MonoMod.Cil;

namespace MonoDetour;

public class PostfixDetour : IMonoDetourHookEmitter
{
    public MonoDetourInfo Info { get; set; } = null!;

    public void ILHookManipulator(ILContext il) => GenericDetour.Manipulator(il, Info);
}
