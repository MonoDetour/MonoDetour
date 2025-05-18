using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MonoDetour.Cil;

/// <summary>
/// An exception handler for <see cref="ILWeaver"/>.
/// </summary>
/// <remarks>
/// This will get translated into a Mono.Cecil <see cref="ExceptionHandler"/>
/// block when applied.
/// </remarks>
public interface IWeaverExceptionHandler
{
    /// <summary>
    /// Inclusive start instruction for Try block.
    /// </summary>
    public Instruction? TryStart { get; set; }

    /// <summary>
    /// Inclusive try end instruction for Try block.
    /// </summary>
    public Instruction? TryEnd { get; set; }

    /// <summary>
    /// Inclusive catch start instruction for handler block.
    /// </summary>
    public Instruction? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive catch end instruction for handler block.
    /// </summary>
    public Instruction? HandlerEnd { get; set; }
}

/// <summary>
/// An exception handler with a catch block.
/// </summary>
/// <param name="catchType">The type of exceptions to catch.</param>
/// <inheritdoc cref="IWeaverExceptionHandler"/>
public class WeaverExceptionCatchHandler(TypeReference catchType) : IWeaverExceptionHandler
{
    /// <summary>
    /// The type of exception to catch.
    /// </summary>
    public TypeReference CatchType { get; } = catchType;

    /// <inheritdoc/>
    public Instruction? TryStart { get; set; }

    /// <inheritdoc/>
    public Instruction? TryEnd { get; set; }

    /// <summary>
    /// Inclusive catch start instruction for Catch block.
    /// </summary>
    public Instruction? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive catch end instruction for Catch block.
    /// </summary>
    public Instruction? HandlerEnd { get; set; }
}

/// <summary>
/// An exception handler with a catch block with a filter.
/// </summary>
/// <inheritdoc/>
public sealed class WeaverExceptionFilterHandler(TypeReference catchType)
    : WeaverExceptionCatchHandler(catchType)
{
    /// <summary>
    /// Inclusive filter start instruction for a catch block with a filter.
    /// </summary>
    public Instruction? FilterStart { get; set; }
}

/// <summary>
/// An exception handler with a finally block.
/// </summary>
/// <inheritdoc cref="IWeaverExceptionHandler"/>
public sealed class WeaverExceptionFinallyHandler : IWeaverExceptionHandler
{
    /// <inheritdoc/>
    public Instruction? TryStart { get; set; }

    /// <inheritdoc/>
    public Instruction? TryEnd { get; set; }

    /// <summary>
    /// Inclusive finally start instruction for Finally block.
    /// </summary>
    public Instruction? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive finally end instruction for Finally block.
    /// </summary>
    public Instruction? HandlerEnd { get; set; }
}

/// <summary>
/// An exception handler with a fault block.<br/>
/// A fault block is similar to a finally block, except it runs only
/// if the code in the try block threw, whereas finally runs always.
/// This differentiates from a catch block in that only one catch block
/// runs if there are multiple.
/// </summary>
/// <inheritdoc cref="IWeaverExceptionHandler"/>
public sealed class WeaverExceptionFaultHandler : IWeaverExceptionHandler
{
    /// <inheritdoc/>
    public Instruction? TryStart { get; set; }

    /// <inheritdoc/>
    public Instruction? TryEnd { get; set; }

    /// <summary>
    /// Inclusive finally start instruction for Fault block.
    /// </summary>
    public Instruction? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive finally end instruction for Fault block.
    /// </summary>
    public Instruction? HandlerEnd { get; set; }
}
