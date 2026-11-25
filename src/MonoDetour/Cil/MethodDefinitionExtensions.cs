using Mono.Cecil;
using MonoMod.Cil;

namespace MonoDetour.Cil;

/// <summary>
/// Extension methods for <see cref="MethodDefinition"/>.
/// </summary>
public static class MethodDefinitionExtensions
{
    // MethodImplAttributes.Async (0x2000) is not defined by the Mono.Cecil version MonoDetour targets.
    internal const int AsyncMethodImplAttribute = 0x2000;

    /// <summary>
    /// Returns true when the method uses the .NET 11+ runtime-async calling convention.
    /// </summary>
    internal static bool IsRuntimeAsync(this MethodDefinition method) =>
        ((int)method.ImplAttributes & AsyncMethodImplAttribute) != 0;

    /// <summary>
    /// Gets the type the method body leaves on the stack. Runtime-async methods
    /// leave their unwrapped result instead of the declared Task/ValueTask.
    /// </summary>
    internal static TypeReference GetILReturnType(this MethodDefinition method)
    {
        var returnType = method.ReturnType;

        if (!method.IsRuntimeAsync())
        {
            return returnType;
        }

        if (
            returnType is GenericInstanceType generic
            && generic.ElementType.FullName
                is "System.Threading.Tasks.Task`1"
                    or "System.Threading.Tasks.ValueTask`1"
        )
        {
            return generic.GenericArguments[0];
        }

        var voidType = returnType.Module.TypeSystem.Void;
        return returnType.FullName switch
        {
            "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" => voidType,
            _ => returnType,
        };
    }

    /// <summary>
    /// Manipulate a <see cref="MethodDefinition"/> in an ILHook-like environment
    /// without being in a real ILHook. Useful when runtime detouring is not possible,
    /// such as when writing IL modifications in BepInEx preloader patchers.<br/>
    /// <br/>
    /// Using this method while already in an ILHook is pointless.
    /// </summary>
    /// <param name="method">The target method to manipulate.</param>
    /// <param name="manipulator">An IL manipulator method accepting a <see cref="ILManipulationInfo"/>.</param>
    public static void ILWeave(
        this MethodDefinition method,
        ILManipulationInfo.Manipulator manipulator
    )
    {
        using var context = new ILContext(method);

        context.Invoke(il =>
        {
            ILManipulationInfo info = new(il, original: null, out var onFinish);
            manipulator(info);
            onFinish();
        });
    }
}
