using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.DetourTypes;
using MonoMod.Cil;

namespace MonoDetour;

internal static class MonoDetourUtils
{
    public static int? EmitParams(this ILCursor c, MonoDetourInfo info)
    {
        var manipParams = info.Data.Manipulator!.GetParameters();

        int? retTypeIdx = null;

        if (info.Data.Target is MethodInfo methodInfo)
        {
            if (methodInfo.ReturnType != typeof(void))
            {
                c.Context.DeclareVariable(methodInfo.ReturnType);
                retTypeIdx = c.Body.Variables.Count - 1;
            }
        }

        ParameterInfo? retField = null;
        if (info.DetourType == typeof(PostfixDetour))
        {
            retField = manipParams.FirstOrDefault(x => x.Name == "returnValue");
        }

        if (retField is not null && retTypeIdx is not null)
        {
            // Store the original return value
            // for use after emitting params.
            c.Emit(OpCodes.Stloc, retTypeIdx);
        }

        bool isStatic = info.Data.Target!.IsStatic;

        foreach (var origParam in c.Method.Parameters)
        {
            if (!isStatic && origParam.Index == 0 || origParam.ParameterType.IsByReference)
            {
                // 'this', and reference types must not be passed by reference.
                c.Emit(OpCodes.Ldarg, origParam.Index);
            }
            else
            {
                c.Emit(OpCodes.Ldarga, origParam.Index);
            }
        }

        if (retField is not null && retTypeIdx is not null)
        {
            // Push return value address to the stack
            // so it can be manipulated by a hook.
            c.Emit(OpCodes.Ldloca, retTypeIdx);
        }

        return retTypeIdx;
    }

    public static void ApplyReturnValue(this ILCursor c, MonoDetourInfo info, int retTypeIdx)
    {
        var manipParams = info.Data.Manipulator!.GetParameters();

        ParameterInfo? retField = manipParams.FirstOrDefault(x => x.Name == "returnValue");
        if (retField is not null)
        {
            // We push the possibly manipulated return value to stack here.
            c.Emit(OpCodes.Ldloc, retTypeIdx);
        }
    }

    public static bool TryGetCustomAttribute<T>(
        MemberInfo member,
        [NotNullWhen(true)] out T? attribute
    )
        where T : Attribute
    {
        attribute = null;

        // Console.WriteLine("+ " + member.ToString());
        var customAttributes = member.GetCustomAttributes();
        foreach (var customAttribute in customAttributes)
        {
            if (customAttribute is T tAttribute)
            {
                attribute = tAttribute;
                return true;
            }
            // else
            //     Console.WriteLine("- " + customAttribute.ToString());
        }

        return false;
    }

    public static VariableDefinition DeclareVariable(this ILContext il, Type type)
    {
        var varDef = new VariableDefinition(il.Import(type));
        il.Body.Variables.Add(varDef);
        return varDef;
    }

    public static Type[] GetTypesFromAssembly(Assembly assembly)
    {
        try
        {
            // GetTypes can not throw on some assemblies with unloadable types and instead return an array with some nulls in it.
            // This is very rare so check first and only create a new array if something is actually found.
            var types = assembly.GetTypes();
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i] == null)
                    return [.. types.Where(type => type is not null)];
            }
            return types;
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.Where(type => type is not null)!];
        }
    }

    public static bool TryGetMonoDetourParameter(
        MethodBase method,
        [NotNullWhen(true)] out ParameterInfo? parameterInfo,
        [NotNullWhen(true)] out Type? parameterType
    )
    {
        parameterInfo = null;
        parameterType = null;

        var parameters = method.GetParameters();
        if (parameters.Length != 1)
            return false;

        parameterInfo = parameters[0];
        parameterType = parameterInfo.ParameterType;

        if (parameterType.IsByRef)
            parameterType = parameterType.GetElementType()!;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidDetourType(
        Type detourType,
        [CallerArgumentExpression(nameof(detourType))] string name = ""
    )
    {
        if (!typeof(IMonoDetourHookEmitter).IsAssignableFrom(detourType))
            throw new ArgumentException(
                $"{nameof(MonoDetourInfo)}.{nameof(MonoDetourInfo.DetourType)} must implement {nameof(IMonoDetourHookEmitter)}.",
                name
            );
    }
}
