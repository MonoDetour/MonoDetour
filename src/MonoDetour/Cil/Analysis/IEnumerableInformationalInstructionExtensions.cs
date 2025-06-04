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

        foreach (var instruction in informationalInstructions.Where(x => x.HasAnnotations))
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }
}
