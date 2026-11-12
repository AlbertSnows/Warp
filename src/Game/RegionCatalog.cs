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
    private static readonly Atom<Dictionary<string, RegionData>> Loaded = new(new Dictionary<string, RegionData>());

    /// <summary>Whether the region has been read already.</summary>
    public static bool IsLoaded(string region) => Loaded.Deref().ContainsKey(region);

    /// <summary>The regions read so far, in no particular order.</summary>
    public static IReadOnlyDictionary<string, RegionData> All => Loaded.Deref();

    /// <summary>The region's data, read from the game's files the first time it is asked for.</summary>
    public static RegionData Get(string region) => Loaded.Deref().TryGetValue(region, out var data) ? data : Reload(region);

    /// <summary>Reads the region again, picking up any change to its files.</summary>
    public static RegionData Reload(string region)
    {
        var data = Read(region);
        Loaded.Swap(current => new Dictionary<string, RegionData>(current) { [region] = data });
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
        var worldLines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/world_{region}.txt"));
        var spoilerLines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/spoilers.txt"));
        var spoilers = RegionFiles.ParseSpoilers(spoilerLines ?? new string[0]);
        var rooms = RegionFiles.ParseWorldFile(worldLines ?? new string[0], region, spoilers);

        var names = ReadSubregionNames(region);
        var subregions = ReadSubregions(region, names);

        var finished = rooms.Select(room => WithDetails(room, region, subregions)).ToArray();
        return new RegionData(region, RoomSorting.Sort(finished, Grouping.Type), names);
    }

    private static string[] ReadSubregionNames(string region)
    {
        var lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/properties.txt"));
        if (lines == null)
        {
            return new[] { RoomInfo.NoSubregion };
        }
        return RegionFiles.ParseSubregionNames(lines, Translator.Translate("None"), SpaceAfterColon);
    }

    private static Dictionary<string, (int Index, string Name)> ReadSubregions(string region, string[] names)
    {
        var lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/map_{region}.txt"));
        return lines == null
            ? new Dictionary<string, (int, string)>()
            : RegionFiles.ParseMapSubregions(lines, names, SpaceAfterColon);
    }

    private static RoomInfo WithDetails(RoomInfo room, string region, Dictionary<string, (int Index, string Name)> subregions)
    {
        var withCameras = room with
        {
            Cameras = ReadCameras(room.Name, region) ?? 1
        };
        return subregions.TryGetValue(room.Name, out var subregion)
            ? withCameras with
            {
                Subregion = subregion.Index,
                SubregionName = subregion.Name
            }
            : withCameras;
    }

    /// <summary>
    /// A room's camera count from its own file, which is a room of the region, a standalone level or a gate.
    /// </summary>
    private static int? ReadCameras(string room, string region)
    {
        var paths = new[]
        {
            $"world/{region}-rooms/{room}.txt", $"levels/{room}.txt", $"world/gates/{room}.txt"
        };
        foreach (var path in paths)
        {
            var lines = WarpFiles.TryReadLines(AssetManager.ResolveFilePath(path));
            if (lines != null)
            {
                return RegionFiles.CameraCount(lines);
            }
        }
        return null;
    }

    private static string SpaceAfterColon(string line) => Custom.ValidateSpacedDelimiter(line, ":");
}
