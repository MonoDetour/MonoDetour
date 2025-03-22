using System;
using System.ComponentModel;
using System.Reflection;
using MonoDetour;
using MonoMod.Cil;

namespace On.PlatformerController;

public static class SpinBounce
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public delegate void MethodParams(ref Params args);

    public ref struct Params
    {
#if NET7_0_OR_GREATER
        public ref global::PlatformerController self;
        public ref float power;
#else
        public global::PlatformerController self;
        public float? power;
#endif
    }

    public static void Prefix(global::MonoDetour.HookManager manager, MethodParams args) =>
        manager.HookGenReflectedHook(args, new() { DetourType = DetourType.Prefix, Manipulator = args });

    public static void Postfix(global::MonoDetour.HookManager manager, MethodParams args) =>
        manager.HookGenReflectedHook(args, new() { DetourType = DetourType.Postfix, Manipulator = args });

    public static void ILHook(global::MonoDetour.HookManager manager, global::MonoMod.Cil.ILContext.Manipulator manipulator) =>
        manager.Hook(Target(), manipulator);
        // manager.HookGenReflectedHook(Target(), manipulator.Method, new() { DetourType = DetourType.ILHook });

    public static MethodBase Target() =>
        typeof(global::PlatformerController).GetMethod(nameof(global::PlatformerController.SpinBounce));
}
