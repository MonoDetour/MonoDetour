using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour;

internal static class MonoDetourUtils
{
    public static int EmitParamsStruct(this ILCursor c, Type structType, FieldInfo[] structFields)
    {
        c.Context.DeclareVariable(structType);
        int structParamIdx = c.Body.Variables.Count - 1;

        c.Emit(OpCodes.Ldloca, structParamIdx);
        c.Emit(OpCodes.Initobj, structType);

        c.ForEachMatchingParam(
            structFields,
            (structField, methodParam) =>
            {
                c.Emit(OpCodes.Ldloca, structParamIdx);
#if NET7_0_OR_GREATER
                c.Emit(OpCodes.Ldarga, methodParam.Index);
#else
                c.Emit(OpCodes.Ldarg, methodParam.Index);
#endif
                c.Emit(OpCodes.Stfld, structField);
            }
        );

        return structParamIdx;
    }

    public static void ApplyStructValuesToMethod(
        this ILCursor c,
        FieldInfo[] structFields,
        int structParamIdx
    )
    {
        c.ForEachMatchingParam(
            structFields,
            (structField, methodParam) =>
            {
                c.Emit(OpCodes.Ldloca, structParamIdx);
                c.Emit(OpCodes.Ldfld, structField);
                c.Emit(OpCodes.Starg, methodParam.Index);
            }
        );
    }

    public static void ForEachMatchingParam(
        this ILCursor c,
        FieldInfo[] fields,
        Action<FieldInfo, ParameterDefinition> action
    )
    {
        foreach (var field in fields)
        {
            // Console.WriteLine($"field: {field.Name}");

            foreach (var origParam in c.Method.Parameters)
            {
                if (field.Name != origParam.Name)
                {
                    if (!(field.Name == "self" && origParam.Name == "this" && origParam.Index == 0))
                        continue;
                }

                action(field, origParam);
            }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(
        [NotNull] T? argument,
        [CallerArgumentExpression(nameof(argument))] string name = ""
    )
    {
        if (argument is null)
            ThrowArgumentNull(name);
        return argument;
    }

    [DoesNotReturn]
    private static void ThrowArgumentNull(string argName)
    {
        throw new ArgumentNullException(argName);
    }
}
