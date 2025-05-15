// Taken from MonoMod, licensed under the MIT license.
// https://github.com/MonoMod/MonoMod/blob/bc177577/src/MonoMod.RuntimeDetour/DetourConfig.cs

using System.Collections.Generic;

namespace MonoDetour.Bindings.Reorg.RuntimeDetour;

internal interface IDetourConfig
{
    public string Id { get; }
    public int? Priority { get; }
    public IEnumerable<string> Before { get; }
    public IEnumerable<string> After { get; }
    // private int SubPriority { get; } // For now not public since this is not supported in legacy.
}
