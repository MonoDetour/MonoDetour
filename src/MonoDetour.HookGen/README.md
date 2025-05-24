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

## Documentation

See <https://monodetour.github.io/getting-started/hookgen/>
