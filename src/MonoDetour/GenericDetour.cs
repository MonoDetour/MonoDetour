using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MonoDetour;

static class GenericDetour
{
    public static void Manipulator(ILContext il, MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new InvalidProgramException();

        MonoDetourData data = info.Data;

        // Console.WriteLine("Original: " + il.ToString());

        ILCursor c = new(il);

        if (info.DetourType == typeof(PostfixDetour))
            c.Index -= 1;

        int structArgumentIdx = c.EmitParamsStruct(
            data.ManipulatorParameterType,
            data.ManipulatorParameterTypeFields
        );

        c.Emit(OpCodes.Ldloca, structArgumentIdx);

        if (!data.Manipulator.IsStatic)
        {
            throw new NotSupportedException(
                "Only static manipulator methods are supported for now."
            );
        }
        else
            c.Emit(OpCodes.Call, data.Manipulator);

#if !NET7_0_OR_GREATER // ref fields are supported since net7.0 so we don't need to apply this 'hack'
        if (!data.ManipulatorParameter.IsIn)
            c.ApplyStructValuesToMethod(data.ManipulatorParameterTypeFields, structArgumentIdx);
#endif

        if (info.DetourType == typeof(PostfixDetour))
        {
            // redirect early ret calls to run postfixes and then return
            // c.TryGotoNext()
        }

        // Console.WriteLine("Manipulated: " + il.ToString());
    }
}
