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

    // Load us https://docs.bepinex.dev/articles/dev_guide/preloader_patchers.html
    public static IEnumerable<string> TargetDLLs { get; } = [];

    public static void Patch(AssemblyDefinition _) { }
}
