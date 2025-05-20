using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour;

internal static class MethodHookRecords
{
    static readonly ConditionalWeakTable<ILContext, HookedMethodInfo> s_MethodToInfo = new();
    static readonly object s_Lock = new();

    /// <summary>
    /// Gets a <see cref="HookedMethodInfo"/> for a target <see cref="ILContext"/>.
    /// </summary>
    /// <param name="il">The target <see cref="ILContext"/>.</param>
    /// <param name="methodBase">The target method.</param>
    /// <returns>A <see cref="HookedMethodInfo"/> for the <see cref="ILContext"/>.</returns>
    internal static HookedMethodInfo GetFor(ILContext il, MethodBase methodBase)
    {
        lock (s_Lock)
        {
            if (s_MethodToInfo.TryGetValue(il, out HookedMethodInfo? info))
            {
                return info;
            }

            if (methodBase is not MethodInfo method)
            {
                throw new InvalidCastException("MethodBase is not MethodInfo!");
            }

            VariableDefinition? returnValue = null;
            if (method.ReturnType != typeof(void))
            {
                returnValue = new VariableDefinition(il.Method.ReturnType);
                il.Body.Variables.Add(returnValue);
            }

            var controlFlow = il.DeclareVariable(typeof(int));
            var tempControlFlow = il.DeclareVariable(typeof(int));
            info = new(new(controlFlow, tempControlFlow), new([]), returnValue);
            s_MethodToInfo.Add(il, info);
            return info;
        }
    }
}

internal record HookedMethodInfo(
    PrefixControlFlowInfo PrefixInfo,
    PostfixControlFlowInfo PostfixInfo,
    VariableDefinition? ReturnValue
);

internal record PrefixControlFlowInfo(
    VariableDefinition ControlFlow,
    VariableDefinition TemporaryControlFlow
)
{
    public bool ControlImplemented { get; set; }
}

internal record PostfixControlFlowInfo(List<Instruction> FirstPostfixInstructions);


// local: ControlFlow -> None

// Skip original -> ControlFlow = SkipOriginal
// None -> (none)
// Implementation: ControlFlow switch
//      SkipOriginal => goto
