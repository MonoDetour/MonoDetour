using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
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
    /// manipulators, including <see cref="MonoDetourHook"/>. Usage elsewhere is not supported.
    /// </remarks>
    /// <param name="method">The <see cref="MethodDefinition"/> whose original instructions to get.</param>
    /// <returns>The original instructions.</returns>
    /// <exception cref="Exception"></exception>
    internal static ReadOnlyCollection<Instruction> GetOriginalInstructions(MethodDefinition method)
    {
        var methodInstructions = ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions;

        if (methodInstructions.TryGetValue(method, out var instructions))
        {
            return instructions;
        }

        throw new Exception(
            "Tried to get original instructions for a method which MonoDetour does not know about."
        );
    }

    internal static void SwapOriginalInstructionsCollection(
        MethodDefinition method,
        ReadOnlyCollection<Instruction> replacement
    )
    {
        ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions.Remove(method);
        ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions.Add(method, replacement);
    }

    /// <param name="il">The <see cref="ILContext"/> for the target method.</param>
    /// <inheritdoc cref="GetHookTargetInfo(MethodDefinition)"/>
    public static HookTargetInfo GetHookTargetInfo(ILContext il) => GetHookTargetInfo(il.Method);

    /// <summary>
    /// Gets a <see cref="HookTargetInfo"/> for a target method.
    /// </summary>
    /// <param name="method">The target <see cref="MethodDefinition"/>.</param>
    /// <returns>A <see cref="HookTargetInfo"/> for the target method.</returns>
    public static HookTargetInfo GetHookTargetInfo(MethodDefinition method)
    {
        if (s_MethodToInfo.TryGetValue(method, out HookTargetInfo? info))
        {
            return info;
        }

        VariableDefinition? returnValue = null;
        if (method.ReturnType.MetadataType != MetadataType.Void)
        {
            returnValue = new VariableDefinition(method.ReturnType);
            method.Body.Variables.Add(returnValue);
        }

        info = new(method, returnValue);
        s_MethodToInfo.Add(method, info);
        return info;
    }

    /// <summary>
    /// Information about a hook target method.
    /// </summary>
    public class HookTargetInfo
    {
        internal HookTargetInfo(MethodDefinition method, VariableDefinition? returnValue)
        {
            PrefixInfo = new(method);
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
        internal TargetPrefixInfo(MethodDefinition method)
        {
            ControlFlow = method.DeclareVariable(typeof(int));
            TemporaryControlFlow = method.DeclareVariable(typeof(int));
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

    static VariableDefinition DeclareVariable(this MethodDefinition method, Type type)
    {
        var varDef = new VariableDefinition(method.Module.ImportReference(type));
        method.Body.Variables.Add(varDef);
        return varDef;
    }
}
