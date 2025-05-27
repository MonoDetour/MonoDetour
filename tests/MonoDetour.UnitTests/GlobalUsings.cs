global using Mono.Cecil.Cil;
global using MonoDetour.DetourTypes;
global using MonoDetour.HookGen;
global using MonoDetour.Logging;
global using MonoDetour.UnitTests.TestLib;
global using MonoMod.Cil;
global using MonoMod.RuntimeDetour;
global using MonoMod.Utils;
global using On.MonoDetour.UnitTests.TestLib.LibraryMethods;

[assembly: MonoDetourTargets(typeof(LibraryMethods))]
[assembly: MonoDetourTargets(typeof(SomeType))]
