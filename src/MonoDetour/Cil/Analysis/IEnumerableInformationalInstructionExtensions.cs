using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDetour.Cil.Analysis;

internal static class IEnumerableInformationalInstructionExtensions
{
    /// <summary>
    /// To string with annotations.
    /// </summary>
    public static string ToStringWithAnnotations(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        StringBuilder sb = new();

        foreach (var instruction in informationalInstructions)
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// To string with annotations. Excludes all other instructions.
    /// </summary>
    public static string ToStringWithAnnotationsExclusive(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        StringBuilder sb = new();

        foreach (var instruction in informationalInstructions.Where(x => x.HasErrorAnnotations))
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the list of informational instructions has annotated errors.
    /// </summary>
    /// <param name="informationalInstructions">The list of informational instructions.</param>
    /// <returns>Whether or not the list of informational instructions has annotated errors.</returns>
    public static bool HasErrors(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        if (informationalInstructions.Any(x => x.HasErrorAnnotations))
        {
            return true;
        }

        return false;
    }

    internal static void ThrowIfErrorAnnotations(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        if (informationalInstructions.HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }

    internal static void ThrowIfNoErrorAnnotations(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        if (!informationalInstructions.HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }
}
