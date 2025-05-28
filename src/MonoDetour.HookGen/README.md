# MonoDetour.HookGen

| MonoDetour | MonoDetour.HookGen |
|--|--|
| [![MonoDetour](https://img.shields.io/nuget/v/MonoDetour?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour) | [![MonoDetour.HookGen](https://img.shields.io/nuget/v/MonoDetour.HookGen?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/MonoDetour.HookGen) |

## Add to Your Project

Change the version number to optimally the newest:

```xml
<ItemGroup>
  <PackageReference Include="MonoDetour.HookGen" Version="0.1.0-*" PrivateAssets="all" />
  <PackageReference Include="MonoDetour" Version="0.1.0-*" />
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

## Documentation

See <https://monodetour.github.io/getting-started/hookgen/>
