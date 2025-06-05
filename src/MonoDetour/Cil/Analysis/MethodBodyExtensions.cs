using Mono.Cecil.Cil;

namespace MonoDetour.Cil.Analysis;

internal static class MethodBodyExtensions
{
    public static InformationalMethodBody CreateInformationalSnapshot(this MethodBody body) =>
        InformationalMethodBody.CreateInformationalSnapshot(body);
}
