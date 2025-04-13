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
    public static int EmitParamsStruct(this ILCursor c, MonoDetourInfo info)
    {
        var structType = info.Data.ManipulatorParameterType!;
        var structFields = info.Data.ManipulatorParameterTypeFields!;

        c.Context.DeclareVariable(structType);
        int structParamIdx = c.Body.Variables.Count - 1;
        int? retTypeIdx = null;

        if (info.Data.Target is MethodInfo methodInfo)
        {
            if (methodInfo.ReturnType != typeof(void))
            {
                c.Context.DeclareVariable(methodInfo.ReturnType);
                retTypeIdx = c.Body.Variables.Count - 1;
            }
        }

        c.Emit(OpCodes.Ldloca, structParamIdx);
        c.Emit(OpCodes.Initobj, structType);

        c.ForEachMatchingParam(
            structFields,
            (structField, methodParam) =>
            {
                c.Emit(OpCodes.Ldloca, structParamIdx);

                // I'd want to add this preprocessor directive,
                // but we'd need support for this in our HookGen.
                // #if NET7_0_OR_GREATER
                // c.Emit(OpCodes.Ldarga, methodParam.Index);
                // #else
                c.Emit(OpCodes.Ldarg, methodParam.Index);
                // #endif
                c.Emit(OpCodes.Stfld, structField);
            }
        );

        if (info.DetourType == typeof(PostfixDetour))
        {
            FieldInfo? retField = structFields.FirstOrDefault(x => x.Name == "returnValue");
            if (retField is not null && retTypeIdx is not null)
            {
                // We grab the return value here and store it,
                // because it needs to be pushed to stack after
                // the Ldloca instruction for the Stfld instruction.
                c.Emit(OpCodes.Stloc, retTypeIdx);
                // Then we load our params struct.
                c.Emit(OpCodes.Ldloca, structParamIdx);
                // We push the return value back on the stack.
                c.Emit(OpCodes.Ldloc, retTypeIdx);
                // And then we set the returnValue field in the params struct.
                c.Emit(OpCodes.Stfld, retField);
            }
        }

        return structParamIdx;
    }

    public static void ApplyStructValuesToMethod(
        this ILCursor c,
        MonoDetourInfo info,
        int structParamIdx
    )
    {
        FieldInfo[] structFields = info.Data.ManipulatorParameterTypeFields!;

        c.ForEachMatchingParam(
            structFields,
            (structField, methodParam) =>
            {
                if (methodParam.ParameterType.IsByReference)
                {
                    // Ref arguments seem to be set like so:
                    // 1. Load ref argument address
                    c.Emit(OpCodes.Ldarg, methodParam.Index);
                    // 2. Load our object reference value
                    c.Emit(OpCodes.Ldloca, structParamIdx);
                    c.Emit(OpCodes.Ldfld, structField);
                    // 3. Store object reference value at address
                    c.Emit(OpCodes.Stind_Ref);
                }
                else
                {
                    c.Emit(OpCodes.Ldloca, structParamIdx);
                    c.Emit(OpCodes.Ldfld, structField);
                    c.Emit(OpCodes.Starg, methodParam.Index);
                }
            }
        );

        if (info.DetourType == typeof(PostfixDetour))
        {
            FieldInfo? retField = structFields.FirstOrDefault(x => x.Name == "returnValue");
            if (retField is not null)
            {
                // We consumed the original return value earlier.
                // Push our returnValue to stack.
                c.Emit(OpCodes.Ldloca, structParamIdx);
                c.Emit(OpCodes.Ldfld, retField);
            }
        }
    }

    public static void ForEachMatchingParam(
        this ILCursor c,
        FieldInfo[] fields,
        Action<FieldInfo, ParameterDefinition> action
    )
    {
        foreach (var field in fields)
        {
            bool isSelfField = field.Name == "self";
            bool isReturnField = field.Name == "returnValue";
            string realFieldName;

            if (!isSelfField && !isReturnField)
            {
                // We add $"_{argNum}" at the end of every argument except self so we strip it out here.
                realFieldName = field.Name.Substring(0, field.Name.LastIndexOf('_'));
            }
            else if (isSelfField)
            {
                realFieldName = "this";
            }
            else
            {
                // Not all fields we have are for parameters.
                continue;
            }

            // Console.WriteLine($"field: {field.Name}");
            // Console.WriteLine($"realFieldName: {realFieldName}");

            foreach (var origParam in c.Method.Parameters)
            {
                if (realFieldName != origParam.Name)
                    continue;

                if (isSelfField && origParam.Index != 0)
                    continue;

                if (!c.Method.IsStatic && !isSelfField && origParam.Index == 0)
                    continue;

                action(field, origParam);
                break;
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
}
