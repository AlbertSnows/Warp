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
    public static RegionData Get(string region) => _loaded.Deref().TryGetValue(region, out RegionData regionData) ? regionData : Reload(region);

    /// <summary>Reads the region again, picking up any change to its files.</summary>
    public static RegionData Reload(string region)
    {
        RegionData regionData = Read(region);
        _loaded.Swap(current => new Dictionary<string, RegionData>(current) { [region] = regionData });
        WarpState.UpdatePalette(palette => palette.WithSubregionCount(region, regionData.SubregionNames.Length));
        WarpLog.Info(region + " read: " + regionData.Rooms.Length + " rooms");
        return regionData;
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
        Dictionary<string, int> subregionOfRoom = ReadSubregions(region, names);

        RoomInfo[] detailed = [.. rooms.Select(room => WithCamerasAndSubregion(room, subregionOfRoom))];
        return new RegionData(region, RoomSorting.Sort(detailed, Grouping.Type), names);
    }

    /// <summary>
    /// The subregion names from the region's properties file, or just the label for "none" if it has no such file.
    /// </summary>
    private static string[] ReadSubregionNames(string region)
    {
        string[] regionSettings = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/properties.txt"));
        return RegionFiles.ParseSubregionNames(regionSettings ?? [], Translator.Translate(RegionData.NoSubregion), SpaceAfterColon);
    }

    /// <summary>
    /// Which subregion each room is in, from the region's map file. Empty if the region has no map file.
    /// </summary>
    private static Dictionary<string, int> ReadSubregions(string region, string[] names)
    {
        string[] mapLayout = WarpFiles.TryReadLines(AssetManager.ResolveFilePath($"world/{region}/map_{region}.txt"));
        return RegionFiles.ParseMapSubregions(mapLayout ?? [], names, SpaceAfterColon);
    }

    /// <summary>
    /// The room with its camera count read from its own file (1 if it has none) and its subregion from the map, if the
    /// map lists it.
    /// </summary>
    private static RoomInfo WithCamerasAndSubregion(RoomInfo room, Dictionary<string, int> subregionOfRoom) => room with
    {
        Cameras = ReadCameras(room.Name) ?? 1,
        Subregion = subregionOfRoom.GetOr(room.Name, room.Subregion)
    };

    /// <summary>A room's camera count from its own file, or null if the room has no readable file.</summary>
    private static int? ReadCameras(string room)
    {
        string[] roomFileLines = WarpFiles.TryReadLines(RoomFiles.Find(room));
        return RegionFiles.CameraCount(roomFileLines ?? []);
    }

    /// <summary> Puts the space the files expect after a colon, using the game's own function. </summary>
    private static string SpaceAfterColon(string line) => Custom.ValidateSpacedDelimiter(line, ":");
}
