using System.Threading.Tasks;

namespace MonoDetour.UnitTests.RuntimeAsyncLib;

public static class RuntimeAsyncMethods
{
    public static async ValueTask<string> GatherAsync(string value)
    {
        await Task.Yield();
        return value;
    }

    public static async Task DoTaskAsync()
    {
        await Task.Yield();
    }

    public static async ValueTask DoValueTaskAsync()
    {
        await Task.Yield();
    }

    public static async Task<int> AddAsync(int a, int b)
    {
        await Task.Yield();
        return a + b;
    }
}
