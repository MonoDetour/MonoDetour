using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using Mono.Cecil;
using static MonoDetour.Logging.MonoDetourLogger;

namespace MonoDetour.Logging;

internal static class Patcher
{
    internal static ManualLogSource Log = Logger.CreateLogSource("MonoDetour");

    public static void Initialize()
    {
        Environment.SetEnvironmentVariable(
            "MONODETOUR_MANUAL_INIT",
            "1",
            EnvironmentVariableTarget.Process
        );

        Init();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Init()
    {
        OnLog += LogHandler;
        ModuleInitialization.Initialize();
        Interop.HarmonyX.HarmonyXInterop.Initialize();
    }

    static void LogHandler(LogChannel channel, string message)
    {
        var logLevel = channel switch
        {
            LogChannel.None => LogLevel.None,
            // LogChannel.Debug => LogLevel.Debug,
            LogChannel.Info => LogLevel.Info,
            LogChannel.Warning => LogLevel.Warning,
            LogChannel.Error => LogLevel.Error,
            LogChannel.IL => LogLevel.Info,
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
