using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

static class ILHookGetDMDBeforeManipulation
{
    internal static readonly ConditionalWeakTable<
        MethodDefinition,
        ReadOnlyCollection<Instruction>
    > s_MethodDefinitionToOriginalInstructions = new();

    internal static readonly ConditionalWeakTable<
        MethodDefinition,
        MethodBase
    > s_MethodDefinitionToOriginalMethod = new();

    static ConstructorInfo dmdConstructor = null!;

    static ILHook getDmdBeforeManipulationHook = null!;

    static bool initialized = false;

    internal static void InitHook()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        try
        {
            if (MonoModVersion.IsReorg)
            {
                InitHookReorg();
            }
            else
            {
                InitHookLegacy();
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"MonoDetour doesn't seem to support this MonoMod version, "
                    + $"please report this issue: https://github.com/MonoDetour/MonoDetour: "
                    + $"'{typeof(Hook).Assembly}'",
                ex
            );
        }
    }

    static void InitHookReorg()
    {
        dmdConstructor =
            typeof(DynamicMethodDefinition).GetConstructor([typeof(DynamicMethodDefinition)])
            ?? throw new NullReferenceException("DMD constructor not found.");

        var type =
            Type.GetType(
                "MonoMod.RuntimeDetour.DetourManager+ManagedDetourState, MonoMod.RuntimeDetour"
            )
            ?? throw new NullReferenceException(
                "Type 'MonoMod.RuntimeDetour.DetourManager+ManagedDetourState, MonoMod.RuntimeDetour' not found."
            );

        var target =
            type.GetMethod("UpdateEndOfChain", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("Method 'UpdateEndOfChain' not found.");

        getDmdBeforeManipulationHook = new(target, GetDMD);
    }

    static void InitHookLegacy()
    {
        dmdConstructor =
            typeof(DynamicMethodDefinition).GetConstructor([typeof(MethodBase)])
            ?? throw new NullReferenceException("DMD constructor not found.");

        var type =
            Type.GetType("MonoMod.RuntimeDetour.ILHook+Context, MonoMod.RuntimeDetour")
            ?? throw new NullReferenceException(
                "Type 'MonoMod.RuntimeDetour.ILHook+Context, MonoMod.RuntimeDetour' not found."
            );

        var target =
            type.GetMethod("Refresh")
            ?? throw new NullReferenceException("Method 'Refresh' not found.");

        getDmdBeforeManipulationHook = new(target, GetDMD);
    }

    private static void GetDMD(ILContext il)
    {
        ILCursor c = new(il);
        bool found = c.TryGotoNext(MoveType.After, x => x.MatchNewobj(dmdConstructor));
        if (!found)
        {
            Console.WriteLine(il);
            throw new NullReferenceException("DMD construction not found.");
        }

        c.EmitDelegate(BorrowDMD);
    }

    static DynamicMethodDefinition BorrowDMD(DynamicMethodDefinition dmd)
    {
        var definition = dmd.Definition;
        var instructions = definition.Body.Instructions.ToList().AsReadOnly();
        s_MethodDefinitionToOriginalInstructions.Add(definition, instructions);
        s_MethodDefinitionToOriginalMethod.Add(definition, dmd.OriginalMethod);
        return dmd;
    }
}
