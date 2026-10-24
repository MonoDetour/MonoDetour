using System;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Patching;

namespace MonoDetour.Logging;

[PatcherAutoPlugin]
internal partial class Patcher : BasePatcher
{
    internal static new ManualLogSource Log = Logger.CreateLogSource("MonoDetour");

    public override void Initialize()
    {
        MonoDetourLogger.LogToConsole = false;
        MonoDetourLogger.OnLog += LogHandler;
        Interop.HarmonyX.HarmonyXInterop.Initialize();
    }

    static void LogHandler(MonoDetourLogger.LogChannel channel, string message)
    {
        var logLevel = channel switch
        {
            MonoDetourLogger.LogChannel.None => LogLevel.None,
            MonoDetourLogger.LogChannel.Debug => LogLevel.Debug,
            MonoDetourLogger.LogChannel.Info => LogLevel.Info,
            MonoDetourLogger.LogChannel.Warning => LogLevel.Warning,
            MonoDetourLogger.LogChannel.Error => LogLevel.Error,
            MonoDetourLogger.LogChannel.IL => LogLevel.Info,
            _ => throw new ArgumentOutOfRangeException(
                nameof(channel),
                "A log can only have a single known channel."
            ),
        };

        Log.Log(logLevel, message);
    }
}
