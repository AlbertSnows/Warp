using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Warp.Util;

namespace Warp.Game;

/// <summary> Asks the operating system to show a file or a folder, and logs it if that fails. </summary>
internal static class Shell
{
    /// <summary> Opens a file with whatever program the system uses for it. A failure is logged, not thrown. </summary>
    public static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception error)
        {
            WarpLog.Error("Failed to open " + path + ": " + error.Message);
        }
    }

    /// <summary>
    /// Opens a folder in the system's file browser, making it first so there is something to open. A failure is
    /// logged, not thrown.
    /// </summary>
    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Application.OpenURL(path);
        }
        catch (Exception error)
        {
            WarpLog.Error("Failed to open " + path + ": " + error.Message);
        }
    }
}
