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
    static readonly ConditionalWeakTable<MethodDefinition, HookedMethodInfo> s_MethodToInfo = new();

    /// <summary>
    /// Gets a <see cref="HookedMethodInfo"/> for a target <see cref="ILContext"/>.
    /// </summary>
    /// <param name="il">The target <see cref="ILContext"/>.</param>
    /// <param name="methodBase">The target method.</param>
    /// <returns>A <see cref="HookedMethodInfo"/> for the <see cref="ILContext"/>.</returns>
    internal static HookedMethodInfo GetFor(ILContext il, MethodBase methodBase)
    {
        if (s_MethodToInfo.TryGetValue(il.Method, out HookedMethodInfo? info))
        {
            // Console.WriteLine("Got existing info for method: " + methodBase.Name);
            // foreach (var postfix in info.PostfixInfo.FirstPostfixInstructions)
            //     Console.WriteLine(postfix);

            return info;
        }

        if (methodBase is not MethodInfo method)
        {
            throw new InvalidCastException("MethodBase is not MethodInfo!");
        }

        // Console.WriteLine("Creating new info for method: " + method.Name);

        VariableDefinition? returnValue = null;
        if (method.ReturnType != typeof(void))
        {
            returnValue = new VariableDefinition(il.Method.ReturnType);
            il.Body.Variables.Add(returnValue);
        }

        var controlFlow = il.DeclareVariable(typeof(int));
        var tempControlFlow = il.DeclareVariable(typeof(int));
        info = new(new(controlFlow, tempControlFlow), new(), returnValue);
        s_MethodToInfo.Add(il.Method, info);
        return info;
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

internal record PostfixControlFlowInfo()
{
    public List<Instruction> FirstPostfixInstructions { get; } = [];
}


// local: ControlFlow -> None

// Skip original -> ControlFlow = SkipOriginal
// None -> (none)
// Implementation: ControlFlow switch
//      SkipOriginal => goto
