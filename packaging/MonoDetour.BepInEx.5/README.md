# MonoDetour BepInEx 5

This plugin provides BepInEx logger integration for MonoDetour and as a side effect causes MonoDetour to initialize early.

MonoDetour initializing early means that everyone after will get the following:

- `ILHook`s (includes HarmonyX transpilers) will get MonoDetour's CIL analysis on target method compilation when an `InvalidProgramException` is thrown
- MonoMod's `ILLabel`s won't cause InvalidCastExceptions in some `Mono.Cecil.Cil.Instruction` methods, such as `ToString`.
