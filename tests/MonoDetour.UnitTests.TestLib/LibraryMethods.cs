using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

    public int TakeAndReturnInt2(int number)
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

    public IEnumerator<int> EnumerateIntRange(int iterations)
    {
        for (int i = 1; i <= iterations; i++)
        {
            Number = i;
            yield return i;
        }
    }

    public bool TryGetThis(bool getThis, [NotNullWhen(true)] out LibraryMethods? result)
    {
        result = getThis ? this : null;

        return getThis;
    }

    public void SetNullStringToHello(ref string? value)
    {
        value ??= "hello";
    }

    public string ReturnNullStringAsHello(string? value)
    {
        return value ?? "hello";
    }

    public void Overloaded() { }

    public void Overloaded(int num) { }

    public void HasArgumentWithGenericType(IEnumerable<int> numbers) { }

    public void ReturnsTuple(out (bool, bool) tuple) => tuple = (true, true);

    public void TakesRefDictionary(ref Dictionary<bool, bool> dictionary) { }

    public void CSharpKeywordParameterName(object @object) { }

    public bool CausesConflictingParameterNames(bool self, bool returnValue) => true;
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression
