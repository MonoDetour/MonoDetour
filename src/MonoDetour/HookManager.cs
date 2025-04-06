using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace MonoDetour;

public class HookManager
{
    public List<ILHook> ILHooks { get; } = [];

    public void HookAllInExecutingAssembly() => HookAllInAssembly(Assembly.GetCallingAssembly());

    public void HookAllInAssembly(Assembly assembly)
    {
        foreach (Type type in MonoDetourUtils.GetTypesFromAssembly(assembly))
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourHooksAttribute>(type, out _))
                continue;

            MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
            foreach (var method in methods)
            {
                if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourAttribute>(method, out _))
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

    public ILHook HookGenReflectedHook<T>(T manipulator, MonoDetourInfo? info = null)
        where T : Delegate
    {
        info ??= new();
        info.Manipulator = manipulator;
        return HookGenReflectedHook(manipulator.Method, info);
    }

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
        info ??= new();
        if (info.DetourType == 0)
        {
            if (
                !MonoDetourUtils.TryGetCustomAttribute<MonoDetourAttribute>(
                    manipulator,
                    out var monoDetourAttribute
                )
            )
                throw new ArgumentException($"Missing {nameof(MonoDetourAttribute)}");

            info.DetourType = monoDetourAttribute.Info.DetourType;
        }

        if (
            !MonoDetourUtils.TryGetMonoDetourParameter(
                manipulator,
                out var parameterInfo,
                out var structType
            )
        )
            throw new Exception("Manipulator method must have only one parameter.");

        Console.WriteLine("Hooking");

        if (info.DetourType == DetourType.ILHook)
        {
            if (structType != typeof(ILContext))
                throw new ArgumentException(
                    "Attempted to apply hook as an ILHook but the manipulator method doesn't accept an ILContext parameter."
                );

            var ilHookManipulator = (ILContext.Manipulator)
                Delegate.CreateDelegate(typeof(ILContext.Manipulator), (MethodInfo)manipulator);

            return Hook(target, ilHookManipulator);
        }

        var structFields = structType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        void PrefixEmitter(ILContext il) => Emitter(il, isPrefix: true);
        void PostfixEmitter(ILContext il) => Emitter(il, isPrefix: false);

        void Emitter(ILContext il, bool isPrefix)
        {
            // Console.WriteLine("Original: " + il.ToString());

            ILCursor c = new(il);

            if (!isPrefix)
                c.Index -= 1;

            int structArgumentIdx = c.EmitParamsStruct(structType, structFields);

            c.Emit(OpCodes.Ldloca, structArgumentIdx);

            if (!manipulator.IsStatic)
            {
                if (info.Manipulator is null)
                    throw new Exception("info.Manipulator is null");

                throw new NotSupportedException(
                    "Only static manipulator methods are supported for now."
                );
            }
            else
                c.Emit(OpCodes.Call, manipulator);

#if !NET7_0_OR_GREATER // ref fields are supported since net7.0 so we don't need to apply this 'hack'
            if (!parameterInfo.IsIn)
                c.ApplyStructValuesToMethod(structFields, structArgumentIdx);
#endif

            if (!isPrefix)
            {
                // redirect early ret calls to run postfixes and then return
                // c.TryGotoNext()
            }

            // Console.WriteLine("Manipulated: " + il.ToString());
        }

        ILHook iLHook = info.DetourType switch
        {
            DetourType.Prefix => new ILHook(target, PrefixEmitter),
            DetourType.Postfix => new ILHook(target, PostfixEmitter),
            _ => throw new NotImplementedException(),
        };
        ILHooks.Add(iLHook);
        return iLHook;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MonoDetourHooksAttribute(params Type[] targetTypes) : Attribute
{
    public Type[] TargetTypes => targetTypes;
}

public enum DetourType
{
    Prefix = 1,
    Postfix = 2,
    ILHook = 3,
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MonoDetourAttribute(DetourType detourType) : Attribute
{
    public MonoDetourInfo Info { get; set; } = new() { DetourType = detourType };
}

public class MonoDetourInfo()
{
    public DetourType DetourType { get; set; }
    public Delegate? Manipulator { get; set; }
}
