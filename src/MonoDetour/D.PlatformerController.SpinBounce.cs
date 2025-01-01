using System.ComponentModel;
using System.Reflection;
using MonoDetour;

namespace On.PlatformerController;

internal static class SpinBounce
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal delegate void Hook_SpinBounce(ref Args args);

    public ref struct Args
    {
        public global::PlatformerController self;
        public float power;
    }

    internal static void Prefix(Hook_SpinBounce args) =>
        DetourManager.Hook(args.Method);

    public static MethodBase Target() =>
        typeof(global::PlatformerController).GetMethod(nameof(global::PlatformerController.SpinBounce));
}

