# MonoDetour BepInEx 5

This plugin provides:

- BepInEx logger integration for MonoDetour
- HarmonyX interop for MonoDetour
- Initializes MonoDetour early as a side effect

MonoDetour initializing early means that everyone after will get the following:

- `ILHook`s (includes HarmonyX transpilers) will get MonoDetour's CIL analysis on target method compilation when an `InvalidProgramException` is thrown
- If an `ILHook` manipulator method throws on legacy MonoMod.RuntimeDetour, it will be disabled so the target method can be ILHooked successfully later
- MonoMod's `ILLabel`s won't cause InvalidCastExceptions in some `Mono.Cecil.Cil.Instruction` methods, such as `ToString`.
