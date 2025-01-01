using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

public class Plugin
{
    public static void Main()
    {
        var p = new PlatformerController();
        var timer = new Stopwatch();
        timer.Start();
        for (int i = 0; i < 10000000; i++)
        {
            p.SpinBounce(5);
        }
        timer.Stop();
        Console.WriteLine($"No hook: {timer.ElapsedMilliseconds} ms");
        DetourManager.HookAllInExecutingAssembly();
        // On.PlatformerController.SpinBounce.Prefix(MyPatch);
        On.PlatformerController.SpinBounce.Prefix((ref On.PlatformerController.SpinBounce.Args a) => { });
        // new Hook(typeof(PlatformerController).GetMethod(nameof(PlatformerController.SpinBounce)), MyHookMonoMod);
        // new ILHook(typeof(PlatformerController).GetMethod(nameof(PlatformerController.SpinBounce)), MyMonoModILHook);
        timer.Reset();
        timer.Start();
        for (int i = 0; i < 10000000; i++)
        {
            p.SpinBounce(5);
        }
        timer.Stop();
        Console.WriteLine($"With hook: {timer.ElapsedMilliseconds} ms");
    }

    // public class PlatformerController
    // {
    //     public void SpinBounce(float power)
    //     {
    //         // Console.WriteLine("power is: " + power);
    //         var x = 5 * 2;
    //     }
    // }

    [MonoDetour(DetourType.Prefix)]
    static void MyPatch(ref On.PlatformerController.SpinBounce.Args args)
    {
        args.self.Foo();
        args.power += 2;
    }

    static void Hook10(ref On.PlatformerController.SpinBounce.Args a)
    {
        a.power = 10;
        a.self.Foo();
    }

    [MonoDetour(DetourType.Prefix)]
    static void MyPatch2(ref On.PlatformerController.SpinBounce.Args args)
    {
        args.power -= 1;
    }

    [MonoDetour(DetourType.Prefix)]
    static void MyPatch3(ref On.PlatformerController.SpinBounce.Args args)
    {
        args.power += 5;
    }

    [MonoDetour(DetourType.Prefix)]
    static void MyPatch4(ref On.PlatformerController.SpinBounce.Args args)
    {
        args.power -= 4;
    }
}

interface IMonoDetour
{

}

public static class DetourManager
{
    static Dictionary<MethodBase, List<MethodBase>> _manipulators = [];
    static MethodBase _target = null!;
    static MethodBase _manipulator = null!;


    public static void HookAllInExecutingAssembly() =>
        HookAllInAssembly(Assembly.GetCallingAssembly());


    public static void HookAllInAssembly(Assembly assembly)
    {

        foreach (Type type in GetTypesFromAssembly(assembly))
        {
            var attribute = GetMonoDetourAttribute(type);
            // if (attribute is null)
            //     continue;
            // Console.WriteLine(type.ToString());


            MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
            foreach (var method in methods)
            {
                var monoDetourAttribute = GetMonoDetourAttribute(method);
                if (monoDetourAttribute is null)
                    continue;

                Hook(method);
            }
        }
    }

    public static void Hook(MethodBase manipulator)
    {
        var target = GetMonoDetourArgumentType(manipulator).DeclaringType.GetMethod("Target");
        Hook((MethodBase)target.Invoke(null, null), manipulator);
    }

    static ILHook iLHook = null!;
    public static void Hook(MethodBase target, MethodBase manipulator)
    {
        var attribute = GetMonoDetourAttribute(manipulator) ?? throw new ArgumentException();
        _target = target;
        _manipulator = manipulator;
        if (_manipulators.TryGetValue(target, out var manipulators))
        {
            manipulators.Add(manipulator);
            iLHook.Undo();
            iLHook.Apply();
            return;
        }

        _manipulators.Add(target, [manipulator]);

        switch (attribute.DetourType)
        {
            case DetourType.Prefix:
                Console.WriteLine("Hooking");
                iLHook = new ILHook(target, PrefixEmitter);
                break;
        }
    }

    private static void PrefixEmitter(ILContext il)
    {
        Console.WriteLine("Original: " + il.ToString());

        var argsType = GetMonoDetourArgumentType(_manipulator);
        // if (args is not IMonoDetourArgs)
        //     throw new NotSupportedException();

        il.DeclareVariable(argsType);
        int structIdx = il.Body.Variables.Count - 1;

        ILCursor c = new(il);
        c.Emit(OpCodes.Ldloca, structIdx);
        c.Emit(OpCodes.Initobj, argsType);

        var fields = argsType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            Console.WriteLine($"field: {field.Name}");

            foreach (var origParam in il.Method.Parameters)
            {
                if (field.Name != origParam.Name)
                {
                    if (!(field.Name == "self" && origParam.Name == "this" && origParam.Index == 0))
                        continue;
                }

                c.Emit(OpCodes.Ldloca, structIdx);
                c.Emit(OpCodes.Ldarg, origParam.Index);
                c.Emit(OpCodes.Stfld, field);
            }
        }

        foreach (var manipulator in _manipulators[_target])
        {
            c.Emit(OpCodes.Ldloca, structIdx);
            c.Emit(OpCodes.Call, manipulator);
        }

        foreach (var field in fields)
        {

            foreach (var origParam in il.Method.Parameters)
            {
                if (field.Name != origParam.Name)
                {
                    if (!(field.Name == "self" && origParam.Name == "this" && origParam.Index == 0))
                        continue;
                }

                c.Emit(OpCodes.Ldloca, structIdx);
                c.Emit(OpCodes.Ldfld, field);
                c.Emit(OpCodes.Starg, origParam.Index);
            }
        }

        Console.WriteLine("Manipulated: " + il.ToString());
    }

    public static MonoDetourAttribute? GetMonoDetourAttribute(MemberInfo member)
    {
        // Console.WriteLine("+ " + member.ToString());
        var customAttributes = member.GetCustomAttributes();
        foreach (var customAttribute in customAttributes)
        {
            if (customAttribute is MonoDetourAttribute attribute)
                return attribute;
            // else
            //     Console.WriteLine("- " + customAttribute.ToString());
        }

        return null;
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
            var tarr = assembly.GetTypes();
            for (var i = 0; i < tarr.Length; i++)
            {
                if (tarr[i] == null)
                    return tarr.Where(type => type is not null).ToArray();
            }
            return tarr;
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).ToArray();
        }
    }

    public static Type GetMonoDetourArgumentType(MethodBase method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 1)
            throw new NotImplementedException();

        return parameters[0].ParameterType.GetElementType(); // Need this because it's a ref type
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class MonoDetourAttribute(DetourType detourType) : Attribute
{
    public DetourType DetourType { get; } = detourType;
}

public enum DetourType
{
    Prefix,
    Postfix,
    Transpiler,
    Wrapper,
}
