using Mono.Cecil.Cil;

namespace MonoDetour.Cil.Analysis;

/// <summary>
/// Extension methods for <see cref="MethodBody"/>.
/// </summary>
public static class MethodBodyExtensions
{
    /// <summary>
    /// Creates an <see cref="IInformationalMethodBody"/> snapshot of the
    /// provided <see cref="MethodBody"/>. This will evaluate instructions as
    /// accurately as possible to the JIT compiler, which means that unreachable
    /// instructions aren't evaluated.
    /// </summary>
    /// <param name="body">The <see cref="MethodBody"/> to create an informational snapshot for.</param>
    /// <returns>An <see cref="IInformationalMethodBody"/>.</returns>
    public static IInformationalMethodBody CreateInformationalSnapshotJIT(this MethodBody body) =>
        InformationalMethodBody.CreateInformationalSnapshotJIT(body);

    /// <summary>
    /// Creates an <see cref="IInformationalMethodBody"/> snapshot of the
    /// provided <see cref="MethodBody"/>. This evaluates unreachable instructions
    /// which means this is not accurate for proper CIL analysis, but this can
    /// be useful when we want to estimate stack sizes of unreachable instructions.
    /// </summary>
    /// <param name="body">The <see cref="MethodBody"/> to create an informational snapshot for.</param>
    /// <returns>An <see cref="IInformationalMethodBody"/>.</returns>
    public static IInformationalMethodBody CreateInformationalSnapshotEvaluateAll(
        this MethodBody body
    ) => InformationalMethodBody.CreateInformationalSnapshotEvaluateAll(body);
}
