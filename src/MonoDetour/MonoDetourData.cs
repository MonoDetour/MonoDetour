using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MonoDetour;

public class MonoDetourData
{
    public MethodBase? Target { get; set; }
    public MethodBase? Manipulator { get; set; }
    public ParameterInfo? ManipulatorParameter { get; set; }
    public Type? ManipulatorParameterType { get; set; }
    public FieldInfo[]? ManipulatorParameterTypeFields { get; set; }

    [MemberNotNullWhen(
        true,
        nameof(Target),
        nameof(Manipulator),
        nameof(ManipulatorParameter),
        nameof(ManipulatorParameterType),
        nameof(ManipulatorParameterTypeFields)
    )]
    public bool IsInitialized() =>
        Target is not null
        && Manipulator is not null
        && ManipulatorParameter is not null
        && ManipulatorParameterType is not null
        && ManipulatorParameterTypeFields is not null;
}
