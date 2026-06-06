using System;
using System.Runtime.CompilerServices;
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
}
