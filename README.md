# MonoDetour

A `MonoMod.RuntimeDetour.ILHook` wrapper optimized for convenience, based around HookGen with C# source generators.

> [!NOTE]
> Also see related project [MonoDetour.ILWeaver](./src/MonoDetour.ILWeaver/README.md), a redesigned ILCursor with a focus on hand-holding.

## Documentation

<https://monodetour.github.io/>

## Usage

MonoDetour is mainly designed to be used with HookGen. That is, MonoDetour generates helpers hooks to make hooking easy.

You can use the generated hooks like so:

```cs
internal static void InitHooks()
{
    // Note: this is using the default generated MonoDetourManager
    // MonoDetour.HookGen.DefaultMonoDetourManager.Instance by default.
    // Use it for managing your hooks.
    On.SomeNamespace.SomeType.SomeMethod.Prefix(Prefix_SomeType_SomeMethod);
}

static void Prefix_SomeType_SomeMethod(SomeType self)
{
    Console.WriteLine("Hello from Prefix hook 1!");
}
```

MonoDetour entirely relies on `ILHook`s for hooking similar to HarmonyX. But instead of having monolithic `ILHook` methods like in HarmonyX, MonoDetour maps every hook to a unique `ILHook`.

## Core Concepts

### MonoDetourManager

<https://monodetour.github.io/getting-started/monodetourmanager/>

## Why?

<https://monodetour.github.io/getting-started/why-monodetour/>

## How Do I Use It?

Right now, you don't. While it works, it's way early for use and the API will change.

## How Does the HookGen Work?

<https://monodetour.github.io/getting-started/hookgen/>

## Credits

The HookGen source generator is *heavily* based on [MonoMod.HookGen.V2](<https://github.com/MonoMod/MonoMod/tree/hookgenv2>).
Without it, the source generator would have been a nightmare to implement myself, as I essentially have zero experience with them.
