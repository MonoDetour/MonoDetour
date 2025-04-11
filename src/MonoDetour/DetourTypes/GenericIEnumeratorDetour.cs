using System;
using System.Collections;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that replaces the target method's returned
/// IEnumerator class with its own IEnumerator.
/// </summary>
internal class GenericIEnumeratorDetour
{
    public static void Manipulator(ILContext il, MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new InvalidProgramException();

        ILCursor c = new(il);
        c.Index -= 1;

        if (info.DetourType == typeof(IEnumeratorDetour))
        {
            c.Emit(OpCodes.Call, info.Data.Manipulator);
        }
        else
        {
            c.EmitReference(info);
            c.Emit(
                OpCodes.Call,
                new Func<IEnumerator, MonoDetourInfo, IEnumerator>(EnumeratorDriver)
            );
        }
    }

    private static IEnumerator EnumeratorDriver(IEnumerator enumerator, MonoDetourInfo info)
    {
        if (info.DetourType == typeof(IEnumeratorPrefixDetour))
            info.Data.Manipulator!.Invoke(null, [enumerator]);

        while (enumerator.MoveNext())
            yield return enumerator.Current;

        if (info.DetourType == typeof(IEnumeratorPostfixDetour))
            info.Data.Manipulator!.Invoke(null, [enumerator]);
    }
}
