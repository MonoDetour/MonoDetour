using System;

namespace SomeNamespace;

public class SomeType
{
    public void SomeMethod(int number)
    {
        Console.WriteLine("Doing stuff.");

        if (number > 0)
            return;

        Console.WriteLine("Doing more stuff.");
    }
}
