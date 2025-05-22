using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour;

/// <summary>
/// An API for hooks to share information for target methods.
/// </summary>
public static class HookTargetRecords
{
    static readonly ConditionalWeakTable<MethodDefinition, HookTargetInfo> s_MethodToInfo = new();

    /// <summary>
    /// Gets a <see cref="HookTargetInfo"/> for a target <see cref="ILContext"/>.
    /// </summary>
    /// <param name="il">The target <see cref="ILContext"/>.</param>
    /// <param name="methodBase">The target method.</param>
    /// <returns>A <see cref="HookTargetInfo"/> for the <see cref="ILContext"/>.</returns>
    public static HookTargetInfo GetFor(ILContext il, MethodBase methodBase)
    {
        if (s_MethodToInfo.TryGetValue(il.Method, out HookTargetInfo? info))
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

        info = new(il, returnValue);
        s_MethodToInfo.Add(il.Method, info);
        return info;
    }

    /// <summary>
    /// Information about a hook target method.
    /// </summary>
    public record HookTargetInfo(ILContext Context, VariableDefinition? ReturnValue)
    {
        /// <inheritdoc cref="TargetPrefixInfo"/>
        public TargetPrefixInfo PrefixInfo { get; } = new(Context);

        /// <inheritdoc cref="TargetPostfixInfo"/>
        public TargetPostfixInfo PostfixInfo { get; } = new();
    }

    /// <summary>
    /// Information relevant to Prefix hooks.
    /// </summary>
    /// <param name="Context">The <see cref="ILContext"/> of the method.</param>
    public record TargetPrefixInfo(ILContext Context)
    {
        /// <summary>
        /// The local int used for determining the control flow of the method.<br/>
        /// See <see cref="DetourTypes.ReturnFlow"/>.
        /// </summary>
        public VariableDefinition ControlFlow { get; } = Context.DeclareVariable(typeof(int));

        /// <summary>
        /// The temporary local int used for setting the
        /// <see cref="ControlFlow"/> int.
        /// </summary>
        public VariableDefinition TemporaryControlFlow { get; } =
            Context.DeclareVariable(typeof(int));

        /// <summary>
        /// Whether or not Prefix control flow has been implemented in the method.
        /// </summary>
        public bool ControlFlowImplemented { get; private set; }

        /// <summary>
        /// Sets <see cref="ControlFlowImplemented"/> to true.
        /// </summary>
        public void SetControlFlowImplemented() => ControlFlowImplemented = true;
    }

    /// <summary>
    /// Information relevant to Postfix hooks.
    /// </summary>
    public record TargetPostfixInfo()
    {
        /// <summary>
        /// The first instruction for each postfix.
        /// </summary>
        public List<Instruction> FirstPostfixInstructions { get; } = [];
    }
}
