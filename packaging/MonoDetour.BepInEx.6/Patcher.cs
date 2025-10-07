using System;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Patching;
using static MonoDetour.Logging.MonoDetourLogger;

namespace MonoDetour.Logging;

[PatcherAutoPlugin]
internal partial class Patcher : BasePatcher
{
    internal static new ManualLogSource Log = Logger.CreateLogSource("MonoDetour");

    public override void Initialize()
    {
        OnLog += LogHandler;
    }

    static void LogHandler(LogChannel channel, string message)
    {
        var logLevel = channel switch
        {
            LogChannel.None => LogLevel.None,
            // MonoDetourLogger.LogChannel.Debug => LogLevel.Debug,
            // MonoDetourLogger.LogChannel.Info => LogLevel.Info,
            LogChannel.Warning => LogLevel.Warning,
            LogChannel.Error => LogLevel.Error,
            LogChannel.IL => LogLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(
                nameof(channel),
                "A log can only have a single known channel."
            ),
        };

        Log.Log(logLevel, message);
    }
}
