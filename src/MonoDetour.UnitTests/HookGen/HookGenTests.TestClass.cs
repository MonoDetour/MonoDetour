namespace MonoDetour.UnitTests.HookGen;

public partial class HookGenTests
{
    public class TestClass
    {
        public TestClass() { }

        public static void Single() { }

        public static void Overloaded() { }

        public static void Overloaded(int i) { }
    }
}
