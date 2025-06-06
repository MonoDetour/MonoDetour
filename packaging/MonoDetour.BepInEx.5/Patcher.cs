using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using static MonoDetour.Logging.MonoDetourLogger;

namespace MonoDetour.Logging;

internal static class Patcher
{
    internal static ManualLogSource Log = Logger.CreateLogSource("MonoDetour");

    public static void Initialize()
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
                "A log can only have a single known channel."
            ),
        };

        Log.Log(logLevel, message);
    }

    // Load us https://docs.bepinex.dev/articles/dev_guide/preloader_patchers.html
    public static IEnumerable<string> TargetDLLs { get; } = [];

    public static void Patch(AssemblyDefinition _) { }
}
