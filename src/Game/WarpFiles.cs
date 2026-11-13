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
    /// <summary> Where Warp keeps its files: a Warp folder in the game's data folder. </summary>
    private static string Folder => Path.Combine(Application.persistentDataPath, "Warp");

    /// <summary> Where exported room pictures go, in a folder for each region. </summary>
    public static string ExportFolder => Path.Combine(Folder, "Export");

    private static string SettingsPath => Path.Combine(Folder, "Settings.txt");

    private static string FavouritesPath => Path.Combine(Folder, "Favourites.txt");

    private static string ColorsPath => Path.Combine(Folder, "Colors.txt");

    /// <summary> The file the F5 and F6 room list is written to. </summary>
    private static string RoomListPath => Path.Combine(Folder, "RoomListExport.txt");

    /// <summary>The folder the previews of one region are exported to.</summary>
    public static string ExportFolderOf(string region) => Path.Combine(ExportFolder, region);

    /// <summary>
    /// The saved preferences. A missing or unreadable file gives the defaults and is written again, so the next
    /// start finds a good one.
    /// </summary>
    public static Preferences LoadPreferences()
    {
        string text = TryRead(SettingsPath);
        Preferences preferences = text == null ? null : Preferences.Parse(text);
        if (preferences != null)
        {
            return preferences;
        }

        preferences = new Preferences();
        SavePreferences(preferences);
        return preferences;
    }

    /// <summary> Writes the preferences to Settings.txt. </summary>
    public static void SavePreferences(Preferences preferences) => TryWrite(SettingsPath, preferences.Serialize());

    /// <summary> The saved favourite regions, or none if there is no file. </summary>
    public static HashSet<string> LoadFavourites()
    {
        string text = TryRead(FavouritesPath);
        return text == null ? [] : FavouriteSet.Parse(text);
    }

    /// <summary> Writes the favourite regions to Favourites.txt. </summary>
    public static void SaveFavourites(IEnumerable<string> favourites)
        => TryWrite(FavouritesPath, FavouriteSet.Serialize(favourites));

    /// <summary>
    /// The saved colors. With no file yet, the colors that come with Warp are written out and used. A line that
    /// cannot be read is logged and its color left as it was.
    /// </summary>
    public static ColorPalette LoadPalette()
    {
        string text = TryRead(ColorsPath);
        if (text == null)
        {
            text = EmbeddedText.Read("Warp.Resources.Colors.txt");
            TryWrite(ColorsPath, text);
            WarpLog.Info("Wrote the default colors");
        }
        return ColorFile.Parse(text.Split('\n'), ColorPalette.Default,
            line => WarpLog.Error("Could not read a color from the colors file: " + line));
    }

    /// <summary> Writes the palette to Colors.txt. </summary>
    public static void SavePalette(ColorPalette palette)
    {
        TryWrite(ColorsPath, ColorFile.Serialize(palette));
        WarpLog.Info("Custom colors saved");
    }

    /// <summary> Writes the room list text where F5 and F6 put it. Returns the path, or null if it could not be written. </summary>
    public static string WriteRoomList(string text) => TryWrite(RoomListPath, text) ? RoomListPath : null;

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

    /// <summary> The text of a file, or null if it is missing or cannot be read. A failure is logged. </summary>
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

    /// <summary>
    /// Writes a text file, making its folder first. Returns whether it worked. A failure is logged and not thrown, so a
    /// read-only folder cannot break the menu.
    /// </summary>
    private static bool TryWrite(string path, string text)
    {
        try
        {
            string folder = Path.GetDirectoryName(path);
            if (folder != null)
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(path, text);
            return true;
        }
        catch (Exception error)
        {
            WarpLog.Exception("writing " + path, error);
            return false;
        }
    }
}
