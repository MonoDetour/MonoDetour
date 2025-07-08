using System.Collections;

namespace MonoDetour.UnitTests.TestLib;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
class ControlFlowLib
{
    public void SetStringToHello(ref string? message)
    {
        try
        {
            message = "hello";
        }
        finally { }
    }

    public string ReturnHello()
    {
        return "hello";
    }

    public IEnumerator GetEnumerator()
    {
        yield break;
    }
}
