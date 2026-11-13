using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Warp.Model;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for reading a region's text files (src/Model/RegionFiles.cs).</summary>
internal static class RegionFilesTests
{
    /// <summary>The game's own fix for "Subregion:Name", which the parsers take as an argument.</summary>
    private static string SpaceAfterColon(string line) => line.Replace(": ", ":").Replace(":", ": ");

    public static void Run()
    {
        Group(nameof(WorldFileTests), WorldFileTests);
        Group(nameof(SpoilerTests), SpoilerTests);
        Group(nameof(ConditionTests), ConditionTests);
        Group(nameof(CultureTests), CultureTests);
        Group(nameof(CameraTests), CameraTests);
        Group(nameof(SubregionNameTests), SubregionNameTests);
        Group(nameof(MapTests), MapTests);
        Group(nameof(RoomInfoTests), RoomInfoTests);
    }

    private static readonly string[] _world =
    [
        "CONDITIONAL LINKS", "END CONDITIONAL LINKS", "ROOMS",
        "SU_A01 : SU_A02, SU_B01",
        "  indented : ignored",
        "// a comment",
        "SU_S01 : SU_A01 : SHELTER",
        "GATE_SU_HI : SU_B13, DISCONNECTED : GATE",
        "(White)SU_C01 : SU_A01, DISCONNECTED",
        "{Spear}SU_D01 : SU_A01 : SWARMROOM",
        "HI_A01 : HI_A02 : SCAVTRADER",
        "SU_S01 : SU_A01 : SHELTER",
        "SU_OUT : SU_A01 : SCAVOUTPOST",
        "SU_X01 : SU_A01 : SCAVTRADER",
        "END ROOMS",
        "SU_AFTER : SU_A01"
    ];

    private static void WorldFileTests()
    {
        List<RoomInfo> rooms = RegionFiles.ParseWorldFile(_world, "SU", new HashSet<string> { "SU_X01" });
        CheckSequence("rooms are read in file order, skipping other regions, repeats and spoilers",
            ["SU_A01", "SU_S01", "GATE_SU_HI", "SU_C01", "SU_D01", "SU_OUT"], rooms.Select(r => r.Name));

        CheckEqual("no tag means an ordinary room", RoomType.Room, rooms[0].Type);
        CheckEqual("the third field is the tag", RoomType.Shelter, rooms[1].Type);
        CheckEqual("a gate tag in the third field", RoomType.Gate, rooms[2].Type);
        CheckEqual("a condition in parentheses is dropped", "SU_C01", rooms[3].Name);
        CheckEqual("a condition in braces is dropped", "SU_D01", rooms[4].Name);
        CheckEqual("swarm room tag", RoomType.SwarmRoom, rooms[4].Type);
        CheckEqual("scavenger outpost tag", RoomType.ScavOutpost, rooms[5].Type);
        CheckEqual("rooms start with one camera until the room file says otherwise", 1, rooms[0].Cameras);

        CheckEqual("a file with no room section has no rooms", 0,
            RegionFiles.ParseWorldFile(["SU_A01 : SU_A02"], "SU", new HashSet<string>()).Count);
        CheckEqual("a trader is read", RoomType.ScavTrader,
            RegionFiles.ParseWorldFile(_world, "HI", new HashSet<string>()).First(r => r.Name == "HI_A01").Type);
    }

    private static void SpoilerTests()
    {
        HashSet<string> spoilers = RegionFiles.ParseSpoilers(["// hidden rooms", "SU_X01", "  SU_Y01  ", ""]);
        CheckSequence("comments are skipped and names trimmed", new[] { "SU_X01", "SU_Y01", "" }.OrderBy(n => n),
            spoilers.OrderBy(n => n));
    }

    private static void CameraTests()
    {
        string[] room = ["SU_A01", "72*43|-1|0", "0", "0,0|100,100|200,200", "more"];
        CheckEqual("cameras are the positions on the fourth line", 3, RegionFiles.CameraCount(room));
        CheckEqual("one position is one camera", 1, RegionFiles.CameraCount(["a", "b", "c", "0,0"]));
        CheckEqual("a short file has no answer", null, RegionFiles.CameraCount(["a", "b"]));
    }

    private static void SubregionNameTests()
    {
        string[] properties = ["Room Setting: x", "Subregion: Filtration", "Subregion:Dry Zone", "Other: y", "Subregion"
        ];
        string[] names = RegionFiles.ParseSubregionNames(properties, "None", SpaceAfterColon);
        CheckSequence("names follow the label for none, in file order", ["None", "Filtration", "Dry Zone"], names);
        CheckSequence("a file with no subregions has only the label",
            ["None"], RegionFiles.ParseSubregionNames(["x: y"], "None", SpaceAfterColon));
    }

    private static void MapTests()
    {
        string[] names = ["None", "Filtration", "Dry Zone"];
        string[] map =
        [
            "Connection: SU_A01,SU_A02,1", "Off: stuff",
            "SU_A01: 1><2><3><4><5><Filtration",
            "SU_A02: 1><2><3><4><5><Dry Zone ",
            "SU_A03: 1><2><3><4><5><Unknown",
            "SU_A04: 1><2",
            "SU_A05: 1><2><3><4><5><"
        ];
        Dictionary<string, int> result = RegionFiles.ParseMapSubregions(map, names, SpaceAfterColon);
        CheckEqual("a named subregion gets its index", 1, result["SU_A01"]);
        CheckEqual("the name is trimmed", 2, result["SU_A02"]);
        CheckEqual("an unknown subregion is none", 0, result["SU_A03"]);
        CheckEqual("a short line is none", 0, result["SU_A04"]);
        CheckEqual("an empty name is none", 0, result["SU_A05"]);
        CheckEqual("connections and off-lines are not rooms", 5, result.Count);
    }

    private static void RoomInfoTests()
    {
        CheckEqual("the short name drops the region", "A01", new RoomInfo("SU_A01").ShortName);
        CheckEqual("only the first underscore counts", "HI_A", new RoomInfo("GATE_HI_A").ShortName);
        CheckEqual("no underscore to cut at, the whole name", "OFFSCREEN", new RoomInfo("OFFSCREEN").ShortName);
        CheckEqual("nothing after the underscore, the whole name", "SU_", new RoomInfo("SU_").ShortName);

        var room = new RoomInfo("SU_A01", RoomType.Shelter, 4, 2);
        CheckEqual("group by type", (int)RoomType.Shelter, room.GroupKey(Grouping.Type));
        CheckEqual("group by size", 4, room.GroupKey(Grouping.Size));
        CheckEqual("group by subregion", 2, room.GroupKey(Grouping.Subregion));

        RoomInfo[] rooms =
        [
            new("SU_B", RoomType.Shelter, 2, 1), new("SU_A", RoomType.Shelter, 5),
            new("SU_C", RoomType.Room, 3, 1), new("SU_D", RoomType.Gate, 3)
        ];
        CheckSequence("sorted by type, then name", ["SU_C", "SU_D", "SU_A", "SU_B"],
            RoomSorting.Sort(rooms, Grouping.Type).Select(r => r.Name));
        CheckSequence("sorted by size, then name", ["SU_B", "SU_C", "SU_D", "SU_A"],
            RoomSorting.Sort(rooms, Grouping.Size).Select(r => r.Name));
        CheckSequence("sorted by subregion, then name", ["SU_A", "SU_D", "SU_B", "SU_C"],
            RoomSorting.Sort(rooms, Grouping.Subregion).Select(r => r.Name));
    }

    private static void ConditionTests()
    {
        CheckEqual("a condition in parentheses", "SU_A01", RegionFiles.StripCondition("(White)SU_A01"));
        CheckEqual("a condition in braces", "SU_A01", RegionFiles.StripCondition("{Spear}SU_A01"));
        CheckEqual("both, in either order", "SU_A01", RegionFiles.StripCondition("{Spear}(White)SU_A01"));
        CheckEqual("both, the other way round", "SU_A01", RegionFiles.StripCondition("(White){Spear}SU_A01"));
        CheckEqual("no condition", "SU_A01", RegionFiles.StripCondition("SU_A01"));
        CheckEqual("a bracket in the middle of a name is not a condition", "SU_A(1)", RegionFiles.StripCondition("SU_A(1)"));
        CheckEqual("a condition that is never closed is left alone", "(WhiteSU_A01", RegionFiles.StripCondition("(WhiteSU_A01"));
        CheckEqual("an empty name", "", RegionFiles.StripCondition(""));

        string[] world =
        [
            "ROOMS",
            "LSU_A01 : LSU_A02",
            "SU_A01 : SU_A02",
            "{Spear}(White)SU_A02 : SU_A01",
            "(Spear)LSU_A03 : SU_A01",
            "GATE_LSU_SU : LSU_A01, SU_A01 : GATE",
            "END ROOMS"
        ];
        CheckSequence("a room has to start with the region's prefix, once its condition is taken off",
            ["SU_A01", "SU_A02", "GATE_LSU_SU"],
            RegionFiles.ParseWorldFile(world, "SU", new HashSet<string>()).Select(room => room.Name));
    }

    /// <summary>The file formats are the same whatever language the computer is set to.</summary>
    private static void CultureTests()
    {
        CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            RoomInfo[] rooms = [new("SU_i"), new("SU_I"), new("SU_a1"), new("SU_B1"), new("GATE_X")];
            CheckSequence("rooms are ordered by character code, not by language",
                ["GATE_X", "SU_B1", "SU_I", "SU_a1", "SU_i"],
                RoomSorting.Sort(rooms, Grouping.Type).Select(room => room.Name));
            CheckSequence("so are region buttons", ["B", "I", "a", "i"],
                RegionList.ButtonOrder(["i", "a", "I", "B"], new HashSet<string>(), true));
            CheckEqual("a comment marker is found", true, RegionFiles.ParseSpoilers(["// note", "SU_I"]).SetEquals(["SU_I"]));
            CheckEqual("a heading is found", true,
                RegionFiles.ParseSubregionNames(["Subregion: X"], "None", line => line).Length == 2);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
