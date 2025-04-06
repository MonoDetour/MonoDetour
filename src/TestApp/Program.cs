using System;
using Mono.Cecil.Cil;
using MonoDetour;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

static class Program
{
    internal static MonoDetourManager m = new();

    static void Main()
    {
        MonoMod.RuntimeDetour.DetourManager.ILHookApplied += OnApplied;
        Console.WriteLine("running...");
        // call System.Void PlatformerControllerPatches::PrintBar(On.PlatformerController.SpinBounce/Params&)
        // call System.Void Program/<>c::<Main>b__1_0(On.PlatformerController.SpinBounce/Params&)
        // DetourManager.Hook(PlatformerControllerPatches.PrintBar);
        m.HookAllInExecutingAssembly();

        // int f = 10;

        // On.PlatformerController.SpinBounce.Prefix(m, (ref On.PlatformerController.SpinBounce.Params a) =>
        // {
        //     Console.WriteLine("power: " + a.power.ToString());
        //     a.self.Foo();
        //     // Console.WriteLine("f: " + f.ToString());
        // });
        On.PlatformerController.SpinBounce.ILHook(
            m,
            static il =>
            {
                try
                {
                    Console.WriteLine("hi");
                    ILWeaver w = new(il);
                    w.GotoMatch(
                            GotoType.FirstPredicate,
                            x => x.MatchLdarg(0),
                            x => true,
                            x => x.Match(OpCodes.Ldsfld)
                        )
                        .Accept();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        );

        // On.PlatformerController.SpinBounce.ILHook(m, static (ILContext il) =>
        // {
        //     Console.WriteLine("hello");
        // });


        // On.PlatformerController.SpinBounce.Postfix(m, static (ref On.PlatformerController.SpinBounce.Params a) =>
        // {

        // });

        var x = new PlatformerController();
        x.SpinBounce(5);
    }

    private static void OnApplied(ILHookInfo info)
    {
        Console.WriteLine("Applied hook: {" + info.ManipulatorMethod.Name + "} end");
    }
}

[MonoDetourTargets(typeof(PlatformerController))]
class PlatformerControllerPatches
{
    [MonoDetourHook<PrefixDetour>()]
    internal static void MyPatch1(ref On.PlatformerController.SpinBounce.Params a)
    {
        Console.WriteLine("hello: " + a.power.ToString());
        a.self.Foo();
        a.power += 1;
    }

    [MonoDetourHook(DetourType.Postfix)]
    internal static void MyPatchPrintBar(in On.PlatformerController.SpinBounce.Params a)
    {
        Console.WriteLine("bar");
    }
}
