using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace MonoDetour;

public class MonoDetourManager
{
    public List<ILHook> ILHooks { get; } = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HookAllInExecutingAssembly() => HookAllInAssembly(Assembly.GetCallingAssembly());

    public void HookAllInAssembly(Assembly assembly)
    {
        foreach (Type type in MonoDetourUtils.GetTypesFromAssembly(assembly))
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourTargetsAttribute>(type, out _))
                continue;

            MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
            foreach (var method in methods)
            {
                if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookAttribute>(method, out _))
                    continue;

                HookGenReflectedHook(method);
            }
        }
    }

    public ILHook Hook(MethodBase target, ILContext.Manipulator manipulator)
    {
        var ilHook = new ILHook(target, manipulator);
        ILHooks.Add(ilHook);
        return ilHook;
    }

    public ILHook HookGenReflectedHook(Delegate manipulator, MonoDetourInfo? info = null) =>
        HookGenReflectedHook(manipulator.Method, info);

    public ILHook HookGenReflectedHook(MethodBase manipulator, MonoDetourInfo? info = null)
    {
        if (!MonoDetourUtils.TryGetMonoDetourParameter(manipulator, out _, out var parameterType))
        {
            throw new Exception("Manipulator method must have only one parameter.");
        }

        if (parameterType.DeclaringType is null)
        {
            throw new Exception("DeclaringType of Manipulator method's parameter Type is null.");
        }

        var targetMethod =
            parameterType.DeclaringType.GetMethod("Target")
            ?? throw new Exception(
                "DeclaringType of Manipulator method's parameter Type does not have a method 'Target'."
            );

        var targetReturnValue = targetMethod.Invoke(null, null);
        if (targetReturnValue is not MethodBase returnedTargetMethod)
            throw new Exception(
                "'Target' method in DeclaringType of Manipulator method's parameter Type doesn't return a MethodBase."
            );

        return HookGenReflectedHook(returnedTargetMethod, manipulator, info);
    }

    public ILHook HookGenReflectedHook(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourInfo? info = null
    )
    {
        if (info is null || info.DetourType is null)
        {
            if (
                !MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookAttribute>(
                    manipulator,
                    out var monoDetourAttribute
                )
            )
                throw new ArgumentException($"Missing {nameof(MonoDetourHookAttribute)}");

            if (info is null)
                info = new(monoDetourAttribute.Info.DetourType);
            else
            {
                info.DetourType = monoDetourAttribute.Info.DetourType;
                MonoDetourUtils.ThrowIfInvalidDetourType(info.DetourType);
            }
        }

        if (
            !MonoDetourUtils.TryGetMonoDetourParameter(
                manipulator,
                out var parameterInfo,
                out var structType
            )
        )
            throw new Exception("Manipulator method must have only one parameter.");

        FieldInfo[] fields = structType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        if (fields.Length == 0)
            throw new Exception(
                "The Manipulator method parameter type has no public instance fields."
            );

        MonoDetourData data = info.Data;
        data.Target = target;
        data.Manipulator = manipulator;
        data.ManipulatorParameter = parameterInfo;
        data.ManipulatorParameterType = structType;
        data.ManipulatorParameterTypeFields = fields;

        return HookGenReflectedHook(info);
    }

    public ILHook HookGenReflectedHook(MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new ArgumentException($"{nameof(MonoDetourInfo)} is not fully initialized.");

        Console.WriteLine("Hooking");

        var emitter = (IMonoDetourHookEmitter)Activator.CreateInstance(info.DetourType)!;
        emitter.Info = info;

        ILHook iLHook = new(info.Data.Target, emitter.ILHookManipulator);
        ILHooks.Add(iLHook);
        return iLHook;
    }
}
