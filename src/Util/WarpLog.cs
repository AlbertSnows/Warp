using System;
using UnityEngine;

namespace Warp.Util;

/// <summary>
/// The plugin's messages in Unity's log (and so in BepInEx's LogOutput.log). Every line starts with "WARP:" so it is
/// easy to find.
/// </summary>
internal static class WarpLog
{
    private const string Prefix = "WARP: ";

    public static void Info(string message) => Debug.Log(Prefix + message);

    public static void Error(string message) => Debug.LogError(Prefix + message);

    /// <summary>Logs an exception with a line that says what was being done.</summary>
    public static void Exception(string whileDoing, Exception error)
    {
        Debug.Log(Prefix + "Error while " + whileDoing + ":");
        Debug.LogException(error);
    }
}
