using Md.MonoDetour.UnitTests.TestLib.StrippingWorks;

namespace MonoDetour.UnitTests.FunctionalityTests;

// If this builds on Debug and Release, it works
public class StrippingHooksWorks
{
    static StrippingHooksWorks()
    {
        StaticConstructor04.ILHook(NoOp);
    }

    public StrippingHooksWorks()
    {
        Constructor03.ILHook(NoOp);
    }

    public static void AnalyzeOptionalParam()
    {
        static void L01()
        {
            Local01.ILHook(NoOp);
            var m = () =>
            {
                Delegate02.ILHook(NoOp);
            };
            m();
        }
        L01();
    }

    static void NoOp(ILManipulationInfo info) { }
}
