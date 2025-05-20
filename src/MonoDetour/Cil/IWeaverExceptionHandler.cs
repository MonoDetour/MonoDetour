using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

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
    /// Inclusive start ILLabel for Try block.
    /// </summary>
    public ILLabel? TryStart { get; set; }

    /// <summary>
    /// Inclusive try end ILLabel for Try block.
    /// </summary>
    public ILLabel? TryEnd { get; set; }

    /// <summary>
    /// Inclusive catch start ILLabel for handler block.
    /// </summary>
    public ILLabel? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive catch end ILLabel for handler block.
    /// </summary>
    public ILLabel? HandlerEnd { get; set; }
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
    public ILLabel? TryStart { get; set; }

    /// <inheritdoc/>
    public ILLabel? TryEnd { get; set; }

    /// <summary>
    /// Inclusive catch start ILLabel for Catch block.
    /// </summary>
    public ILLabel? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive catch end ILLabel for Catch block.
    /// </summary>
    public ILLabel? HandlerEnd { get; set; }
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
    public ILLabel? FilterStart { get; set; }
}

/// <summary>
/// An exception handler with a finally block.
/// </summary>
/// <inheritdoc cref="IWeaverExceptionHandler"/>
public sealed class WeaverExceptionFinallyHandler : IWeaverExceptionHandler
{
    /// <inheritdoc/>
    public ILLabel? TryStart { get; set; }

    /// <inheritdoc/>
    public ILLabel? TryEnd { get; set; }

    /// <summary>
    /// Inclusive finally start ILLabel for Finally block.
    /// </summary>
    public ILLabel? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive finally end ILLabel for Finally block.
    /// </summary>
    public ILLabel? HandlerEnd { get; set; }
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
    public ILLabel? TryStart { get; set; }

    /// <inheritdoc/>
    public ILLabel? TryEnd { get; set; }

    /// <summary>
    /// Inclusive finally start ILLabel for Fault block.
    /// </summary>
    public ILLabel? HandlerStart { get; set; }

    /// <summary>
    /// Inclusive finally end ILLabel for Fault block.
    /// </summary>
    public ILLabel? HandlerEnd { get; set; }
}
