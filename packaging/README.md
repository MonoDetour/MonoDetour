# MonoDetour Packaging

This project exists to automate building and publishing MonoDetour packages to modding platforms (NuGet is not included here). All publishing logic is implemented in MSBuild, in `.proj` files.

Everything is expected to be built with Release configuration (after all [build dependencies](#build-dependencies) are installed):

```sh
dotnet build ./packaging/ -c Release -v d
```

Thunderstore packages will be found in `./artifacts/thunderstore/`

> [!NOTE]  
> We don't depend on any Thunderstore BepInExPack because there are many different BepInEx packages for different communities, and none is correct for all communities.

Actual publishing is only done if the `PublishMonoDetour` property is `true`:

```sh
dotnet build ./packaging/ -c Release -property:PublishMonoDetour=true
```

Of course, this will not work without the `TCLI_AUTH_TOKEN` environment variable being set.

## Build Dependencies

- Thunderstore CLI: `dotnet tool restore`
- Pkl CLI: [Install instructions](<https://pkl-lang.org/main/current/pkl-cli/index.html#installation>)
