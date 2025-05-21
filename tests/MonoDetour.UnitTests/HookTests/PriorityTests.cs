using MonoDetour.Cil;

namespace MonoDetour.UnitTests.HookTests;

public static partial class PriorityTests
{
    private static readonly Queue<int> order = [];

    [Fact]
    public static void HookOrderTest()
    {
        var m = DefaultMonoDetourManager.New();
        // Also test that things work as expected with DetourContext.
        var scope = new DetourConfigContext(new(id: "detourContext")).Use();

        // Hooks with a default priority apply in reverse order
        // and MonoDetour enforces a default priority for its hooks.
        m.Hook<PostfixDetour>(Stub, Postfix2DetourContextId, config: null);
        m.Hook<PostfixDetour>(Stub, Postfix1DetourContextId, config: null);

        Stub();
        Assert.Equal([1, 2], order);
        order.Clear();

        MonoDetourConfig lowerPriority = new(priority: -1, overrideId: "lowerPriority");

        m.Hook<PostfixDetour>(Stub, Postfix5LowerPriority, lowerPriority);

        scope.Dispose();

        Stub();
        Assert.Equal([1, 2, 5], order);
        order.Clear();

        MonoDetourConfig beforeLowerPriority = new(priority: -2, before: ["lowerPriority"]);

        m.Hook<PostfixDetour>(Stub, Postfix4BeforeLowerPriority, beforeLowerPriority);
        m.Hook<PostfixDetour>(
            Stub,
            Postfix3BeforeAnyWithAssemblyNameAsId,
            new(priority: -100, before: [typeof(PriorityTests).Assembly.GetName().Name!])
        );

        Stub();
        Assert.Equal([1, 2, 3, 4, 5], order);

        m.DisposeHooks();
    }

    static void Postfix1DetourContextId() => order.Enqueue(1);

    static void Postfix2DetourContextId() => order.Enqueue(2);

    static void Postfix3BeforeAnyWithAssemblyNameAsId() => order.Enqueue(3);

    static void Postfix4BeforeLowerPriority() => order.Enqueue(4);

    static void Postfix5LowerPriority() => order.Enqueue(5);

    static void Stub()
    {
        return;
    }
}
