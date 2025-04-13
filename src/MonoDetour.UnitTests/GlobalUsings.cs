global using System.Collections;
global using System.Diagnostics.CodeAnalysis;
global using Mono.Cecil.Cil;
global using MonoDetour.HookGen;
global using MonoDetour.UnitTests.TestLib;
global using MonoMod.Cil;
global using MonoMod.RuntimeDetour;
global using MonoMod.Utils;
global using On.MonoDetour.UnitTests.TestLib.LibraryMethods;

[assembly: MonoDetour.MonoDetourTargets(typeof(LibraryMethods))]
