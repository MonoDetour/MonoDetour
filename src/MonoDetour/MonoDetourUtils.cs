using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour;

internal static class MonoDetourUtils
{
    public static bool TryGetCustomAttribute<T>(
        MemberInfo member,
        [NotNullWhen(true)] out T? attribute
    )
    {
        attribute = default;

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
}
