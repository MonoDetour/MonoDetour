using System.Reflection;
using System.Runtime.CompilerServices;
using Op = System.Reflection.Emit.OpCodes;

var m = DefaultMonoDetourManager.New();

var prefix = m.Hook<PostfixDetour>(Target, Prefix_Target);

// ApplyTimes(1);

// void ApplyTimes(int times)
// {
//     for (int i = 0; i < times; i++)
//     {
//         m.Hook<PostfixDetour>(Target, Prefix_Target);
//     }
// }

for (int i = 0; i < 500; i++)
{
    UndoHooksWrapper();
    ApplyHooksWrapper();
}

[MethodImpl(MethodImplOptions.NoInlining)]
void UndoHooksWrapper() => m.UndoHooks();

[MethodImpl(MethodImplOptions.NoInlining)]
void ApplyHooksWrapper() => m.ApplyHooks();

m.UndoHooks();
FinalHook();

void FinalHook()
{
    m.ApplyHooks();
}

Target();

[MethodImpl(MethodImplOptions.NoInlining)]
static int Stub() => 1;
static void Prefix_Target() { }
static void Target()
{
    _ = Stub();
    _ = Stub();
}
