# MonoDetour BepInEx 5 Resonite

Full MonoDetour package (`MonoDetour` + `MonoDetour.BepInEx.5`) as a Resonite renderer preloader plugin.

After MonoDetour has initialized, everyone will get the following:

- `ILHook`s (includes HarmonyX transpilers) will get MonoDetour's CIL analysis on target method compilation when an `InvalidProgramException` is thrown
- MonoMod's `ILLabel`s won't cause InvalidCastExceptions in some `Mono.Cecil.Cil.Instruction` methods, such as `ToString`.
