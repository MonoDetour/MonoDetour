using Xunit.Sdk;
using Xunit.v3;

[assembly: TestPipelineStartup(typeof(PipelineStartup))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

class PipelineStartup : ITestPipelineStartup
{
    public async ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        await Task.CompletedTask;
        MonoDetourLogger.ChannelFilter = MonoDetourLogger.LogChannel.None;
    }

    public async ValueTask StopAsync()
    {
        await Task.CompletedTask;
    }
}
