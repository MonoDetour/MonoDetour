using System;
using Mono.Cecil.Cil;
using MonoDetour.Cil.Analysis;
using MonoMod.Cil;

/// <summary>
/// Extension methods for <see cref="ILContext"/>.
/// </summary>
public static class ILContextExtensions
{
    /// <summary>
    /// Returns a string presentation of the method body's instructions with
    /// with recalculated offsets and a visualized stack size,
    /// try catch ranges,
    /// incoming branch annotations,
    /// and analyzed error annotations.
    /// </summary>
    /// <returns>A rich string presentation of the method body's instructions.</returns>
    public static string ToAnalyzedString(this ILContext context) =>
        context.Body.CreateInformationalSnapshot().AnnotateErrors().ToStringWithAnnotations();

    internal static VariableDefinition DeclareVariable(this ILContext il, Type type)
    {
        var varDef = new VariableDefinition(il.Import(type));
        il.Body.Variables.Add(varDef);
        return varDef;
    }
}
