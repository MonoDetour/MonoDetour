// using System.ComponentModel;
// using System.Reflection;
// using MonoDetour;

// namespace D.PlatformerController;

// public static class DoStuff
// {
//     [EditorBrowsable(EditorBrowsableState.Never)]
//     public delegate void MethodParams(ref Params args);

//     public ref struct Params
//     {
// #if NET7_0_OR_GREATER
//         public ref global::PlatformerController self;
// #else
//         public global::PlatformerController self;
// #endif
//     }

//     public static void Prefix(global::MonoDetour.MonoDetourManager manager, MethodParams args) =>
//         manager.HookGenReflectedHook(args, new(DetourType.Prefix));

//     public static void Postfix(global::MonoDetour.MonoDetourManager manager, MethodParams args) =>
//         manager.HookGenReflectedHook(args, new(DetourType.Postfix));

//     public static MethodBase Target() =>
//         typeof(global::PlatformerController).GetMethod(
//             nameof(global::PlatformerController.DoStuff)
//         )!;
// }
