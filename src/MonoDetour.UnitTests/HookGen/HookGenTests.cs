using MonoDetour.HookGen;
using SomeNamespace;

// [assembly: MonoMod.HookGen.GenerateHookHelpers(typeof(TestApp.GameNetcodeStuff.PlayerControllerB))]
[assembly: GenerateHookHelpers(typeof(SomeNamespace.SomeType))]

// [assembly: GenerateHookHelpers(typeof(TestApp.PlatformerController))]

namespace MonoDetour.UnitTests.HookGen;

[MonoDetourTargets]
public partial class HookGenTests
{
    // private static readonly MonoDetourManager m = new();

    // private static readonly Type generatorType =
    //     typeof(MonoMod.Roslyn.UnitTests.Verifiers.Adapter<HookHelperGenerator>);
    // private static readonly (Type, string, string) attributesSource = (
    //     generatorType,
    //     HookHelperGenerator.GenHelperForTypeAttrFile,
    //     HookHelperGenerator.GenHelperForTypeAttributeSource
    // );

    // internal static readonly MetadataReference SelfMetadataReference =
    //     MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location);
    // internal static readonly MetadataReference RuntimeDetourMetadataReference =
    //     MetadataReference.CreateFromFile(typeof(Hook).Assembly.Location);
    // internal static readonly MetadataReference UtilsMetadataReference =
    //     MetadataReference.CreateFromFile(typeof(MonoMod.Cil.ILContext).Assembly.Location);

    // internal static readonly MetadataReference LethalCompanyMetadataReference =
    //     MetadataReference.CreateFromFile(
    //         typeof(TestApp.GameNetcodeStuff.PlayerControllerB).Assembly.Location
    //     );

    [Fact]
    public void Hook1()
    {
        On.SomeNamespace.SomeType.SomeMethod.Prefix(Prefix_SomeType_SomeMethod);
    }

    private static void Prefix_SomeType_SomeMethod(
        ref On.SomeNamespace.SomeType.SomeMethod.Params args
    )
    {
        Console.WriteLine("Hello from Prefix hook!");
    }

    [Fact]
    public void Hook2()
    {
        HookGenManager.Instance.HookAll();
        var someType = new SomeType();
        someType.SomeMethod(1);
    }

    [MonoDetourHook(DetourType.PrefixDetour)]
    private static void Prefix2_SomeType_SomeMethod(
        ref On.SomeNamespace.SomeType.SomeMethod.Params args
    )
    {
        Console.WriteLine("Hello from Prefix hook 2!");
        Console.WriteLine("Number is " + args.number_1);
    }

    [MonoDetourHook<PrefixDetour>]
    private static void Prefix3_SomeType_SomeMethod(
        ref On.SomeNamespace.SomeType.SomeMethod.Params args
    )
    {
        args.number_1 = 3;
        Console.WriteLine("Hello from Prefix hook 3!");
    }

    // private static void MoveNext_ctor(ref _DoStuff_d__3._ctor.Params args)
    // {
    //     Console.WriteLine("Hello from MoveNext ctor!");
    // }

    // private static void PlatformerController_SpinBounce(ref SpinBounce.Params args)
    // {
    //     args.self.Foo();
    //     args.self.DoStuff();
    // }
}
