using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;

static class ILContextExtensions
{
    public static VariableDefinition DeclareVariable(this ILContext il, Type type)
    {
        var varDef = new VariableDefinition(il.Import(type));
        il.Body.Variables.Add(varDef);
        return varDef;
    }
}
