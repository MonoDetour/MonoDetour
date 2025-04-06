using System;

namespace MonoDetour;

public class MonoDetourInfo
{
    public Type DetourType { get; set; }
    public MonoDetourData Data { get; } = new();

    public MonoDetourInfo(DetourType detourType)
        : this(GetTypeFromDetourType(detourType)) { }

    public MonoDetourInfo(Type detourType)
    {
        MonoDetourUtils.ThrowIfInvalidDetourType(detourType);
        DetourType = detourType;
    }

    internal static Type GetTypeFromDetourType(DetourType detourType) =>
        detourType switch
        {
            MonoDetour.DetourType.Prefix => typeof(PrefixDetour),
            MonoDetour.DetourType.Postfix => typeof(PostfixDetour),
            MonoDetour.DetourType.ILHook => typeof(ILHookDetour),
            _ => throw new ArgumentOutOfRangeException(),
        };
}

public enum DetourType
{
    Prefix = 1,
    Postfix = 2,
    ILHook = 3,
}
