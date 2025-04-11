using System;
using System.Collections;

namespace MonoDetour.UnitTests.TestLib;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
public class LibraryMethods
{
    int Number { get; set; }

    public int TakeAndReturnInt(int number)
    {
        return number;
    }

    public IEnumerator EnumerateRange(int iterations)
    {
        for (int i = 1; i <= iterations; i++)
        {
            Number = i;
            yield return i;
        }
    }
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression
