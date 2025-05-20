namespace MonoDetour.UnitTests.TestLib;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
class ControlFlowLib
{
    public void SetStringToHello(ref string? message)
    {
        message = "hello";
    }
}
