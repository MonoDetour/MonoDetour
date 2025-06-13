using Mono.Cecil.Cil;

namespace MonoDetour.Cil.Analysis;

/// <summary>
/// Extension methods for <see cref="MethodBody"/>.
/// </summary>
public static class MethodBodyExtensions
{
    /// <summary>
    /// Creates an <see cref="IInformationalMethodBody"/> snapshot of the
    /// provided <see cref="MethodBody"/>.
    /// </summary>
    /// <param name="body">The <see cref="MethodBody"/> to create an informational snapshot for.</param>
    /// <returns>An <see cref="IInformationalMethodBody"/>.</returns>
    public static IInformationalMethodBody CreateInformationalSnapshot(this MethodBody body) =>
        InformationalMethodBody.CreateInformationalSnapshot(body);
}
