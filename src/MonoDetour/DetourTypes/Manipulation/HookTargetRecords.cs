using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.RuntimeDetour;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes.Manipulation;

/// <summary>
/// An API for hooks to share information about target methods.
/// </summary>
public static class HookTargetRecords
{
    static readonly ConditionalWeakTable<MethodDefinition, HookTargetInfo> s_MethodToInfo = new();

    /// <summary>
    /// Initializes MonoDetour's hooks on MonoMod to allow tracking
    /// of "original" instructions of methods.
    /// </summary>
    /// <remarks>
    /// It is only necessary to call this if you need to use
    /// <see cref="GetHookTargetInfo(MethodDefinition)"/>
    /// without using any <see cref="MonoDetourHook"/>s
    /// which would have initialized this automatically.
    /// <br/>
    /// This also initializes some other unrelated but useful tweaks MonoDetour expects.
    /// </remarks>
    public static void InitializeIfNeeded()
    {
        ModuleInitialization.InitializeModule();
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

        info = new(method, returnValue, GetOriginalInstructions(method));
        s_MethodToInfo.Add(method, info);
        return info;
    }

    /// <summary>
    /// Gets the original instructions for the <see cref="MethodDefinition"/>.<br/>
    /// <br/>
    /// This doesn't provide a fully unmodified original instructions list but a snapshot
    /// of the original instruction list before it was manipulated. As such this shares
    /// instruction instances with the current <see cref="ILContext"/>.
    /// </summary>
    /// <remarks>
    /// This method is strictly intended to be used in <see cref="MonoMod.RuntimeDetour.ILHook"/>
    /// manipulators, including <see cref="MonoDetourHook"/>. Usage elsewhere returns an empty collection.
    /// </remarks>
    /// <param name="method">The <see cref="MethodDefinition"/> whose original instructions to get.</param>
    /// <returns>The original instructions, or an empty collection if MonoDetour doesn't know about the
    /// <see cref="MethodDefinition"/> because it wasn't an ILHook manipulation target
    /// managed by MonoMod.</returns>
    internal static ReadOnlyCollection<Instruction> GetOriginalInstructions(MethodDefinition method)
    {
        var methodInstructions = ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions;

        if (methodInstructions.TryGetValue(method, out var instructions))
        {
            return instructions;
        }

#if NETSTANDARD2_0
        return new([]);
#else
        return ReadOnlyCollection<Instruction>.Empty;
#endif
    }

    [Obsolete(
        "For backwards compatibility with old MonoDetour.Interop.HarmonyX versions, just to be safe for now."
    )]
    internal static void SwapOriginalInstructionsCollection(
        MethodDefinition method,
        HookTargetInfo info,
        ReadOnlyCollection<Instruction> replacement
    ) => SwapOriginalInstructions(method, info, replacement, null);

    internal static void SwapOriginalInstructions(
        MethodDefinition method,
        HookTargetInfo info,
        ReadOnlyCollection<Instruction> replacement,
        Dictionary<Instruction, Instruction>? oldToNew
    )
    {
        ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions.Remove(method);
        ILHookDMDManipulation.s_MethodDefinitionToOriginalInstructions.Add(method, replacement);
        info.OriginalInstructions = replacement;

        if (oldToNew is null)
        {
            // Old MonoDetour.Interop.HarmonyX. Remove this check later.
            return;
        }

        SwapInstructionToSourceInstructions(info, oldToNew);
        SwapOriginalPostfixes(info, oldToNew);
        SwapPersistentInstructions(info, oldToNew);
    }

    private static void SwapInstructionToSourceInstructions(
        HookTargetInfo info,
        Dictionary<Instruction, Instruction> oldToNew
    )
    {
        var instructionToSource = info.InstructionToSource;

        foreach (var pair in oldToNew)
        {
            var oldInstruction = pair.Key;
            var newInstruction = pair.Value;

            if (!instructionToSource.TryGetValue(oldInstruction, out var source))
                continue;

            instructionToSource.Remove(oldInstruction);
            instructionToSource[newInstruction] = source;
        }
    }

    private static void SwapOriginalPostfixes(
        HookTargetInfo info,
        Dictionary<Instruction, Instruction> oldToNew
    )
    {
        var postfixes = info.PostfixInfo.FirstPostfixInstructions;

        for (int i = 0; i < postfixes.Count; i++)
        {
            var oldInstruction = postfixes[i];

            if (!oldToNew.TryGetValue(oldInstruction, out var newInstruction))
                continue;

            postfixes[i] = newInstruction;
        }
    }

    private static void SwapPersistentInstructions(
        HookTargetInfo hookTargetInfo,
        Dictionary<Instruction, Instruction> oldToNew
    )
    {
        var oldPersistentInstructions = hookTargetInfo.PersistentInstructions.ToArray();
        hookTargetInfo.PersistentInstructions.Clear();

        foreach (var oldInstruction in oldPersistentInstructions)
        {
            if (!oldToNew.TryGetValue(oldInstruction, out var newInstruction))
                continue;

            hookTargetInfo.MarkPersistentInstruction(newInstruction);
        }
    }

    /// <summary>
    /// Information about a hook target method.
    /// </summary>
    public class HookTargetInfo
    {
        internal HookTargetInfo(
            MethodDefinition method,
            VariableDefinition? returnValue,
            ReadOnlyCollection<Instruction> originalInstructions
        )
        {
            PrefixInfo = new(method);
            ReturnValue = returnValue;
            OriginalInstructions = originalInstructions;
        }

        /// <inheritdoc cref="TargetPrefixInfo"/>
        public TargetPrefixInfo PrefixInfo { get; }

        /// <inheritdoc cref="TargetPostfixInfo"/>
        public TargetPostfixInfo PostfixInfo { get; } = new();

        /// <summary>
        /// The local variable containing the return value of the method.
        /// </summary>
        public VariableDefinition? ReturnValue { get; }

        /// <summary>
        /// A list of the original instructions before the method was manipulated.
        /// </summary>
        /// <remarks>
        /// This list has the same instruction instances as the current <see cref="ILContext"/>,
        /// meaning some may have been modified.
        /// </remarks>
        public ReadOnlyCollection<Instruction> OriginalInstructions
        {
            get;
            internal set
            {
                field = value;
                isOriginalInstructionsHashSetDirty = true;
            }
        }

        bool isOriginalInstructionsHashSetDirty;

        internal HashSet<Instruction> OriginalInstructionsHashSet
        {
            get
            {
                if (!isOriginalInstructionsHashSetDirty)
                {
                    return field ??= [.. OriginalInstructions];
                }

                return field = [.. OriginalInstructions];
            }
        }

        internal HashSet<Instruction> PersistentInstructions { get; } = [];

        internal Dictionary<
            Instruction,
            (IReadOnlyMonoDetourHook?, string?)?
        > InstructionToSource { get; } = [];

        /// <summary>
        /// Give the provided instruction an owner name to identify it by
        /// in MonoDetour's CIL analysis exception reports.
        /// </summary>
        /// <remarks>
        /// Names above 25 characters are truncated.
        /// Note that this limit could be changed in the future.
        /// </remarks>
        /// <param name="instruction">The instruction to give an owner to.</param>
        /// <param name="owner">The owner name.</param>
        public void SetInstructionOwner(Instruction instruction, string owner)
        {
            InstructionToSource[instruction] = (null, Helpers.ThrowIfNull(owner));
        }

        /// <summary>
        /// Makes the specified instruction non-redirectable by HarmonyX.
        /// </summary>
        /// <param name="instruction">Instruction to mark.</param>
        /// <returns>The provided instruction.</returns>
        internal Instruction MarkPersistentInstruction(Instruction instruction)
        {
            PersistentInstructions.Add(instruction);
            return instruction;
        }

        /// <summary>
        /// Checks if the specified instruction is marked as persistent.
        /// </summary>
        /// <param name="instruction">The instruction to check.</param>
        /// <returns><see langword="true"/> if persistent; <see langword="false"/> otherwise.</returns>
        internal bool IsPersistentInstruction(Instruction instruction) =>
            PersistentInstructions.Contains(instruction);
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

        internal int AppliedControlFlowPrefixes { get; private set; }

        /// <summary>
        /// Sets <see cref="ControlFlowImplemented"/> to true.
        /// </summary>
        public void SetControlFlowImplemented() => ControlFlowImplemented = true;

        internal void MarkControlFlowPrefixAsApplied() => AppliedControlFlowPrefixes++;
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
