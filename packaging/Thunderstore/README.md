# MonoDetour

Easy and convenient .NET detouring library, powered by MonoMod.RuntimeDetour.

- GitHub: <https://github.com/MonoDetour/MonoDetour>
- Documentation: <https://monodetour.github.io/>

Note: MonoDetour is a library for developers. By itself, it does the following once it's loaded by the runtime:

- `ILHook`s (includes HarmonyX transpilers) will get MonoDetour's CIL analysis on target method compilation when an `InvalidProgramException` is thrown
- MonoMod's `ILLabel`s won't cause InvalidCastExceptions in some `Mono.Cecil.Cil.Instruction` methods, such as `ToString`.

Note: do not depend on this package directly. Instead depend on `MonoDetour_BepInEx_5` as it will integrate MonoDetour's logger with BepInEx. Otherwise logs might not show up due to some BepInEx quirks with MonoDetour's colored logs.

## Why Use MonoDetour?

MonoDetour is an alternative to HarmonyX or MonoMod.RuntimeDetour for detouring. It attempts to improve on the hooking experience as much as possible. See <https://monodetour.github.io/getting-started/why-monodetour/> to learn more.
