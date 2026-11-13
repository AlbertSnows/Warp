using System.Collections.Generic;
using System.Linq;
using RWCustom;
using Warp.Model;
using Warp.Util;

namespace Warp.Game;

/// <summary>
/// Reads regions' room lists from the game's files and remembers them. Reading is slow enough to notice (one file
/// per room), so a region is read once and kept until the player asks for it to be read again.
/// </summary>
internal static class RegionCatalog
{
    private static readonly Atom<Dictionary<string, RegionData>> _loaded = new(new Dictionary<string, RegionData>());

    /// <summary>Whether the region has been read already.</summary>
    public static bool IsLoaded(string region) => _loaded.Deref().ContainsKey(region);

    /// <summary>The regions read so far, in no particular order.</summary>
    public static IReadOnlyDictionary<string, RegionData> All => _loaded.Deref();

    /// <summary>The region's data, read from the game's files the first time it is asked for.</summary>
    public static RegionData Get(string region) => _loaded.Deref().TryGetValue(region, out RegionData data) ? data : Reload(region);

    /// <summary>Reads the region again, picking up any change to its files.</summary>
    public static RegionData Reload(string region)
    {
        RegionData data = Read(region);
        _loaded.Swap(current => new Dictionary<string, RegionData>(current) { [region] = data });
        WarpState.UpdatePalette(palette => palette.WithSubregionCount(region, data.SubregionNames.Length));
        WarpLog.Info(region + " read: " + data.Rooms.Length + " rooms");
        return data;
    }

    /// <summary>
    /// Region information can be in several places: the vanilla world folders, the folders of Downpour and other mods,
    /// the merged mods folder and workshop folders. The asset manager looks in all of them for a given file and
    /// returns the most up to date one. The "-rooms" folder is not searched directly, because for a vanilla region
    /// that a mod changed, the merged folder holds only the changed files; instead the world file gives the room names
    /// and each room file is looked up on its own.
    /// </summary>
    private static RegionData Read(string region)
    {
        string[] worldLines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/world_{region}.txt"));
        string[] spoilerLines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/spoilers.txt"));
        HashSet<string> spoilers = RegionFiles.ParseSpoilers(spoilerLines ?? []);
        List<RoomInfo> rooms = RegionFiles.ParseWorldFile(worldLines ?? [], region, spoilers);

        string[] names = ReadSubregionNames(region);
        Dictionary<string, int> subregions = ReadSubregions(region, names);

        RoomInfo[] finished = [.. rooms.Select(room => WithDetails(room, region, subregions))];
        return new RegionData(region, RoomSorting.Sort(finished, Grouping.Type), names);
    }

    /// <summary>
    /// The subregion names from the region's properties file, or just the label for "none" if it has no such file.
    /// </summary>
    private static string[] ReadSubregionNames(string region)
    {
        string[] lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/properties.txt"));
        return lines == null ? [RegionData.NoSubregion] : RegionFiles.ParseSubregionNames(lines, Translator.Translate("None"), SpaceAfterColon);
    }

    /// <summary>
    /// Which subregion each room is in, from the region's map file. Empty if the region has no map file.
    /// </summary>
    private static Dictionary<string, int> ReadSubregions(string region, string[] names)
    {
        string[] lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/map_{region}.txt"));
        return lines == null
            ? []
            : RegionFiles.ParseMapSubregions(lines, names, SpaceAfterColon);
    }

    /// <summary> The room with its camera count and subregion filled in from the files that describe them. </summary>
    private static RoomInfo WithDetails(RoomInfo room, string region, Dictionary<string, int> subregions)
    {
        RoomInfo withCameras = room with
        {
            Cameras = ReadCameras(room.Name, region) ?? 1
        };
        return subregions.TryGetValue(room.Name, out int subregion)
            ? withCameras with
            {
                Subregion = subregion
            }
            : withCameras;
    }

    /// <summary>
    /// A room's camera count from its own file, which is a room of the region, a standalone level or a gate.
    /// </summary>
    private static int? ReadCameras(string room, string region)
    {
        string[] paths =
        [
            $"world/{region}-rooms/{room}.txt", $"levels/{room}.txt", $"world/gates/{room}.txt"
        ];
        foreach (string path in paths)
        {
            string[] lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath(path));
            if (lines != null)
            {
                return RegionFiles.CameraCount(lines);
            }
        }
        return null;
    }

    /// <summary> Puts the space the files expect after a colon, using the game's own function. </summary>
    private static string SpaceAfterColon(string line) => Custom.ValidateSpacedDelimiter(line, ":");
}
