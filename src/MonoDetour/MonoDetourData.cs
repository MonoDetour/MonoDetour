using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MonoDetour;

/// <summary>
/// Data which is mostly gained through reflection and initialized by MonoDetour
/// for use when applying a MonoDetour Hook.
/// </summary>
public class MonoDetourData
{
    /// <summary>
    /// The owner <see cref="MonoDetourManager"/> of this <see cref="MonoDetourData"/>.
    /// All applied MonoDetour hooks must have an owner.
    /// </summary>
    public MonoDetourManager? Owner { get; set; }

    /// <summary>
    /// The method to hook.
    /// </summary>
    public MethodBase? Target { get; set; }

    /// <summary>
    /// The hook or manipulator method.
    /// </summary>
    public MethodBase? Manipulator { get; set; }

    /// <summary>
    /// Checks if all the values are initialized.
    /// </summary>
    /// <returns>Whether or not all the values are initialized.</returns>
    [MemberNotNullWhen(true, nameof(Owner), nameof(Target), nameof(Manipulator))]
    public bool IsInitialized() =>
        Owner is not null && Target is not null && Manipulator is not null;
}
