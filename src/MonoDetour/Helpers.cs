using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MonoDetour;

/// <summary>
/// Helper methods from MonoMod.Utils, but these are not public in Legacy.
/// </summary>
static class Helpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(
        [NotNull] T? argument,
        [CallerArgumentExpression(nameof(argument))] string name = ""
    )
    {
        if (argument is null)
            ThrowArgumentNull(name);
        return argument;
    }

    [DoesNotReturn]
    private static void ThrowArgumentNull(string argName)
    {
        throw new ArgumentNullException(argName);
    }
}
