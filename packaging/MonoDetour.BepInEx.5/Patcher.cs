using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;

namespace MonoDetour.Logging;

internal static class Patcher
{
    internal static ManualLogSource Log = Logger.CreateLogSource("MonoDetour");

    public static void Initialize()
    {
        MonoDetourLogger.MessageReceived += LogHandler;
    }

    static void LogHandler(object sender, MonoDetourLogger.LogEventArgs e)
    {
        var logLevel = e.LogChannel switch
        {
            MonoDetourLogger.LogChannel.None => LogLevel.None,
            MonoDetourLogger.LogChannel.Debug => LogLevel.Debug,
            MonoDetourLogger.LogChannel.Info => LogLevel.Info,
            MonoDetourLogger.LogChannel.Warn => LogLevel.Warning,
            MonoDetourLogger.LogChannel.Error => LogLevel.Error,
            MonoDetourLogger.LogChannel.IL => LogLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(
                "A log can only have a single known channel."
            ),
        };

        Log.Log(logLevel, e.Message);
    }

    // Load us https://docs.bepinex.dev/articles/dev_guide/preloader_patchers.html
    public static IEnumerable<string> TargetDLLs { get; } = [];

    public static void Patch(AssemblyDefinition _) { }
}
