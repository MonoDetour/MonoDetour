using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Logging;
using MonoMod.Cil;
using InstrList = Mono.Collections.Generic.Collection<Mono.Cecil.Cil.Instruction>;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace MonoDetour.Cil;

/// <summary>
/// An API for manipulating CIL method bodies.
/// See https://monodetour.github.io/ilhooking/introduction/
/// </summary>
public partial class ILWeaver : IMonoDetourLogSource
{
    private enum InsertType
    {
        BeforeAndStealLabels,
        Before,
        After,
    }

    private enum MatchPassType
    {
        StrictNoOriginalPass,
        RelaxedAllowOriginalPass,
        IsOriginalPass,
    }

    private const string obsoleteMessageRemoveAt =
        "Removing a range by count is prone to causing invalid IL if the "
        + "target method's instructions have changed. "
        + $"Use {nameof(ILWeaver)}.{nameof(InsertBranchOver)} instead "
        + "as it doesn't have this design flaw. Note that these two methods behave "
        + "slightly differently; please read the method's remarks in detail.";

    /// <inheritdoc cref="ILManipulationInfo"/>
    public ILManipulationInfo ManipulationInfo { get; }

    /// <inheritdoc cref="ILContext"/>
    public ILContext Context { get; }

    /// <inheritdoc cref="ILContext.IL"/>
    public ILProcessor IL => Context.IL;

    /// <inheritdoc cref="ILContext.Body"/>
    public MethodBody Body => Context.Body;

    /// <inheritdoc cref="ILContext.Method"/>
    public MethodDefinition Method => Context.Method;

    /// <inheritdoc cref="ILContext.Instrs"/>
    public InstrList Instructions => Context.Instrs;

    /// <summary>
    /// The instruction this weaver currently points to.
    /// </summary>
    /// <remarks>
    /// Setter is <see cref="CurrentTo(Instruction)"/>.<br/>
    /// For replacing the current instruction,
    /// see <see cref="ReplaceCurrent(Instruction)"/>
    /// </remarks>
    public Instruction Current
    {
        get => current;
        set => CurrentTo(value);
    }

    /// <summary>
    /// The instruction before what this weaver currently points to.
    /// </summary>
    public Instruction Previous => Current.Previous;

    /// <summary>
    /// The instruction after what this weaver currently points to.
    /// </summary>
    /// <remarks>
    /// This is not equivalent to <see cref="ILCursor.Next"/>.
    /// The equivalent would be <see cref="Current"/>.
    /// </remarks>
    public Instruction Next => Current.Next;

    /// <summary>
    /// Gets the first instruction in the method body.
    /// </summary>
    public Instruction First => Instructions[0];

    /// <summary>
    /// Gets the last instruction in the method body.
    /// </summary>
    public Instruction Last => Instructions[^1];

    /// <summary>
    /// The index of the instruction on <see cref="Current"/>
    /// </summary>
    /// <remarks>
    /// A negative index will loop back.
    /// Setter uses <see cref="CurrentTo(int)"/> which can throw.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public int Index
    {
        get => Instructions.IndexOf(Current);
        [Obsolete(
            "Offsetting the index is error-prone; see https://github.com/MonoDetour/MonoDetour/issues/10\n"
                + "If you know what you are doing, use ILWeaver.CurrentTo(int) directly.",
            true
        )]
        set => CurrentTo(value);
    }

    /// <inheritdoc/>
    public MonoDetourLogger.LogChannel LogFilter { get; set; } =
        MonoDetourLogger.LogChannel.Warning | MonoDetourLogger.LogChannel.Error;

    Instruction current;

    readonly List<ILLabel> pendingFutureNextInsertLabels = [];
    readonly List<IWeaverExceptionHandler> pendingHandlers = [];

    const string gotoMatchingDocsLink = "<documentation link will be here once it exists>";

    /// <summary>
    /// Constructs a new <see cref="ILWeaver"/> instance
    /// for manipulating the target CIL method body.
    /// See https://monodetour.github.io/ilhooking/introduction/
    /// </summary>
    public ILWeaver(ILManipulationInfo il)
    {
        ManipulationInfo = Helpers.ThrowIfNull(il);
        Context = il.Context;
        current = Context.Instrs[0];

        il.OnManipulatorFinished += () =>
        {
            foreach (var handler in pendingHandlers.ToArray())
            {
                HandlerApply(handler);
            }
        };
    }

    /// <summary>
    /// Create a new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>
    /// with state copied optionally.
    /// </summary>
    /// <returns>A new <see cref="ILWeaver"/> or a copy with state.</returns>
    public ILWeaver(ILWeaver weaver, bool copyState = true)
        : this(weaver.ManipulationInfo)
    {
        if (copyState == false)
            return;

        Current = weaver.Current;
    }

    /// <summary>
    /// Create a new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>
    /// using the <see cref="ILWeaver(ILWeaver, bool)"/> constructor.<br/>
    /// Does not copy state.
    /// </summary>
    /// <returns>A new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>.</returns>
    public ILWeaver New() => new(this, copyState: false);

    /// <summary>
    /// Create a clone of the <see cref="ILWeaver"/>
    /// using the <see cref="ILWeaver(ILWeaver, bool)"/> constructor.<br/>
    /// State is copied.
    /// </summary>
    /// <returns>A clone of the <see cref="ILWeaver"/>.</returns>
    public ILWeaver Clone() => new(this, copyState: true);

    /// <summary>
    /// Declare a new local variable on the target method.<br/>
    /// <br/>
    /// A local variable can be assigned with a <c>stloc</c> instruction
    /// and loaded using an <c>ldloc</c> instruction.
    /// To assign or load this local variable, the returned
    /// <see cref="VariableDefinition"/> or its index should be used
    /// as the Operand.
    /// </summary>
    /// <param name="type">The type of the local variable.</param>
    /// <returns>A new local variable.</returns>
    public VariableDefinition DeclareVariable(Type type)
    {
        DeclareVariable(type, out var variable);
        return variable;
    }

    /// <param name="variableDefinition">A new local variable.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    /// <inheritdoc cref="DeclareVariable(Type)"/>
    /// <param name="type"></param>
    ///
    public ILWeaver DeclareVariable(Type type, out VariableDefinition variableDefinition)
    {
        variableDefinition = new VariableDefinition(Context.Import(type));
        Body.Variables.Add(variableDefinition);
        return this;
    }
}

/// <summary>
/// A result value sometimes returned by <see cref="ILWeaver"/> containing status on
/// action through <see cref="IsValid"/>.
/// If <see cref="IsValid"/> is false, <see cref="FailureMessage"/> has a value.
/// </summary>
public class ILWeaverResult
{
    /// <summary>
    /// Constructs a new <see cref="ILWeaverResult"/> for the select <see cref="ILWeaver"/>.
    /// If <paramref name="failureMessage"/> is left null, the result is success; otherwise failure.
    /// </summary>
    /// <param name="weaver">The <see cref="ILWeaver"/> who this result is connected to.</param>
    /// <param name="failureMessage">A delegate returning the failure message if the result
    /// is a failure, or null if it was a success.</param>
    public ILWeaverResult(ILWeaver weaver, Func<string>? failureMessage)
    {
        this.weaver = weaver;

        if (failureMessage is not null)
        {
            IsValid = false;
            getFailureMessage = failureMessage;
        }
    }

    /// <summary>
    /// Returns the state of the result.
    /// </summary>
    [MemberNotNullWhen(false, nameof(FailureMessage))]
    public bool IsValid { get; } = true;

    /// <summary>
    /// A message containing the details of what went wrong
    /// if <see cref="IsValid"/> is <see langword="false"/>.
    /// </summary>
    public string? FailureMessage => invalidActionMessage ??= getFailureMessage?.Invoke();

    string? invalidActionMessage;
    readonly Func<string>? getFailureMessage;
    readonly ILWeaver weaver;

    /// <summary>
    /// Throws if the previous action was not successful.<br/>
    /// <br/>
    /// For checking if the action was valid without throwing, see
    /// <see cref="IsValid"/> or <see cref="Extract"/>.
    /// </summary>
    /// <returns>The <see cref="ILWeaver"/>.</returns>
    /// <exception cref="ILWeaverResultException"></exception>
    public ILWeaver ThrowIfFailure()
    {
        if (IsValid)
            return weaver;

        throw new ILWeaverResultException($"Failed result was thrown.\n" + FailureMessage);
    }

    /// <summary>
    /// Outputs this <see cref="ILWeaverResult"/>. This method exists to allow
    /// more fluent chaining of the <see cref="ILWeaver"/> methods.
    /// </summary>
    /// <returns>The <see cref="ILWeaver"/>.</returns>
    public ILWeaver Extract(out ILWeaverResult result)
    {
        result = this;
        return weaver;
    }
}

/// <summary>
/// An exception thrown by <see cref="ILWeaverResult.ThrowIfFailure"/>.
/// </summary>
[Serializable]
public class ILWeaverResultException : Exception
{
    /// <inheritdoc/>
    public ILWeaverResultException() { }

    /// <inheritdoc/>
    public ILWeaverResultException(string message)
        : base(message) { }

    /// <inheritdoc/>
    public ILWeaverResultException(string message, Exception inner)
        : base(message, inner) { }
}
