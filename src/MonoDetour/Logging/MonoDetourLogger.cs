using System;

namespace MonoDetour.Logging;

/// <summary>
/// Main logger class that exposes log events.
/// </summary>
public static class MonoDetourLogger
{
    /// <summary>
    /// A single log event that represents a single log message.
    /// </summary>
    public class LogEventArgs(LogChannel logChannel, string message) : EventArgs
    {
        /// <summary>
        /// Log channel of the message.
        /// </summary>
        public LogChannel LogChannel { get; internal set; } = logChannel;

        /// <summary>
        /// The log message.
        /// </summary>
        public string Message { get; internal set; } = message;
    }

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
        /// Full IL dumps of the generated dynamic methods.
        /// </summary>
        IL = 1 << 2,

        /// <summary>
        /// Channel for warnings.
        /// </summary>
        Warn = 1 << 3,

        /// <summary>
        /// Channel for errors.
        /// </summary>
        Error = 1 << 4,

        /// <summary>
        /// Additional debug information that is related to patching.
        /// </summary>
        Debug = 1 << 5,

        /// <summary>
        /// All channels.
        /// </summary>
        All = Info | IL | Warn | Error | Debug,
    }

    static string LogChannelToString(LogChannel channel) =>
        channel switch
        {
            LogChannel.None => "None   ",
            LogChannel.Info => "Info   ",
            LogChannel.IL => "IL     ",
            LogChannel.Warn => "Warning",
            LogChannel.Error => "Error  ",
            LogChannel.Debug => "Debug  ",
            _ => throw new NotSupportedException(),
        };

    // TODO: Maybe some kind of global override could be good?
    // /// <summary>
    // /// Filter for which channels should be listened to.
    // /// If the channel is in the filter, all log messages from that
    // /// channel get propagated into <see cref="MessageReceived"/> event.
    // /// </summary>
    // public static LogChannel GlobalFilter { get; set; } = LogChannel.Error;

    /// <summary>
    /// Event fired on any incoming message that passes the channel filter.
    /// </summary>
    public static event EventHandler<LogEventArgs>? MessageReceived;

    internal static bool IsEnabledFor(LogChannel caller, LogChannel channel)
    {
        return (caller & channel) != LogChannel.None;
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

        if (MessageReceived is null)
        {
            DefaultLog(channel, message());
            return;
        }

        MessageReceived?.Invoke(null, new LogEventArgs(channel, message()));
    }

    internal static void LogText(this MonoDetourManager caller, LogChannel channel, string message)
    {
        if (!IsEnabledFor(caller.LogFilter, channel))
            return;

        if (MessageReceived is null)
        {
            DefaultLog(channel, message);
            return;
        }

        MessageReceived?.Invoke(null, new LogEventArgs(channel, message));
    }

    static void DefaultLog(LogChannel channel, string message)
    {
        ConsoleColor color = channel switch
        {
            LogChannel.Warn => ConsoleColor.Yellow,
            LogChannel.Error => ConsoleColor.Red,
            _ => Console.ForegroundColor,
        };

        LogWithColor($"[{LogChannelToString(channel)}: MonoDetour] {message}", color);
    }

    static void LogWithColor(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}
