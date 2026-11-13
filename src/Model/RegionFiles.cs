#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        string first = parts[0];
        if (!first.Contains(region + "_") && !first.Contains(GatePrefix))
        {
            return null;
        }
        return new RoomInfo(StripCondition(first), parts.Length > 2 ? TypeFromTag(parts[2]) : RoomType.Room);
    }

    /// <summary>
    /// A room name without the condition in front of it: "{Spear}SU_A01" and "(White)SU_A01" both give "SU_A01".
    /// </summary>
    private static string StripCondition(string name)
    {
        if (name.Contains("}"))
        {
            return name.Split('}')[1];
        }
        return name.Contains(")") ? name.Split(')')[1] : name;
    }

    /// <summary> The room type a world file tag stands for. A missing or unknown tag is an ordinary room. </summary>
    private static RoomType TypeFromTag(string tag) => tag switch
    {
        "GATE" => RoomType.Gate,
        "SHELTER" => RoomType.Shelter,
        "SWARMROOM" => RoomType.SwarmRoom,
        "SCAVTRADER" => RoomType.ScavTrader,
        "SCAVOUTPOST" => RoomType.ScavOutpost,
        _ => RoomType.Room
    };

    /// <summary>
    /// The room names in a spoilers file. Lines that start with "//" are comments.
    /// </summary>
    public static HashSet<string> ParseSpoilers(IEnumerable<string> lines)
        => [.. lines.Where(line => !line.StartsWith("//")).Select(line => line.Trim())];

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
        return Math.Max(1, roomFileLines[cameraLine].Split('|').Length);
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
            .Where(line => line.StartsWith("Subregion"))
            .Select(line => SplitField(spaceAfterColon(line)))
            .Where(fields => fields.Length > 1)
            .Select(fields => fields[1]);
        return [noneLabel, .. names];
    }

    /// <summary>
    /// The subregion index of each room named in a map file. A map line is the room's name, a colon, and fields separated by
    /// the two characters <c>&gt;&lt;</c>, as in <c>SU_A01: 12&gt;&lt;34&gt;&lt;...&gt;&lt;Filtration System</c>. The
    /// sixth field is the subregion's name. "Off" and "Connection" lines are not rooms. A room whose name is not
    /// among <paramref name="subregionNames"/> (index 0, "no subregion", is never a match) gets subregion 0. Rooms
    /// the map does not mention are absent from the result.
    /// </summary>
    public static Dictionary<string, int> ParseMapSubregions(
        IEnumerable<string> lines, IReadOnlyList<string> subregionNames, Func<string, string> spaceAfterColon)
    {
        var result = new Dictionary<string, int>();
        foreach (string line in lines)
        {
            if (line.StartsWith("Off") || line.StartsWith("Connection"))
            {
                continue;
            }
            string[] fields = SplitField(spaceAfterColon(line));
            if (fields.Length < 2)
            {
                continue;
            }
            result[fields[0]] = Subregion(fields[1], subregionNames);
        }
        return result;
    }

    /// <summary> The index of the subregion a map line names, matched against the region's subregion names. 0 if none matches. </summary>
    private static int Subregion(string mapData, IReadOnlyList<string> subregionNames)
    {
        string[] data = Regex.Split(mapData, "><");
        string name = data.Length >= 6 ? data[5].Trim() : "";
        for (int i = subregionNames.Count - 1; i >= 1; i--)
        {
            if (name.Length > 0 && subregionNames[i] == name)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary> Splits a "name: data" line at the colon. </summary>
    private static string[] SplitField(string line) => line.Split([": "], StringSplitOptions.None);
}
