using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MonoDetour;

/// <summary>
/// Helper methods from MonoMod.Utils, but these are not public in Legacy.
/// </summary>
static class MMHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(
        [NotNull] T? arg,
        [CallerArgumentExpression("arg")] string name = ""
    )
    {
        if (arg is null)
            ThrowArgumentNull(name);
        return arg;
    }

    [DoesNotReturn]
    private static void ThrowArgumentNull(string argName)
    {
        throw new ArgumentNullException(argName);
    }
}
