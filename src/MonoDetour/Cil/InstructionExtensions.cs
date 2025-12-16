using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Cil;

/// <summary>
/// Extension methods for <see cref="Instruction"/>.
/// </summary>
public static class InstructionExtensions
{
    /// <summary>
    /// Stores an instruction instance into an 'out' parameter variable while
    /// returning the same instruction instance for convenient use in
    /// methods that take instruction instances, e.g.
    /// <code>
    /// <![CDATA[
    /// weaver.InsertBeforeCurrent(
    ///     w.Create(OpCodes.Ldc_I4_0).Get(out var ldcI40),
    ///     w.Create(OpCodes.Ret).Get(out var ret)
    /// );
    /// ]]>
    /// </code>
    /// </summary>
    /// <param name="source">The instruction instance to get.</param>
    /// <param name="instruction">The got instruction instance.</param>
    /// <returns>The same instruction instance.</returns>
    public static Instruction Get(this Instruction source, out Instruction instruction)
    {
        instruction = source;
        return source;
    }

    /// <summary>
    /// Marks an instruction as a target of an <see cref="ILLabel"/> while returning
    /// the same instruction instance for convenient use in
    /// methods that take instruction instances, e.g.
    /// <code>
    /// <![CDATA[
    /// ILLabel label = weaver.DeclareLabel();
    /// // ...
    /// weaver.InsertBeforeCurrent(
    ///     w.Create(OpCodes.Ret).MarkLabel(label)
    /// );
    /// ]]>
    /// </code>
    /// </summary>
    /// <param name="instruction">The instruction to mark a label with.</param>
    /// <param name="label">The label to be marked.</param>
    /// <returns>The same instruction instance.</returns>
    public static Instruction MarkLabel(this Instruction instruction, ILLabel label)
    {
        label.InteropSetTarget(instruction);
        return instruction;
    }

    /// <summary>
    /// Returns a string representation of an <see cref="Instruction"/> without fear of
    /// <see cref="InvalidCastException"/>.
    /// </summary>
    /// <param name="instruction">Instruction to turn into a string representation.</param>
    /// <returns>String representation of an <see cref="Instruction"/>.</returns>
    public static string ToStringSafe(this Instruction instruction)
    {
        var sb = new StringBuilder();

        AppendLabel(sb, instruction);
        sb.Append(':');
        sb.Append(' ');
        sb.Append(instruction.OpCode.Name);

        if (instruction.Operand == null)
            return sb.ToString();

        sb.Append(' ');

        switch (instruction.OpCode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                Instruction? target;
                switch (instruction.Operand)
                {
                    case ILLabel label:
                        target = label.InteropGetTarget();
                        if (target is null)
                        {
                            return sb.ToString();
                        }
                        break;
                    case Instruction ins:
                        target = ins;
                        break;
                    case object value:
                        AppendInvalidOperandType(sb, value);
                        return sb.ToString();
                    default:
                        return sb.ToString();
                }

                AppendLabel(sb, target);
                break;
            case OperandType.InlineSwitch:

                Instruction?[] labels;

                switch (instruction.Operand)
                {
                    case ILLabel[] ilLabels:
                        labels = ilLabels.Select(l => l.InteropGetTarget()).ToArray();
                        break;
                    case Instruction[] instructions:
                        labels = instructions;
                        break;
                    case object value:
                        AppendInvalidOperandType(sb, value);
                        return sb.ToString();
                    default:
                        return sb.ToString();
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    var label = labels[i];
                    if (label is null)
                        sb.Append("null");
                    else
                        AppendLabel(sb, label);
                }
                break;
            case OperandType.InlineString:
                sb.Append('\"');
                sb.Append(instruction.Operand);
                sb.Append('\"');
                break;
            default:
                sb.Append(instruction.Operand);
                break;
        }

        return sb.ToString();
    }

    private static void AppendInvalidOperandType(StringBuilder sb, object value)
    {
        sb.Append("<invalid operand type: '")
            .Append(value.GetType())
            .Append("', value: '")
            .Append(value.ToString())
            .Append("'>");
    }

    static void AppendLabel(StringBuilder builder, Instruction instruction)
    {
        builder.Append("IL_");
        builder.Append(instruction.Offset.ToString("x4", CultureInfo.InvariantCulture));
    }
}
