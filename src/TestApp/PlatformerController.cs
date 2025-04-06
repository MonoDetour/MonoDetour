using System;
using System.Collections;

public class PlatformerController
{
    int num = 0;
    bool bar = false;

    public void SpinBounce(float power)
    {
        Console.WriteLine("power is: " + power);
        num += 1;
        if (power >= 5)
            return;

        bar = true;
        // var x = new On.PlatformerController.SpinBounce.Params();
        // x.power = ref power2;
        // PlatformerControllerPatches.MyPatch2(ref x);

        Console.WriteLine("continuing...");

        // var x = 5 * 2;
    }

    public IEnumerator DoStuff()
    {
        Console.WriteLine("Starting to do stuff");

        yield return null;

        Console.WriteLine("Stuff done.");
    }

    public void Foo()
    {
        Console.WriteLine("Foo'd");

        return;
    }
}
