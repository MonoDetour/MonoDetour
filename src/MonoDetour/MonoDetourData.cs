using System;
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
    /// The singular parameter of the hook or manipulator method.
    /// </summary>
    public ParameterInfo? ManipulatorParameter { get; set; }

    /// <summary>
    /// The type of the <see cref="ManipulatorParameter"/>.
    /// </summary>
    public Type? ManipulatorParameterType { get; set; }

    /// <summary>
    /// The fields of the <see cref="ManipulatorParameterType"/>.
    /// </summary>
    public FieldInfo[]? ManipulatorParameterTypeFields { get; set; }

    /// <summary>
    /// Checks if all the values are initialized.
    /// </summary>
    /// <returns>Whether or not all the values are initialized.</returns>
    [MemberNotNullWhen(
        true,
        nameof(Owner),
        nameof(Target),
        nameof(Manipulator),
        nameof(ManipulatorParameter),
        nameof(ManipulatorParameterType),
        nameof(ManipulatorParameterTypeFields)
    )]
    public bool IsInitialized() =>
        Owner is not null
        && Target is not null
        && Manipulator is not null
        && ManipulatorParameter is not null
        && ManipulatorParameterType is not null
        && ManipulatorParameterTypeFields is not null;
}
