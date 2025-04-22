using System;
using System.Collections;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that replaces the target method's returned
/// IEnumerator class with its own IEnumerator.<br/>
/// <br/>
/// If you don't need or want full control over the enumeration, you can use
/// <see cref="IEnumeratorPrefixDetour"/> or <see cref="IEnumeratorPostfixDetour"/>.
/// <example>
/// <br/>
/// <br/>
/// This example shows how to implement an IEnumerator hook that
/// keeps the behavior of the original IEnumerator the same:
/// <code>
/// static IEnumerator MyEnumerator(IEnumerator enumerator)
/// {
///     // Prefix...
///
///     // Enumerate the original IEnumerator
///     while (enumerator.MoveNext())
///     {
///         // Return the original IEnumerator's Current
///         // value as yours to keep the behavior the same.
///         yield return enumerator.Current;
///     }
///
///     // Postfix...
/// }
/// </code>
/// </example>
/// </summary>
public class IEnumeratorDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info { get; set; } = null!;

    /// <inheritdoc/>
    public void Manipulator(ILContext il) => GeneralIEnumeratorDetour.Manipulator(il, Info);
}
