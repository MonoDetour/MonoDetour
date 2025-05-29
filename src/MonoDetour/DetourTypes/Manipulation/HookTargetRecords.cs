using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes.Manipulation;

/// <summary>
/// An API for hooks to share information about target methods.
/// </summary>
public static class HookTargetRecords
{
    static readonly ConditionalWeakTable<MethodDefinition, HookTargetInfo> s_MethodToInfo = new();

    /// <summary>
    /// Gets the original instructions for the <see cref="MethodDefinition"/>.<br/>
    /// <br/>
    /// This doesn't provide a fully unmodified original instructions list but a snapshot
    /// of the original instruction list before it was manipulated. As such this shares
    /// instruction instances with the current <see cref="ILContext"/>.
    /// </summary>
    /// <remarks>
    /// This method is strictly intended to be used in <see cref="MonoMod.RuntimeDetour.ILHook"/>
    /// manipulators, including <see cref="MonoDetourHook{TApplier}"/>. Usage elsewhere is not supported.
    /// </remarks>
    /// <param name="method">The <see cref="MethodDefinition"/> whose original instructions to get.</param>
    /// <returns>The original instructions.</returns>
    /// <exception cref="Exception"></exception>
    internal static ReadOnlyCollection<Instruction> GetOriginalInstructions(MethodDefinition method)
    {
        var methodInstructions =
            ILHookGetDMDBeforeManipulation.s_MethodDefinitionToOriginalInstructions;

        if (methodInstructions.TryGetValue(method, out var instructions))
        {
            return instructions;
        }

        throw new Exception(
            "Tried to get original instructions for a method which MonoDetour does not know about."
        );
    }

    /// <summary>
    /// Gets a <see cref="HookTargetInfo"/> for a target <see cref="ILContext"/>.
    /// </summary>
    /// <param name="il">The target <see cref="ILContext"/>.</param>
    /// <returns>A <see cref="HookTargetInfo"/> for the <see cref="ILContext"/>.</returns>
    public static HookTargetInfo GetHookTargetInfo(ILContext il)
    {
        if (s_MethodToInfo.TryGetValue(il.Method, out HookTargetInfo? info))
        {
            return info;
        }

        VariableDefinition? returnValue = null;
        if (il.Method.ReturnType.MetadataType != MetadataType.Void)
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
        /// See <see cref="ReturnFlow"/>.
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
