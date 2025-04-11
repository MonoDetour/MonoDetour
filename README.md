# MonoDetour

A highly experimental `MonoMod.RuntimeDetour.ILHook` wrapper optimized for convenience, based around HookGen with C# source generators.

> [!NOTE]
> Also see related project [MonoDetour.ILWeaver](./src/MonoDetour.ILWeaver/README.md), a redesigned ILCursor with a focus on hand-holding.

## Documentation

<https://monodetour.github.io/>

## Usage

In MonoDetour, target method's parameters are passed in as a struct, making it easy to discover what's possible.

You can use generated hooks directly for hooking like with MonoMod's HookGen:

```cs
internal static void InitHooks()
{
    // Note: this is using the default generated MonoDetourManager
    // MonoDetour.HookGen.DefaultMonoDetourManager.Instance by default.
    // Use it for managing your hooks.
    On.SomeNamespace.SomeType.SomeMethod.Prefix(Prefix_SomeType_SomeMethod);
}

static void Prefix_SomeType_SomeMethod(
    ref On.SomeNamespace.SomeType.SomeMethod.Params args)
{
    Console.WriteLine("Hello from Prefix hook 1!");
}
```

Or you can do things the Harmony way:

```cs
using MonoDetour;
using MonoDetour.HookGen;

// Tell MonoDetourManager to look for MonoDetourHook methods in this type.
// Also tells HookGen to generate hooks for the specified type.
[MonoDetourTargets<SomeType>]
class SomeTypeHooks
{
    internal static void InitHooks()
    {
        // HookAll using the generated MonoDetourManager instance for this assembly.
        DefaultMonoDetourManager.Instance.HookAll();
    }

    // Via enum. Maps to MonoDetour.PrefixDetour as seen in next hook.
    [MonoDetourHook(DetourType.PrefixDetour)]
    static void Prefix2_SomeType_SomeMethod(
        ref On.SomeNamespace.SomeType.SomeMethod.Params args)
    {
        Console.WriteLine("Hello from Prefix hook 2!");
    }

    // Via class that implements MonoDetour.IMonoDetourHookEmitter
    [MonoDetourHook<PrefixDetour>]
    static void Prefix3_SomeType_SomeMethod(
        ref On.SomeNamespace.SomeType.SomeMethod.Params args)
    {
        Console.WriteLine("Hello from Prefix hook 3!");
    }
}
```

MonoDetour entirely relies on `ILHook`s for hooking similar to HarmonyX. But instead of having monolithic `ILHook` methods like in HarmonyX, MonoDetour maps every hook to a unique `ILHook`.

## Core Concepts

### MonoDetourManager

<https://monodetour.github.io/getting-started/monodetourmanager/>

## Why?

MonoMod.RuntimeDetour.HookGen isn't perfect, and HarmonyX doesn't have HookGen. And I simply had an idea, and a goal to make the perfect hooking API for myself.

I do hope though that this will be useful to other people too, and as such this project has (some) documentation.

## How Do I Use It?

Right now, you don't. While it works, it's way early for use and the API will change.

## How Does the HookGen Work?

<https://monodetour.github.io/getting-started/hookgen/>

## Credits

The HookGen source generator is *heavily* based on [MonoMod.HookGen.V2](<https://github.com/MonoMod/MonoMod/tree/hookgenv2>).
Without it, the source generator would have been a nightmare to implement myself, as I essentially have zero experience with them.
