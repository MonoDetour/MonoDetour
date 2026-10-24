# MonoDetour.Interop.HarmonyX

Initialize it:

```cs
MonoDetour.Interop.HarmonyX.HarmonyXInterop.Initialize();
```

> [!NOTE]  
> Only reference this package if you are e.g. shipping a MonoDetour compatibility integration for a specific mod loader. Plugins for the mod loader which use MonoDetour have no need to reference this package as it's expected that an integration plugin calls `MonoDetour.Interop.HarmonyX.HarmonyXInterop.Initialize();` as early as possible.

## What It Does

This optional MonoDetour submodule applies runtime patches to HarmonyX in order to make it communicate its modifications to MonoDetour.

The goal is to solve these issues:

- Hooks from MonoDetour and HarmonyX which skip the original method's instructions but still intend to run all written prefixes/postfixes aren't aware of each other, causing some hooks to be skipped.
- HarmonyX's ILHook which applies HarmonyX patches rewrites all instructions in the target method, but MonoDetour wants to keep track of them for certain ILWeaver features (`ILWeaver.MatchRelaxed`).
