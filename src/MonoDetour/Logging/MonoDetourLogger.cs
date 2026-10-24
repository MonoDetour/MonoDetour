using System;
using MonoDetour.Bindings.Reorg;

namespace MonoDetour.Logging;

/// <summary>
/// Main logger class that exposes log events.
/// </summary>
public static class MonoDetourLogger
{
    /// <summary>
    /// A method which can receive log messages.
    /// </summary>
    /// <param name="channel">Log channel of the message.</param>
    /// <param name="message">The log message.</param>
    public delegate void LogReceiver(LogChannel channel, string message);

    /// <summary>
    /// Log channel for the messages.
    /// </summary>
    [Flags]
    public enum LogChannel
    {
        /// <summary>
        /// No channels (or an empty channel).
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic information.
        /// </summary>
        Info = 1 << 1,

        /// <summary>
        /// Full IL dumps of manipulated methods.
        /// </summary>
        IL = 1 << 2,

        /// <summary>
        /// Channel for warnings.
        /// </summary>
        Warning = 1 << 3,

        /// <summary>
        /// Channel for errors.
        /// </summary>
        Error = 1 << 4,

        /// <summary>
        /// Additional debug information that is related to hooking.
        /// </summary>
        /// <remarks>
        /// This is currently unused.
        /// </remarks>
        Debug = 1 << 5,

        // /// <summary>
        // /// All channels.
        // /// </summary>
        // All = Info | IL | Warning | Error | Debug,
    }

    static string LogChannelToString(LogChannel channel) =>
        channel switch
        {
            LogChannel.None => "None   ",
            LogChannel.Info => "Info   ",
            LogChannel.IL => "IL     ",
            LogChannel.Warning => "Warning",
            LogChannel.Error => "Error  ",
            // LogChannel.Debug => "Debug  ",
            _ => throw new NotSupportedException(),
        };

    /// <summary>
    /// Filter for which channels should be listened to.
    /// If the channel is in the filter, all log messages from that
    /// channel get propagated into <see cref="OnLog"/> event.
    /// </summary>
    private static LogChannel GlobalFilter { get; set; } = LogChannel.Warning | LogChannel.Error;

    static LogChannel OverrideFilter { get; set; }

    static bool parsedEnvironmentVariables;

    /// <summary>
    /// Event fired on any incoming message that passes the channel filter.
    /// </summary>
    public static event LogReceiver? OnLog;

    /// <summary>
    /// Whether or not MonoDetour will log to console. Set this to <see langword="false"/>
    /// if you are adding a "native" logger via <see cref="OnLog"/>.
    /// </summary>
    public static bool LogToConsole { get; set; } = true;

    static void ParseEnvironmentVariablesIfNeeded()
    {
        if (parsedEnvironmentVariables)
            return;

        parsedEnvironmentVariables = true;

        var variable = "MONODETOUR_LOG_OVERRIDE";
        var value = Environment.GetEnvironmentVariable(variable);
        if (value is { })
        {
            var entries = value.Split(':');
            foreach (var entry in entries)
            {
                if (!Enum.TryParse<LogChannel>(entry, ignoreCase: true, out var result))
                {
                    Log(
                        LogChannel.Error,
                        $"Failed to parse value '{entry}' in '{value}' from environment variable '{variable}'"
                    );
                    continue;
                }

                OverrideFilter |= result;
            }
        }
    }

    internal static bool IsEnabledFor(LogChannel caller, LogChannel channel)
    {
        ParseEnvironmentVariablesIfNeeded();
        return ((caller | OverrideFilter) & channel) != LogChannel.None;
    }

    internal static void Log(
        this MonoDetourManager caller,
        LogChannel channel,
        Func<string> message
    )
    {
        if (!IsEnabledFor(caller.LogFilter, channel))
        {
            return;
        }

        var messageStr = $"[{caller.Id}] {message()}";

        if (LogToConsole)
        {
            DefaultLog(channel, messageStr);
        }

        OnLog?.Invoke(channel, messageStr);
    }

    internal static void Log(
        this IMonoDetourLogSource caller,
        LogChannel channel,
        Func<string> message
    )
    {
        if (!IsEnabledFor(caller.LogFilter, channel))
        {
            return;
        }

        if (LogToConsole)
        {
            DefaultLog(channel, message());
        }

        OnLog?.Invoke(channel, message());
    }

    internal static void Log(LogChannel channel, Func<string> message)
    {
        if (!IsEnabledFor(GlobalFilter, channel))
        {
            return;
        }

        if (LogToConsole)
        {
            DefaultLog(channel, message());
        }

        OnLog?.Invoke(channel, message());
    }

    internal static void Log(this MonoDetourManager caller, LogChannel channel, string message)
    {
        if (!IsEnabledFor(caller.LogFilter, channel))
            return;

        message = $"[{caller.Id}] {message}";

        if (LogToConsole)
        {
            DefaultLog(channel, message);
        }

        OnLog?.Invoke(channel, message);
    }

    internal static void Log(this IMonoDetourLogSource caller, LogChannel channel, string message)
    {
        if (!IsEnabledFor(caller.LogFilter, channel))
            return;

        if (LogToConsole)
        {
            DefaultLog(channel, message);
        }

        OnLog?.Invoke(channel, message);
    }

    internal static void Log(LogChannel channel, string message)
    {
        if (!IsEnabledFor(GlobalFilter, channel))
            return;

        if (LogToConsole)
        {
            DefaultLog(channel, message);
        }

        OnLog?.Invoke(channel, message);
    }

    static void DefaultLog(LogChannel channel, string message)
    {
        LogWithChannel($"[{LogChannelToString(channel)}: MonoDetour] {message}", channel);
    }

    static void LogWithChannel(string message, LogChannel channel)
    {
        ConsoleColor color = channel switch
        {
            LogChannel.Warning => ConsoleColor.Yellow,
            LogChannel.Error => ConsoleColor.Red,
            _ => Console.ForegroundColor,
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;

        // Console.Error is not read by BepInEx 5,
        // and we likely have that if we have legacy MonoMod.
        if (channel is LogChannel.Error && MonoModVersion.IsReorg)
            Console.Error.WriteLine(message);
        else
            Console.Out.WriteLine(message);

        Console.ForegroundColor = originalColor;
    }
}
