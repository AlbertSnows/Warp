using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.Game;

/// <summary>
/// The files Warp keeps in the game's data folder (&lt;persistentDataPath&gt;/Warp) and how to read and write them.
/// What the files contain is decided in Model; this class only moves the text to and from the disk.
/// </summary>
internal static class WarpFiles
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "Warp");

    public static string ExportFolder => Path.Combine(Folder, "Export");

    private static string SettingsPath => Path.Combine(Folder, "Settings.txt");

    private static string FavouritesPath => Path.Combine(Folder, "Favourites.txt");

    private static string ColorsPath => Path.Combine(Folder, "Colors.txt");

    public static string RoomListPath => Path.Combine(Folder, "RoomListExport.txt");

    /// <summary>The folder the previews of one region are exported to.</summary>
    public static string ExportFolderOf(string region) => Path.Combine(ExportFolder, region);

    /// <summary>
    /// The saved preferences. A missing or unreadable file gives the defaults and is written again, so the next
    /// start finds a good one.
    /// </summary>
    public static Preferences LoadPreferences()
    {
        var text = TryRead(SettingsPath);
        var preferences = text == null ? null : Preferences.Parse(text);
        if (preferences == null)
        {
            preferences = new Preferences();
            SavePreferences(preferences);
        }
        return preferences;
    }

    public static void SavePreferences(Preferences preferences) => TryWrite(SettingsPath, preferences.Serialize());

    public static HashSet<string> LoadFavourites()
    {
        var text = TryRead(FavouritesPath);
        return text == null ? new HashSet<string>() : Favourites.Parse(text);
    }

    public static void SaveFavourites(IEnumerable<string> favourites)
    {
        TryWrite(FavouritesPath, Favourites.Serialize(favourites));
    }

    /// <summary>
    /// The saved colors. With no file yet, the colors that come with Warp are written out and used. A line that
    /// cannot be read is logged and its color left as it was.
    /// </summary>
    public static ColorPalette LoadPalette()
    {
        var text = TryRead(ColorsPath);
        if (text == null)
        {
            text = Resources.Colors;
            TryWrite(ColorsPath, text);
            WarpLog.Info("Wrote the default colors");
        }
        return ColorFile.Parse(text.Split('\n'), ColorPalette.Default,
            line => WarpLog.Error("Could not read a color from the colors file: " + line));
    }

    public static void SavePalette(ColorPalette palette)
    {
        TryWrite(ColorsPath, ColorFile.Serialize(palette));
        WarpLog.Info("Custom colors saved");
    }

    public static void DeleteColors()
    {
        try
        {
            File.Delete(ColorsPath);
        }
        catch (Exception error)
        {
            WarpLog.Exception("deleting the colors file", error);
        }
    }

    /// <summary>Writes the room list text where F5 and F6 put it, and returns the path.</summary>
    public static string WriteRoomList(string text)
    {
        TryWrite(RoomListPath, text);
        return RoomListPath;
    }

    /// <summary>The lines of a file, or null if it is missing or cannot be read.</summary>
    public static string[] TryReadLines(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path) : null;
        }
        catch (Exception error)
        {
            WarpLog.Exception("reading " + path, error);
            return null;
        }
    }

    private static string TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception error)
        {
            WarpLog.Exception("reading " + path, error);
            return null;
        }
    }

    private static void TryWrite(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
        catch (Exception error)
        {
            WarpLog.Exception("writing " + path, error);
        }
    }
}
