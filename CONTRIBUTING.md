# Contributing Guidelines

## General Information About Build

MonoDetour targets both legacy MonoMod and reorg. MonoDetour is built against legacy, but is tested with reorg at runtime.

Legacy is not tested at runtime. This is because legacy doesn't support any supported .NET runtime and I haven't bothered to set up anything to make it work. However, legacy should work where the API is essentially the same as in reorg.

## Formatting

Use [CSharpier](<https://csharpier.com/>) for formatting C# source files and msbuild files.

For markdown, use something like [vscode-markdownlint](<https://github.com/DavidAnson/vscode-markdownlint>).

## Tests

When changing something, make sure that there is a test for it to ensure everything still works.
