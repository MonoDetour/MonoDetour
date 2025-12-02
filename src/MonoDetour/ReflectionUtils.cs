using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoDetour.Logging;

namespace MonoDetour;

internal static class ReflectionUtils
{
    public static bool HasCustomAttribute<T>(MemberInfo member, bool reportUnloadableTypes)
    {
        IEnumerable<Attribute> customAttributes;

        try
        {
            customAttributes = member.GetCustomAttributes();
        }
        catch (TypeLoadException)
        {
            // If we work around unloadable attributes,
            // method invocation on the member type appears to fail anyways.
            // So, this is appropriate behavior for the intended purpose of this method.

            if (!reportUnloadableTypes)
                return false;

            bool hasOurAttribute = false;

            foreach (var attribute in member.CustomAttributes)
            {
                try
                {
                    if (typeof(T).IsAssignableFrom(attribute.AttributeType))
                    {
                        hasOurAttribute = true;
                        break;
                    }
                }
                catch (TypeLoadException)
                {
                    // While undocumented, IsAssignableFrom can throw (at least on Mono) even if
                    // the ReflectionTypeLoadException for whatever reason wasn't thrown earlier.
                }
            }

            if (hasOurAttribute)
            {
                MonoDetourLogger.Log(
                    MonoDetourLogger.LogChannel.Warning,
                    $"[{nameof(MonoDetourManager.InvokeHookInitializers)}]"
                        + $" Skipping '{member}' ({member.Module.Assembly.GetName().Name}) due to unloadable type."
                        + $" Use '{nameof(reportUnloadableTypes)}: false' to hide this message."
                );
            }
            return false;
        }

        foreach (var customAttribute in customAttributes)
        {
            if (customAttribute is T)
                return true;
        }

        return false;
    }

    public static Type[] GetTypesFromAssembly(Assembly assembly, bool reportUnloadableTypes)
    {
        try
        {
            // GetTypes can not throw on some assemblies with unloadable types and instead return an array with some nulls in it.
            // This is very rare so check first and only create a new array if something is actually found.
            var types = assembly.GetTypes();
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i] == null)
                {
                    ReportUnloadableAssemblyTypes(assembly, reportUnloadableTypes);
                    return [.. types.Where(type => type is not null)];
                }
            }
            return types;
        }
        catch (ReflectionTypeLoadException ex)
        {
            ReportUnloadableAssemblyTypes(assembly, reportUnloadableTypes);
            return [.. ex.Types.Where(type => type is not null)!];
        }
    }

    static void ReportUnloadableAssemblyTypes(Assembly assembly, bool reportUnloadableTypes)
    {
        if (!reportUnloadableTypes)
            return;

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.Warning,
            $"[{nameof(MonoDetourManager.InvokeHookInitializers)}]"
                + $" Unloadable type(s) found in '{assembly.GetName().Name}'."
                + $" If such a type has hook initializers, they will be skipped."
                + $" Use '{nameof(reportUnloadableTypes)}: false' to hide this message."
        );
    }
}
