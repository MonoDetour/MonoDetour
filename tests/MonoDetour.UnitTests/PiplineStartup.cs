using Xunit.Sdk;
using Xunit.v3;

[assembly: TestPipelineStartup(typeof(PipelineStartup))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

sealed class PipelineStartup : ITestPipelineStartup
{
    public async ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        await Task.CompletedTask;
    }

    public async ValueTask StopAsync()
    {
        await Task.CompletedTask;
    }
}
