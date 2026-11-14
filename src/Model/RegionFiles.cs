#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>
/// Reads the text files a region is made of. Each function takes the lines of a file and returns what is in them, so
/// none of them touch the disk or the game. A caller that wants to skip the disk entirely, a test for one, passes
/// lines straight in.
/// </summary>
internal static class RegionFiles
{
    private const string RoomsStart = "ROOMS";
    private const string RoomsEnd = "END ROOMS";
    private const string GatePrefix = "GATE_";

    /// <summary>
    /// The rooms listed in a world file (world_XX.txt), in file order, each once. Room lines sit between "ROOMS" and
    /// "END ROOMS" and look like "SU_A01 : SU_B02 : SHELTER". A line starting with a space or a slash is skipped.
    /// A room name may start with a condition in braces or parentheses, such as "(White)SU_A01", and the condition
    /// is dropped. Rooms of another region are ignored, except gates. Rooms named in <paramref name="spoilers"/> are
    /// left out.
    /// </summary>
    public static List<RoomInfo> ParseWorldFile(IEnumerable<string> lines, string region, ISet<string> spoilers)
    {
        var rooms = new List<RoomInfo>();
        var seen = new HashSet<string>();
        bool inRooms = false;
        foreach (string line in lines)
        {
            if (line == RoomsEnd)
            {
                break;
            }
            if (inRooms && line.Length > 0 && line[0] != ' ' && line[0] != '/')
            {
                RoomInfo? room = ParseRoomLine(line, region);
                if (room != null && seen.Add(room.Name) && !spoilers.Contains(room.Name))
                {
                    rooms.Add(room);
                }
            }
            if (line == RoomsStart)
            {
                inRooms = true;
            }
        }
        return rooms;
    }

    /// <summary> The room on one line of a world file, or null if the line is for a room of another region. </summary>
    private static RoomInfo? ParseRoomLine(string line, string region)
    {
        string[] parts = line.Split([" : "], StringSplitOptions.None);
        string name = StripCondition(parts[0]);
        // The name has to start with the region's prefix, so region SU does not take a room of region LSU
        if (!name.StartsWith(region + "_", StringComparison.Ordinal) && !name.StartsWith(GatePrefix, StringComparison.Ordinal))
        {
            return null;
        }
        return new RoomInfo(name, parts.Length > 2 ? RoomTypes.FromWorldTag(parts[2]) : RoomType.Room);
    }

    /// <summary>
    /// A room name without the conditions in front of it: "{Spear}SU_A01", "(White)SU_A01" and "{Spear}(White)SU_A01"
    /// all give "SU_A01". Only groups at the start are conditions, and a group that is never closed is left alone.
    /// </summary>
    internal static string StripCondition(string name)
    {
        string rest = name;
        while (rest.Length > 0 && (rest[0] == '{' || rest[0] == '('))
        {
            int end = rest.IndexOf(rest[0] == '{' ? '}' : ')');
            if (end < 0)
            {
                break;
            }
            rest = rest.Substring(end + 1);
        }
        return rest;
    }

    /// <summary>
    /// The room names in a spoilers file. Lines that start with "//" are comments.
    /// </summary>
    public static HashSet<string> ParseSpoilers(IEnumerable<string> lines)
        => [.. lines.Where(line => !line.StartsWith("//", StringComparison.Ordinal)).Select(line => line.Trim())];

    /// <summary>
    /// How many screens a room has, from the lines of its room file. The camera positions are on the fourth line,
    /// separated by "|". Null when the file is too short to have them.
    /// </summary>
    public static int? CameraCount(IReadOnlyList<string> roomFileLines)
    {
        const int cameraLine = 3;
        if (roomFileLines.Count <= cameraLine)
        {
            return null;
        }
        return roomFileLines[cameraLine].Split('|').Length;
    }

    /// <summary>
    /// The subregion names from a region's properties file, with <paramref name="noneLabel"/> in front as the name
    /// of subregion 0. Subregions are lines like "Subregion: Filtration System". The game accepts a missing space
    /// after the colon, and <paramref name="spaceAfterColon"/> puts the space in before the line is split.
    /// </summary>
    public static string[] ParseSubregionNames(
        IEnumerable<string> lines, string noneLabel, Func<string, string> spaceAfterColon)
    {
        IEnumerable<string> names = lines
            .Where(line => line.StartsWith("Subregion", StringComparison.Ordinal))
            .Select(line => SplitField(spaceAfterColon(line)))
            .Where(fields => fields.Length > 1)
            .Select(fields => fields[1]);
        return [noneLabel, .. names];
    }

    /// <summary>
    /// Which subregion each room is in, read from the lines of a region's map file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A room's line is its name, a colon, and six fields separated by <c>&gt;&lt;</c>: the room's x and y on the map,
    /// its x and y on the developer map, its layer, and the name of its subregion. Only the last is read. It is empty for
    /// a room with no subregion, such as a gate. For example:
    /// </para>
    /// <code>SU_B13: 184.4822&gt;&lt;248.3098&gt;&lt;141.6139&gt;&lt;444.8402&gt;&lt;1&gt;&lt;Outskirts</code>
    /// <para>
    /// Lines that start with "Off" or "Connection" are not rooms and are skipped.
    /// </para>
    /// </remarks>
    /// <param name="lines">The lines of the map file.</param>
    /// <param name="subregionNames">
    /// The region's subregion names, where index 0 is the label for "no subregion" and never matches a name.
    /// </param>
    /// <param name="spaceAfterColon">
    /// Puts in the space after the colon that the game does not insist on, before a line is split.
    /// </param>
    /// <returns>
    /// The index in <paramref name="subregionNames"/> of each room's subregion, by room name. A room whose subregion
    /// name is not in the list gets 0. A room the map does not mention is left out.
    /// </returns>
    public static Dictionary<string, int> ParseMapSubregions(
        IEnumerable<string> lines, IReadOnlyList<string> subregionNames, Func<string, string> spaceAfterColon)
    {
        var subregionOfRoom = new Dictionary<string, int>();
        IEnumerable<string[]> roomFields = lines
            .Where(line => !line.StartsWith("Off", StringComparison.Ordinal)
                           && !line.StartsWith("Connection", StringComparison.Ordinal))
            .Select(line => SplitField(spaceAfterColon(line)))
            .Where(fields => fields.Length >= 2);
        foreach (string[] fields in roomFields)
        {
            subregionOfRoom[fields[0]] = Subregion(fields[1], subregionNames);
        }
        return subregionOfRoom;
    }

    /// <summary> The index of the subregion a map line names, matched against the region's subregion names. 0 if none matches. </summary>
    private static int Subregion(string mapData, IReadOnlyList<string> subregionNames)
    {
        string[] fields = mapData.Split(["><"], StringSplitOptions.None);
        string name = fields.Length >= 6 ? fields[5].Trim() : "";
        // Index 0 is the label for "no subregion", so only the names after it can match. If two share a name, the later wins
        return Enumerable
            .Range(1, Math.Max(subregionNames.Count - 1, 0))
            .LastOrDefault(index => name.Length > 0 && subregionNames[index] == name);
    }

    /// <summary> Splits a "name: data" line at the colon. </summary>
    private static string[] SplitField(string line) => line.Split([": "], StringSplitOptions.None);
}
