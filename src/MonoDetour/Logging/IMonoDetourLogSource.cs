namespace MonoDetour.Logging;

internal interface IMonoDetourLogSource
{
    /// <summary>
    /// Filter for which channels this log source logs to.
    /// </summary>
    public MonoDetourLogger.LogChannel LogFilter { get; set; }
}
