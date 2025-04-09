# MonoDetour

A highly experimental `MonoMod.RuntimeDetour.ILHook` wrapper, optimized for convenience, and based around HookGen with C# source generators.

> [!NOTE]
> Also see related project [MonoDetour.ILWeaver](./src/MonoDetour.ILWeaver/README.md), a redesigned ILCursor with a focus on hand-holding.

## Usage

In MonoDetour, target method's parameters are passed in as a struct, making it easy to discover what's possible.

You can use generated hooks directly for hooking like with MonoMod's HookGen:

```cs
internal static void InitHooks()
{
    // Note: this is using the default generated MonoDetourManager
    // MonoDetour.HookGen.HookGenManager.Instance by default.
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
        HookGenManager.Instance.HookAll();
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

Every hook made with MonoDetour is attached to a `MonoDetour.MonoDetourManager` object.
When no `MonoDetourManager` object is specified, MonoDetour will use the default `MonoDetour.HookGen.HookGenManager.Instance` it has generated for your assembly. You can use that manager for managing your hooks, or you can create your own managers.

## Why?

MonoMod.RuntimeDetour.HookGen isn't perfect, and HarmonyX doesn't have HookGen. And I simply had an idea, and a goal to make the perfect hooking API for myself.

I do hope though that this will be useful to other people too, and such this project has (some) documentation.

## How Do I Use It?

Right now, you don't. While it works, it's way early for use and the API will change.

## How Does the HookGen Work?

Types which Hooks are generated for need to be marked with an attribute. // TODO: Implement custom attribute

Also, all the generated hooks will be in your assembly.

Every method in a target type gets its own static class. It will look something like this:

```cs
internal static class SomeMethod
{
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public delegate void MethodParams(ref Params args);

    public ref struct Params
    {
        public global::SomeNamespace.SomeType self;
    }

    public static global::MonoMod.RuntimeDetour.ILHook Prefix(MethodParams args, global::MonoDetour.MonoDetourManager? manager = null) =>
        (manager ?? global::MonoDetour.HookGen.HookGenManager.Instance).HookGenReflectedHook(args, new(global::MonoDetour.DetourType.PrefixDetour));

    public static global::MonoMod.RuntimeDetour.ILHook Postfix(MethodParams args, global::MonoDetour.MonoDetourManager? manager = null) =>
        (manager ?? global::MonoDetour.HookGen.HookGenManager.Instance).HookGenReflectedHook(args, new(global::MonoDetour.DetourType.PostfixDetour));

    public static global::MonoMod.RuntimeDetour.ILHook ILHook(global::MonoMod.Cil.ILContext.Manipulator manipulator, global::MonoDetour.MonoDetourManager? manager = null) =>
        (manager ?? global::MonoDetour.HookGen.HookGenManager.Instance).Hook(Target(), manipulator);

    public static global::System.Reflection.MethodBase Target()
    {
        var type = typeof(global::SomeNamespace.SomeType);
        var method = type.GetMethod("SomeMethod", (global::System.Reflection.BindingFlags)~0, null, [
        ], null);
        if (method is null) ThrowHelper.ThrowMissingMethod("SomeNamespace.SomeType", "SomeMethod");
        return method;
    }
}
```

When a Hook method has a single parameter like the `Params` struct which exists in a type which also has a `public static global::System.Reflection.MethodBase Target()` method that returns the target method, `MonoDetourManager.HookGenReflectedHook` can gather all the information required for the hook and applies it.

## Credits

The HookGen source generator is *heavily* based on [MonoMod.HookGen.V2](<https://github.com/MonoMod/MonoMod/tree/hookgenv2>).
Without it, the source generator would have been a nightmare to implement myself, as I essentially have zero experience with them.
