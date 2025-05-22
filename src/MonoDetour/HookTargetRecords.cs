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
    public class HookTargetInfo
    {
        internal HookTargetInfo(ILContext Context, VariableDefinition? returnValue)
        {
            PrefixInfo = new(Context);
            ReturnValue = returnValue;
        }

        /// <inheritdoc cref="TargetPrefixInfo"/>
        public TargetPrefixInfo PrefixInfo { get; }

        /// <inheritdoc cref="TargetPostfixInfo"/>
        public TargetPostfixInfo PostfixInfo { get; } = new();

        /// <summary>
        /// The local variable containing the return value of the method.
        /// </summary>
        public VariableDefinition? ReturnValue { get; }
    }

    /// <summary>
    /// Information relevant to Prefix hooks.
    /// </summary>
    public class TargetPrefixInfo
    {
        internal TargetPrefixInfo(ILContext Context)
        {
            ControlFlow = Context.DeclareVariable(typeof(int));
            TemporaryControlFlow = Context.DeclareVariable(typeof(int));
        }

        /// <summary>
        /// The local int used for determining the control flow of the method.<br/>
        /// See <see cref="DetourTypes.ReturnFlow"/>.
        /// </summary>
        public VariableDefinition ControlFlow { get; }

        /// <summary>
        /// The temporary local int used for setting the
        /// <see cref="ControlFlow"/> int.
        /// </summary>
        public VariableDefinition TemporaryControlFlow { get; }

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
    public class TargetPostfixInfo
    {
        internal TargetPostfixInfo() { }

        /// <summary>
        /// The first instruction for each postfix.
        /// </summary>
        public List<Instruction> FirstPostfixInstructions { get; } = [];
    }
}
