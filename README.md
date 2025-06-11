# MonoDetour

| MonoDetour | MonoDetour.HookGen | Community Support |
|:-:|:-:|:-:|
| [![MonoDetour](https://img.shields.io/nuget/v/MonoDetour?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour) | [![MonoDetour.HookGen](https://img.shields.io/nuget/v/MonoDetour.HookGen?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour.HookGen) | [![Discord](https://img.shields.io/discord/1377047282381361152?style=for-the-badge&label=Discord)](<https://discord.gg/Pt2BsA2cP4>) |

Easy and convenient .NET detouring library based around HookGen with C# source generators, powered by MonoMod.RuntimeDetour.

> [!WARNING]
> This library is not fully released, and things *will* change.
>
> - Bugs and missing functionality is expected
> - Documentation may not reflect reality
>
> Major things missing for you may be:
>
> - Currently there is no HarmonyX interoperability
>   - Hooks that change control flow (HarmonyX prefix return false) will ignore MonoDetour/HarmonyX

## Documentation

- Website: <https://monodetour.github.io/>
- Join the Discord server for further support: <https://discord.gg/Pt2BsA2cP4>

## Features

- HookGen: Hooking any non-generic method is as easy as `On.Namespace.Type.Method.Prefix/Postfix/ILHook(MyHook);`
  - Compiler generated unspeakable `IEnumerator` type instances are wrapped by a `SpeakableEnumerator` type, allowing easy access to standard fields; see [Hooking IEnumerators](<https://monodetour.github.io/hooking/ienumerators/>)
- Prefix and Postfix hooks which throw will be caught and immediately disposed, including all of the owner MonoDetourManger's hooks
  - `MonoDetourManger` includes an event to gracefully disable your mod when any of its hooks throw
- Extensible DetourType system: create your own e.g. `RPCPrefixDetour` for Unity NGO source generated RPC methods (TODO: HookGen integration)
  - MonoDetour hooks are just MonoMod `ILHook` wrappers which e.g. insert a call to your hook method
  - MonoDetour has 3 built-in detour types: `PrefixDetour`, `PostfixDetour`, and `ILHookDetour`
- IL manipulation API `ILWeaver` which includes features such as matching against the "original" state of the target method's instructions as a fallback
  - Makes any matching statements much more robust without additional effort
- Advanced CIL analysis in stack traces on `ILHook` manipulation target method throwing on compilation

## Why

Full answer: <https://monodetour.github.io/getting-started/why-monodetour/>

### TL;DR

MonoDetour hooks are similar to Harmony patches:

```cs
// Apply this hook somewhere
On.Lib.TargetClass.TakeAndReturnInt.Prefix(Prefix_TakeAndReturnInt);
// ...
static void Prefix_TakeAndReturnInt(TargetClass self, ref int number)
{
    // ...
}
```

And here is HarmonyX:

```cs
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TakeAndReturnInt))]
[HarmonyPrefix]
static void Prefix_TakeAndReturnInt(TargetClass __instance, ref int number)
{
    // ...
}
```

So why would you want to use this? Well, HarmonyX doesn't have HookGen, MonoMod HookGen v1 has issues (see full answer), and MonoMod HookGen v2 isn't finished and it doesn't generate the kind of hooks MonoDetour wants anyways. MonoMod HookGen v2 is great though, so MonoDetour's HookGen is based off of it.

You may be thinking that you don't need HookGen, and you are right. But it's so convenient when you use it that it hurts to go back. And new situations you might not have faced before such as hooking [`IEnumerator`](<https://monodetour.github.io/hooking/ienumerators/>) methods or methods with overloads is made easy thanks to HookGen! MonoDetour is a lot more than just HookGen though, even if that is the main selling point.

MonoDetour attempts to be the easiest and most convenient detouring library, and comes with great documentation (which is still a WIP)! MonoDetour has its own IL manipulation API called `ILWeaver` which (I promise) will offer the most extensive documentation of any IL manipulation APIs once it's finished.

## Add to Your Project

Change the version number to optimally the newest:

```xml
<ItemGroup>
  <PackageReference Include="MonoDetour.HookGen" Version="0.6.3" PrivateAssets="all" />
  <PackageReference Include="MonoDetour" Version="0.5.4" />
</ItemGroup>
```

MonoDetour.HookGen will automatically bring in the oldest MonoDetour reference it supports, so it's a good idea to specify the version for both.

Additionally MonoDetour automatically brings in the oldest MonoMod.RuntimeDetour version it supports, so also specify its version to the one you want (or don't if it's included by e.g. BepInEx references). MonoDetour should support MonoMod.RuntimeDetour versions 21.12.13.1 and 25.*, with possibly anything in between.

Note that MonoDetour.HookGen will strip unused generated hooks when building with `Release` configuration by default when `MonoDetourHookGenStripUnusedHooks` is undefined (evaluates to empty string).

You can configure this setting yourself however you wish, such as replicating the default behavior:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <MonoDetourHookGenStripUnusedHooks>true</MonoDetourHookGenStripUnusedHooks>
</PropertyGroup>
```

Having this setting enabled will be more expensive generation wise and intellisense may not keep up when writing hooks, e.g. Prefix, Postfix and ILHook methods may not immediately appear when typing `On.Namespace.Type.Method.` even if they have just been generated.

If the default HookGen namespace `On` causes collisions or you just don't like it, you can set it with the following property:

```xml
<PropertyGroup>
  <MonoDetourHookGenNamespace>On</MonoDetourHookGenNamespace>
</PropertyGroup>
```

## Usage

MonoDetour is mainly designed to be used with HookGen. That is, MonoDetour generates helpers hooks to make hooking easy.

You can use the generated hooks like so:

```cs
[MonoDetourTargets(typeof(SomeType))]
class SomeTypeHooks
{
    [MonoDetourHookInit]
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
}
```

See <https://monodetour.github.io/hooking/normal-methods/> for more information on hooking.

MonoDetour relies on `ILHook`s for hooking similar to HarmonyX. But instead of having monolithic `ILHook` methods like in HarmonyX, MonoDetour maps every hook to a unique `ILHook`.

## Core Concepts

### MonoDetourManager

<https://monodetour.github.io/getting-started/monodetourmanager/>

## How Does the HookGen Work?

<https://monodetour.github.io/getting-started/hookgen/>

## Credits

The HookGen source generator is *heavily* based on [MonoMod.HookGen.V2](<https://github.com/MonoMod/MonoMod/tree/hookgenv2>).
Without it, this project probably wouldn't exist.
