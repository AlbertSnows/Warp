using System;
using BepInEx.Logging;
using UnityEngine;

namespace Warp.Util;

/// <summary>
/// The plugin's messages, written through BepInEx's log so they end up in BepInEx/LogOutput.log, which is the file people
/// paste into a bug report. The log source's name tags each line, so the messages carry no prefix of their own. Before
/// <see cref="Initialize"/> has been called, and so only for a fault that happens that early, they go to Unity's log with
/// a "WARP:" prefix.
/// </summary>
internal static class WarpLog
{
    private static ManualLogSource _source;

    /// <summary> Sets the log source the messages go to. Call it once, when the plugin starts. </summary>
    public static void Initialize(ManualLogSource source) => _source = source;

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Error(string message) => Write(LogLevel.Error, message);

    /// <summary> Logs an exception, with its stack trace, after a line that says what was being done. </summary>
    public static void Exception(string whileDoing, Exception error)
    {
        Error("Error while " + whileDoing + ":");
        Write(LogLevel.Error, error.ToString());
    }

    /// <summary> Sends a message to the log source, or to Unity's log if there is none yet. </summary>
    private static void Write(LogLevel level, string message)
    {
        if (_source != null)
        {
            _source.Log(level, message);
        }
        else if (level == LogLevel.Error)
        {
            Debug.LogError("WARP: " + message);
        }
        else
        {
            Debug.Log("WARP: " + message);
        }
    }
}
