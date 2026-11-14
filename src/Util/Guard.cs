using System;

namespace Warp.Util;

/// <summary>
/// Keeps a fault in Warp from reaching the game. The hooks call into Warp from places the game runs every frame, so an
/// exception there would repeat every frame, or stop the pause menu from opening. A fault is logged the first time it
/// happens in a place, and the game carries on.
/// A file of its own because every hook uses it, and it is the one place that decides to report a fault once for each
/// place it happens.
/// </summary>
internal static class Guard
{
    private static readonly Once _reported = new();

    /// <summary>
    /// Logs the exception, but only the first time something with this description fails. Call it from a catch block
    /// around the Warp part of a hook, never around the game's own original method.
    /// </summary>
    public static void Report(string whileDoing, Exception error)
    {
        if (_reported.First(whileDoing))
        {
            WarpLog.Exception(whileDoing, error);
        }
    }
}
