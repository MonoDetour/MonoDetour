using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MonoDetour.UnitTests.Examples.Basic;

public static class AddingLogic
{
    /*
    In this example, we are going to add new logic inside the (fictional) `Gun.OnUse` method:

    ```cs
    class Gun(int magazineSize) : Item
    {
        internal int _ammo = magazineSize;

        public override void OnUse(Entity user)
        {
            if (_ammo <= 0)
                return;

            _ammo--;

            if (!user.TryGetTarget(out Entity? target))
            {
                return;
            }

            target.TakeDamage(4);
        }
    }
    ```

    We'll add logic to make the gun deal double damage if the last ammo was used.
    An easy way to add such logic is by inserting our own `CalculateDamage` method:

    ```cs
    target.TakeDamage(CalculateDamage(this, 4));
    ```

    :::tip
    It's almost always preferred to perform as much logic in C#
    as possible while minimizing the amount of IL instructions written.
    :::

    Let's take a look at the instructions to see how this could be done.
    What we are looking for is the `Entity::TakeDamage(int32)` call near the end.

    ```cil
    IL_0000: ldarg.0
    IL_0001: ldfld int32 Gun::_ammo
    IL_0006: ldc.i4.0
    IL_0007: bgt.s IL_000a

    IL_0009: ret

    IL_000a: ldarg.0
    IL_000b: ldarg.0
    IL_000c: ldfld int32 Gun::_ammo
    IL_0011: ldc.i4.1
    IL_0012: sub
    IL_0013: stfld int32 Gun::_ammo
    IL_0018: ldarg.1
    IL_0019: ldloca.s 0
    IL_001b: callvirt instance bool Entity::TryGetTarget(class Entity&)
    IL_0020: brtrue.s IL_0023

    IL_0022: ret

    IL_0023: ldloc.0
    IL_0024: ldc.i4.4
    IL_0025: callvirt instance void Entity::TakeDamage(int32)
    IL_002a: ret
    ```

    Let's get to writing the ILHook.
    */

    static void ILHook_OnUse(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        // As always, we perform a match and point Current to what we are looking for.
        ILWeaverResult result = w.MatchRelaxed(
            x => x.MatchLdloc(out _),
            x => x.MatchLdcI4(out _),
            x => x.MatchCallvirt<Entity>(nameof(Entity.TakeDamage)) && w.SetCurrentTo(x)
        );

        result.ThrowIfFailure();

        // We insert our instructions just before the Entity.TakeDamage method call.
        w.InsertBeforeCurrent(
            // Load arg 0, which is always 'this' for instance (non-static) methods
            w.Create(OpCodes.Ldarg_0),
            // Add a call to our method which consumes the base damage and 'this' from the stack.
            w.CreateDelegateCall(CalculateDamage)
        );

        // CalculateDamage returns int, so stack becomes [Entity, int] for
        // the Entity.TakeDamage method, which is correct.
    }

    static int CalculateDamage(int baseDamage, Gun self)
    {
        if (self._ammo == 0)
            return baseDamage * 2;
        else
            return baseDamage;
    }

    [Fact]
    public static void TestDoubleDamageLastAmmo()
    {
        PrepareAndValidateBehaviorForOnUse();

        // Apply our ILHook
        DefaultMonoDetourManager.Instance.ILHook(GetOnUseMethod(), ILHook_OnUse);

        var gun = new Gun(magazineSize: 2);
        var user = new Entity(health: 12);
        var target = new Entity(health: 12);

        user.SetTarget(target);

        gun.OnUse(user);
        Assert.Equal(8, target.Health);

        gun.OnUse(user);
        Assert.Equal(0, target.Health);

        gun.OnUse(user);
        Assert.Equal(0, target.Health);
    }

    private static void PrepareAndValidateBehaviorForOnUse()
    {
        // Manually write the instructions to ensure they are always consistent
        DefaultMonoDetourManager.Instance.ILHook(
            GetOnUseMethod(),
            ILHook_OnUse_WriteInstructions,
            new MonoDetourConfig(priority: 1)
        );

        var gun = new Gun(magazineSize: 2);
        var user = new Entity(health: 12);
        var target = new Entity(health: 12);

        user.SetTarget(target);

        gun.OnUse(user);
        Assert.Equal(8, target.Health);

        gun.OnUse(user);
        Assert.Equal(4, target.Health);

        gun.OnUse(user);
        Assert.Equal(4, target.Health);
    }

    private static MethodInfo GetOnUseMethod()
    {
        return typeof(Gun).GetMethod(nameof(Gun.OnUse))!;
    }

    static void ILHook_OnUse_WriteInstructions(ILManipulationInfo info)
    {
        ILWeaver w = new(info);

        ILLabel labelIsEnoughAmmo = w.DefineLabel();
        ILLabel labelHasTarget = w.DefineLabel();

        var entityVariable = w.DeclareVariable(typeof(Entity));

        w.InsertBeforeCurrent(
            w.Create(OpCodes.Ldarg_0),
            w.Create<Gun>(OpCodes.Ldfld, "_ammo"),
            w.Create(OpCodes.Ldc_I4, 0),
            w.Create(OpCodes.Bgt, labelIsEnoughAmmo),
            //
            w.Create(OpCodes.Ret),
            //
            w.Create(OpCodes.Ldarg_0).MarkLabel(labelIsEnoughAmmo),
            w.Create(OpCodes.Ldarg_0),
            w.Create<Gun>(OpCodes.Ldfld, "_ammo"),
            w.Create(OpCodes.Ldc_I4, 1),
            w.Create(OpCodes.Sub),
            w.Create<Gun>(OpCodes.Stfld, "_ammo"),
            w.Create(OpCodes.Ldarg, 1),
            w.Create(OpCodes.Ldloca, entityVariable),
            w.Create<Entity>(OpCodes.Callvirt, nameof(Entity.TryGetTarget)),
            w.Create(OpCodes.Brtrue, labelHasTarget),
            //
            w.Create(OpCodes.Ret),
            //
            w.Create(OpCodes.Ldloc, entityVariable).MarkLabel(labelHasTarget),
            w.Create(OpCodes.Ldc_I4, 4),
            w.Create<Entity>(OpCodes.Callvirt, nameof(Entity.TakeDamage)),
            w.Create(OpCodes.Ret)
        );
    }

#pragma warning disable CA1852 // class can be sealed
#pragma warning disable CS0649 // field is never assigned to
    class Gun(int magazineSize) : Item
    {
        internal int _ammo = magazineSize;

        public override void OnUse(Entity user)
        {
            /*
            if (_ammo <= 0)
                return;

            _ammo--;

            if (!user.TryGetTarget(out Entity? target))
            {
                return;
            }

            target.TakeDamage(4);
            */
        }
    }

    abstract class Item
    {
        public abstract void OnUse(Entity user);
    }

    class Entity(int health)
    {
        public int Health { get; private set; } = health;
        Entity? _target;

        public void SetTarget(Entity target) => _target = target;

        public bool TryGetTarget([NotNullWhen(true)] out Entity? target)
        {
            target = _target;
            return target is { };
        }

        public void TakeDamage(int damage)
        {
            Health -= damage;
            // ...
        }
    }
#pragma warning restore CS0649 // field is never assigned to
#pragma warning restore CA1852 // class can be sealed
}
