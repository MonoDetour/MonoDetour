using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDetour.Cil.Analysis;

internal static class IEnumerableInformationalInstructionExtensions
{
    /// <summary>
    /// To string with annotation types deduplicated.
    /// </summary>
    public static string ToStringWithAnnotationTypesDeduplicated(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        StringBuilder sb = new();
        HashSet<Type> types = [];

        foreach (var instruction in informationalInstructions)
        {
            sb.AppendLine(instruction.ToStringInternal(withAnnotations: true, types));
        }

        return sb.ToString();
    }

    /// <summary>
    /// To string with annotation types deduplicated. Excludes all other instructions.
    /// </summary>
    public static string ToStringWithAnnotationTypesDeduplicatedExclusive(
        this IEnumerable<InformationalInstruction> informationalInstructions
    )
    {
        StringBuilder sb = new();
        HashSet<Type> types = [];

        foreach (var instruction in informationalInstructions.Where(x => x.HasAnnotations))
        {
            var annotationTypes = instruction.Annotations.Select(x => x.GetType());
            if (annotationTypes.All(types.Contains))
            {
                continue;
            }

            sb.AppendLine(instruction.ToStringInternal(withAnnotations: true, types));

            foreach (var annotationType in annotationTypes)
            {
                types.Add(annotationType);
            }
        }

        return sb.ToString();
    }
}
