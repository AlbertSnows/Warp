#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Warp.Model;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for what is saved and listed: settings, favourites, colors, stats and the region list.</summary>
internal static class DataTests
{
    public static void Run()
    {
        Group(nameof(PreferencesTests), PreferencesTests);
        Group(nameof(FavouritesTests), FavouritesTests);
        Group(nameof(PaletteTests), PaletteTests);
        Group(nameof(ColorFileTests), ColorFileTests);
        Group(nameof(ShippedColorsTests), ShippedColorsTests);
        Group(nameof(StatsTests), StatsTests);
        Group(nameof(RegionListTests), RegionListTests);
        Group(nameof(ExportTests), ExportTests);
        Group(nameof(SignalTests), SignalTests);
        Group(nameof(RoomTypeTests), RoomTypeTests);
        Group(nameof(GroupingTests), GroupingTests);
    }

    private static void PreferencesTests()
    {
        CheckEqual("the defaults are written as digits", "100011", new Preferences().Serialize());
        var chosen = new Preferences(false, true, Grouping.Subregion, Grouping.Size, false, false);
        CheckEqual("choices are written in order", "012100", chosen.Serialize());
        CheckEqual("and read back", chosen, Preferences.Parse("012100"));
        CheckEqual("an empty file means the defaults", new Preferences(), Preferences.Parse(""));
        CheckEqual("a file from an older version may stop early", new Preferences(false),
            Preferences.Parse("0"));
        CheckEqual("surrounding whitespace is ignored", new Preferences(), Preferences.Parse("100011\n"));
        CheckEqual("text that is not digits is not a settings file", null, Preferences.Parse("hello"));
        CheckEqual("a flag that is not 0 or 1 is not a settings file", null, Preferences.Parse("190011"));
        CheckEqual("a grouping past the third is not a settings file", null, Preferences.Parse("103011"));
    }

    private static void FavouritesTests()
    {
        HashSet<string> set = FavouriteSet.Parse("SU:HI::GW\n");
        CheckSequence("names are split at colons, trimmed, and empty ones dropped", ["GW", "HI", "SU"],
            set.OrderBy(n => n));
        CheckEqual("an empty file has none", 0, FavouriteSet.Parse("").Count);
        CheckEqual("they are joined with colons, in order of name so the file is stable", "HI:SU", FavouriteSet.Serialize(["SU", "HI"]));
        Check("toggling adds a region", FavouriteSet.Toggle(["SU"], "HI").SetEquals(["SU", "HI"]));
        Check("and removes it again", FavouriteSet.Toggle(["SU", "HI"], "HI").SetEquals(["SU"]));
        var original = new HashSet<string> { "SU" };
        FavouriteSet.Toggle(original, "HI");
        CheckEqual("the original set is left alone", 1, original.Count);
    }

    private static void PaletteTests()
    {
        ColorPalette palette = ColorPalette.Default;
        CheckEqual("the room type colors", new Hsl(0.33f, 0.99f, 0.67f), palette.Type(RoomType.Room));
        CheckEqual("nine or more cameras share the last color", palette.Size(9), palette.Size(40));
        CheckEqual("a camera count under 1 is read as 1", palette.Size(1), palette.Size(0));

        ColorPalette edited = palette.WithType((RoomType)1, new Hsl(0.5f, 0.5f, 0.5f));
        CheckEqual("an edit changes the new palette", new Hsl(0.5f, 0.5f, 0.5f), edited.Type((RoomType)1));
        CheckEqual("and not the old one", new Hsl(0.18f, 0.99f, 0.63f), palette.Type((RoomType)1));

        CheckEqual("a region with no colors falls back to the default for the index", ColorPalette.DefaultSubregion(2),
            palette.Subregion("SU", 2));
        ColorPalette grown = palette.WithSubregionCount("SU", 4);
        CheckEqual("growing a region keeps four colors", 4, grown.Subregions["SU"].Count);
        Check("asking for fewer than it has returns the same palette", ReferenceEquals(grown, grown.WithSubregionCount("SU", 2)));
        ColorPalette recolored = grown.WithSubregion("SU", 6, new Hsl(0.1f, 0.2f, 0.3f));
        CheckEqual("setting one past the end grows the list first", 7, recolored.Subregions["SU"].Count);
        CheckEqual("and sets the color", new Hsl(0.1f, 0.2f, 0.3f), recolored.Subregion("SU", 6));
        CheckEqual("the earlier list is unchanged", 4, grown.Subregions["SU"].Count);

        Hsl deep = ColorPalette.DefaultSubregion(40);
        CheckEqual("defaults past the table are always the same", deep, ColorPalette.DefaultSubregion(40));
        Check("and a valid color", deep.Hue is >= 0f and < 1f);

        var room = new RoomInfo("SU_A", RoomType.Shelter, 3, 1);
        CheckEqual("colored by type", palette.Type(RoomType.Shelter), palette.For(room, Grouping.Type, "SU"));
        CheckEqual("colored by size", palette.Size(3), palette.For(room, Grouping.Size, "SU"));
        CheckEqual("colored by subregion", palette.Subregion("SU", 1), palette.For(room, Grouping.Subregion, "SU"));
    }

    private static void ColorFileTests()
    {
        ColorPalette edited = ColorPalette.Default
            .WithType((RoomType)2, new Hsl(0.25f, 0.5f, 0.75f))
            .WithSubregions("SU", [new Hsl(0.1f, 0.2f, 0.3f), new Hsl(0.4f, 0.5f, 0.6f)]);
        string text = ColorFile.Serialize(edited);
        ColorPalette read = ColorFile.Parse(text.Split('\n'), ColorPalette.Default);
        CheckSequence("types survive a round trip", edited.Types, read.Types);
        CheckSequence("sizes survive a round trip", edited.Sizes, read.Sizes);
        CheckSequence("subregion colors survive a round trip", edited.Subregions["SU"], read.Subregions["SU"]);
        Check("the text ends with the end marker", text.EndsWith("[END]"));
        string twice = ColorFile.Serialize(ColorFile.Parse(text.Split('\n'), ColorPalette.Default));
        CheckEqual("saving the palette that was read gives the same file", text, twice);

        Hsl[] given = [new(0.1f, 0.2f, 0.3f)];
        var kept = new ColorPalette([.. ColorPalette.Default.Types], [.. ColorPalette.Default.Sizes], new Dictionary<string, Hsl[]> { ["ZZ"] = given });
        given[0] = new Hsl(0.9f, 0.9f, 0.9f);
        CheckEqual("the palette keeps its own copy of what it was given", new Hsl(0.1f, 0.2f, 0.3f), kept.Subregion("ZZ", 0));
        CheckSequence("regions are written in order of name", ["[AA]", "[ZZ]"],
            ColorFile.Serialize(kept.WithSubregion("AA", 0, new Hsl(0f, 0f, 0f))).Split('\n')
                .Select(line => line.Trim()).Where(line => line is "[AA]" or "[ZZ]"));

        string[] lines =
        [
            "// comment", "[TYPE]", "0.5:0.5:0.5", "bad", "0,25:0,5:0,75", "", "[SIZE]", "0.1:0.2:0.3",
            "[SUBREGION]", "[GW]", "0.9:0.8:0.7", "oops", "0.6:0.5:0.4", "[END]", "[TYPE]", "0:0:0"
        ];
        var bad = new List<string>();
        ColorPalette parsed = ColorFile.Parse(lines, ColorPalette.Default, bad.Add);
        CheckEqual("a color is read", new Hsl(0.5f, 0.5f, 0.5f), parsed.Type(0));
        CheckEqual("a color that cannot be read keeps its old value", ColorPalette.Default.Type((RoomType)1), parsed.Type((RoomType)1));
        CheckEqual("a decimal comma from an old file is accepted", new Hsl(0.25f, 0.5f, 0.75f), parsed.Type((RoomType)2));
        CheckEqual("sizes are read in their own section", new Hsl(0.1f, 0.2f, 0.3f), parsed.Size(1));
        CheckEqual("a bad subregion color is replaced by the default, so the later ones keep their place",
            new Hsl(0.6f, 0.5f, 0.4f), parsed.Subregion("GW", 2));
        CheckEqual("reading stops at the end marker", ColorPalette.Default.Type((RoomType)3), parsed.Type((RoomType)3));
        CheckSequence("bad lines are reported", ["bad", "oops"], bad);

        string[] odd = ["[TYPE]", "NaN:0.5:0.5", "0.5:Infinity:0.5", "-0.1:0.5:0.5", "1.5:0.5:0.5", "0.5:0.5:0.5:9"];
        var oddBad = new List<string>();
        ColorPalette oddPalette = ColorFile.Parse(odd, ColorPalette.Default, oddBad.Add);
        CheckSequence("NaN, infinity and values outside 0 to 1 are bad lines",
            ["NaN:0.5:0.5", "0.5:Infinity:0.5", "-0.1:0.5:0.5", "1.5:0.5:0.5"], oddBad);
        CheckEqual("and leave the color as it was", ColorPalette.Default.Type(0), oddPalette.Type(0));
        CheckEqual("a fourth field is ignored", new Hsl(0.5f, 0.5f, 0.5f), oddPalette.Type((RoomType)4));
        CheckEqual("the edges 0 and 1 are fine", new Hsl(0f, 1f, 0.5f),
            ColorFile.Parse(["[TYPE]", "0:1:0.5"], ColorPalette.Default).Type(0));
    }

    private static void ShippedColorsTests()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Colors.txt");
        var bad = new List<string>();
        ColorPalette shipped = ColorFile.Parse(System.IO.File.ReadAllLines(path), ColorPalette.Default, bad.Add);
        CheckEqual("the colors file that ships with Warp has nothing unreadable", 0, bad.Count);
        CheckSequence("its type colors are the built-in ones", ColorPalette.Default.Types, shipped.Types);
        CheckSequence("its size colors are the built-in ones", ColorPalette.Default.Sizes, shipped.Sizes);
        Check("it has colors for some regions", shipped.Subregions.ContainsKey("SU") && shipped.Subregions.Count > 3);
        CheckEqual("each region's list starts with the color for none", ColorPalette.DefaultSubregion(0),
            shipped.Subregions["SU"][0]);
    }

    private static void StatsTests()
    {
        RoomInfo[] rooms =
        [
            new("SU_A", Cameras: 1), new("SU_B", Cameras: 3), new("GATE_X", RoomType.Gate, 2),
            new("SU_S", RoomType.Shelter), new("SU_W", RoomType.SwarmRoom)
        ];
        var stats = RegionStats.Of(rooms);
        CheckEqual("rooms are counted", 5, stats.Rooms);
        CheckEqual("screens are added up", 8, stats.Screens);
        CheckEqual("rooms of a type", 1, stats.Count(RoomType.Gate));
        CheckEqual("a type with none counts zero", 0, stats.Count(RoomType.ScavTrader));
        CheckSequence("the screen breakdown is in order of size",
            [new KeyValuePair<int, int>(1, 3), new KeyValuePair<int, int>(2, 1), new KeyValuePair<int, int>(3, 1)],
            stats.RoomsByCameras);

        (string summary, string breakdown) = stats.Describe(text => text);
        string nl = Environment.NewLine;
        CheckEqual("the summary lists the counts and hides empty extras",
            "ROOMS: 5" + nl + "SCREENS: 8" + nl + nl + "Gates: 1" + nl + "Shelters: 1" + nl + "Swarm Rooms: 1" + nl, summary);
        CheckEqual("the breakdown has a line for each size",
            nl + "1 Screen: 3" + nl + "2 Screen: 1" + nl + "3 Screen: 1", breakdown);
        CheckEqual("an empty region has no breakdown", "", RegionStats.Of([]).Describe(t => t).Breakdown);
        CheckEqual("text goes through the translator", "X ROOMS: 0",
            RegionStats.Of([]).Describe(t => "X " + t).Summary.Split('\n')[0].TrimEnd('\r'));
    }

    private static void RegionListTests()
    {
        string[] ids = ["SU", "HI", "CC", "DS", "SU"];
        string? Name(string id) => id switch
        {
            "SU" => "Outskirts",
            "HI" => "Industrial Complex",
            "CC" => null,
            _ => "Drainage"
        };
        var favourites = new HashSet<string> { "HI" };

        List<RegionEntry> alphabetical = RegionList.DropdownEntries(ids, Name, favourites, true);
        CheckSequence("favourites are marked and come first, the rest follow alphabetically; repeats are dropped",
            ["> Industrial Complex", "CC", "Drainage", "Outskirts"], alphabetical.Select(e => e.Display));
        CheckSequence("each entry keeps its id", ["HI", "CC", "DS", "SU"], alphabetical.Select(e => e.Id));
        CheckSequence("without sorting the favourites still come first and the rest stay as listed",
            ["> Industrial Complex", "Outskirts", "CC", "Drainage"],
            RegionList.DropdownEntries(ids, Name, favourites, false).Select(e => e.Display));

        CheckSequence("buttons: favourites first then the rest sorted", ["HI", "CC", "DS", "SU"],
            RegionList.ButtonOrder(ids.Distinct(), favourites, true));
        CheckSequence("buttons: unsorted keeps the game's order after the favourites", ["HI", "SU", "CC", "DS"],
            RegionList.ButtonOrder(ids.Distinct(), favourites, false));
        CheckSequence("favourite buttons are always sorted", ["CC", "HI", "SU"],
            RegionList.ButtonOrder(["SU", "HI", "CC"], new HashSet<string> { "SU", "HI", "CC" }, false));
    }

    private static void ExportTests()
    {
        KeyValuePair<string, RoomInfo[]>[] regions =
        [
            new("SU", [new RoomInfo("SU_A", Cameras: 2, Subregion: 1)]),
            new("HI", [])
        ];
        string nl = Environment.NewLine;
        CheckEqual("each region lists its rooms and a total",
            "---WARP Room List Export---" + nl + nl
            + "---SU---" + nl + "SU_A: | Cameras: 2, Subregion: 1" + nl + "---Total Rooms: 1---" + nl + nl
            + "---HI---" + nl + "---Total Rooms: 0---" + nl + nl,
            RoomListExport.Format(regions));
    }

    private static void SignalTests()
    {
        CheckEqual("a room button's message names its room", "SU_A01", WarpSignals.RoomOf(WarpSignals.ForRoom("SU_A01")));
        CheckEqual("another mod's message that ends in warp is not a room", null, WarpSignals.RoomOf("AnotherModwarp"));
        CheckEqual("nor one that is only the old suffix on a room name", null, WarpSignals.RoomOf("SU_A01warp"));
        CheckEqual("a message with the prefix and nothing after it is not a room", null, WarpSignals.RoomOf("warp:room:"));
        CheckEqual("the toggle is not a room", null, WarpSignals.RoomOf(WarpSignals.Toggle));
        CheckEqual("room names with colons survive", "GATE_A:B", WarpSignals.RoomOf(WarpSignals.ForRoom("GATE_A:B")));

        CheckEqual("a region button's message names its region", "SU", WarpSignals.RegionOf(WarpSignals.ForRegion("SU")));
        CheckEqual("a region message with no region is nothing", null, WarpSignals.RegionOf("reg-"));
        CheckEqual("another message is not a region", null, WarpSignals.RegionOf("STATS"));

        foreach (Grouping grouping in Groupings.All)
        {
            CheckEqual("the sort button for " + grouping + " reads back", new WarpSignals.GroupingChoice(true, grouping),
                WarpSignals.GroupingChoiceOf(WarpSignals.ForSort(grouping)));
            CheckEqual("the view button for " + grouping + " reads back", new WarpSignals.GroupingChoice(false, grouping),
                WarpSignals.GroupingChoiceOf(WarpSignals.ForView(grouping)));
            CheckEqual("the category button for " + grouping + " reads back", grouping,
                WarpSignals.CategoryOf(WarpSignals.ForCategory(grouping)));
        }
        string[] notChoices = [WarpSignals.Stats, WarpSignals.EditorDone, WarpSignals.EditorSave, WarpSignals.Colors, "S", "V", "SX", ""];
        CheckSequence("messages that are not a sort or view choice", [], notChoices.Where(m => WarpSignals.GroupingChoiceOf(m) != null));
        CheckSequence("messages that are not a category", [],
            new[] { WarpSignals.Colors, "C", "CX", "STYPE", "" }.Where(m => WarpSignals.CategoryOf(m) != null));

        CheckEqual("a swatch's message names its index", 7, WarpSignals.SwatchOf(WarpSignals.ForSwatch(7)));
        CheckEqual("index 0 is a swatch", 0, WarpSignals.SwatchOf("X0"));
        CheckSequence("messages that are not a swatch", [],
            new[] { "X", "X-1", "Xa", "X1.5", "X 2", "", WarpSignals.Stats }.Where(m => WarpSignals.SwatchOf(m) != null));

        string[] ownMessages =
        [
            WarpSignals.PageLeft, WarpSignals.PageRight, WarpSignals.Stats, WarpSignals.Favourite,
            WarpSignals.ToggleRegionList, WarpSignals.Colors, WarpSignals.EditorDone, WarpSignals.EditorSave
        ];
        CheckEqual("none of Warp's own messages is taken for a room or the toggle", true,
            ownMessages.All(m => WarpSignals.RoomOf(m) == null && m != WarpSignals.Toggle));
        CheckEqual("and they are all different", ownMessages.Length, ownMessages.Distinct().Count());
    }

    private static void RoomTypeTests()
    {
        RoomType[] types = [.. Enum.GetValues(typeof(RoomType)).Cast<RoomType>()];
        CheckEqual("there is a row for every room type", types.Length, RoomTypes.All.Count);
        CheckEqual("each row is at the index of its type, which the palette depends on", true,
            RoomTypes.All.Select((info, i) => info.Type == (RoomType)i).All(matches => matches));
        CheckEqual("every type can be looked up", true, types.All(type => RoomTypes.Of(type).Type == type));
        CheckEqual("headings are not repeated", RoomTypes.All.Count, RoomTypes.All.Select(info => info.Heading).Distinct().Count());
        CheckEqual("the palette has a color for each type", RoomTypes.All.Count, ColorPalette.TypeCount);

        foreach (RoomTypeInfo info in RoomTypes.All.Where(info => info.WorldTag.Length > 0))
        {
            CheckEqual("the tag " + info.WorldTag + " reads back as its type", info.Type, RoomTypes.FromWorldTag(info.WorldTag));
        }
        CheckEqual("no tag is an ordinary room", RoomType.Room, RoomTypes.FromWorldTag(""));
        CheckEqual("an unknown tag is an ordinary room", RoomType.Room, RoomTypes.FromWorldTag("DISCONNECTED"));
        CheckEqual("tags are matched exactly", RoomType.Room, RoomTypes.FromWorldTag("shelter"));
    }

    private static void GroupingTests()
    {
        CheckSequence("the groupings are in the order of the buttons", [Grouping.Type, Grouping.Size, Grouping.Subregion], Groupings.All);
        CheckSequence("their codes", ["TYPE", "SIZE", "SUB"], Groupings.All.Select(grouping => grouping.Code()));
        CheckEqual("every code reads back", true, Groupings.All.All(grouping => Groupings.FromCode(grouping.Code()) == grouping));
        CheckEqual("an unknown code is nothing", null, Groupings.FromCode("TATS"));
        CheckEqual("codes are matched exactly", null, Groupings.FromCode("type"));
        CheckEqual("no code is nothing", null, Groupings.FromCode(""));

        var start = new Preferences(Sort: Grouping.Type, View: Grouping.Type);
        CheckEqual("sorting by something also colors by it", (Grouping.Subregion, Grouping.Subregion),
            (start.WithSort(Grouping.Subregion).Sort, start.WithSort(Grouping.Subregion).View));
        CheckEqual("coloring by something leaves the sort alone", (Grouping.Type, Grouping.Size),
            (start.WithView(Grouping.Size).Sort, start.WithView(Grouping.Size).View));
        CheckEqual("and the other settings are untouched", start with
        {
            Sort = Grouping.Size,
            View = Grouping.Size
        },
            start.WithSort(Grouping.Size));
    }
}
