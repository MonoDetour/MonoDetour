using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that replaces the target method's returned
/// IEnumerator class with its own IEnumerator.
/// </summary>
internal class GeneralIEnumeratorDetour
{
    static readonly MethodInfo genericEnumeratorDriver = typeof(GeneralIEnumeratorDetour).GetMethod(
        nameof(GenericEnumeratorDriver),
        (BindingFlags)~0
    )!;

    public static void Manipulator(ILContext il, MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new InvalidProgramException();

        ILCursor c = new(il);
        c.Index -= 1;

        if (info.DetourType == typeof(IEnumeratorDetour))
        {
            c.Emit(OpCodes.Call, info.Data.Manipulator!);
        }
        else
        {
            c.EmitReference(info);
            if (info.Data.Target is MethodInfo methodInfo && methodInfo.ReturnType.IsGenericType)
            {
                var genericType = methodInfo.ReturnType.GenericTypeArguments[0];
                var method = genericEnumeratorDriver.MakeGenericMethod(genericType);
                c.Emit(OpCodes.Call, method);
            }
            else
            {
                c.Emit(
                    OpCodes.Call,
                    new Func<IEnumerator, MonoDetourInfo, IEnumerator>(EnumeratorDriver).Method
                );
            }
        }

        if (info.Data.Owner.LogLevel == MonoDetourManager.Logging.Diagnostic)
        {
            c.Method.RecalculateILOffsets();
            Console.WriteLine($"Manipulated by {info.Data.Manipulator.Name}: " + il);
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

    private static IEnumerator<T> GenericEnumeratorDriver<T>(
        IEnumerator<T> enumerator,
        MonoDetourInfo info
    )
    {
        if (info.DetourType == typeof(IEnumeratorPrefixDetour))
            info.Data.Manipulator!.Invoke(null, [enumerator]);

        while (enumerator.MoveNext())
            yield return enumerator.Current;

        if (info.DetourType == typeof(IEnumeratorPostfixDetour))
            info.Data.Manipulator!.Invoke(null, [enumerator]);
    }
}
