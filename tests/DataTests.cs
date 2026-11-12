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
        var set = Favourites.Parse("SU:HI::GW\n");
        CheckSequence("names are split at colons, trimmed, and empty ones dropped", new[] { "GW", "HI", "SU" },
            set.OrderBy(n => n));
        CheckEqual("an empty file has none", 0, Favourites.Parse("").Count);
        CheckEqual("they are joined with colons", "SU:HI", Favourites.Serialize(new[] { "SU", "HI" }));
        Check("toggling adds a region", Favourites.Toggle(new[] { "SU" }, "HI").SetEquals(new[] { "SU", "HI" }));
        Check("and removes it again", Favourites.Toggle(new[] { "SU", "HI" }, "HI").SetEquals(new[] { "SU" }));
        var original = new HashSet<string> { "SU" };
        Favourites.Toggle(original, "HI");
        CheckEqual("the original set is left alone", 1, original.Count);
    }

    private static void PaletteTests()
    {
        var palette = ColorPalette.Default;
        CheckEqual("the room type colors", new Hsl(0.33f, 0.99f, 0.67f), palette.Type((int)RoomType.Room));
        CheckEqual("nine or more cameras share the last color", palette.Size(9), palette.Size(40));
        CheckEqual("a camera count under 1 is read as 1", palette.Size(1), palette.Size(0));

        var edited = palette.WithType(1, new Hsl(0.5f, 0.5f, 0.5f));
        CheckEqual("an edit changes the new palette", new Hsl(0.5f, 0.5f, 0.5f), edited.Type(1));
        CheckEqual("and not the old one", new Hsl(0.18f, 0.99f, 0.63f), palette.Type(1));

        CheckEqual("a region with no colors falls back to the default for the index", ColorPalette.DefaultSubregion(2),
            palette.Subregion("SU", 2));
        var grown = palette.WithSubregionCount("SU", 4);
        CheckEqual("growing a region keeps four colors", 4, grown.Subregions["SU"].Length);
        Check("asking for fewer than it has returns the same palette", ReferenceEquals(grown, grown.WithSubregionCount("SU", 2)));
        var recolored = grown.WithSubregion("SU", 6, new Hsl(0.1f, 0.2f, 0.3f));
        CheckEqual("setting one past the end grows the list first", 7, recolored.Subregions["SU"].Length);
        CheckEqual("and sets the color", new Hsl(0.1f, 0.2f, 0.3f), recolored.Subregion("SU", 6));
        CheckEqual("the earlier list is unchanged", 4, grown.Subregions["SU"].Length);

        var deep = ColorPalette.DefaultSubregion(40);
        CheckEqual("defaults past the table are always the same", deep, ColorPalette.DefaultSubregion(40));
        Check("and a valid color", deep.Hue is >= 0f and < 1f);

        var room = new RoomInfo("SU_A", RoomType.Shelter, 3, 1);
        CheckEqual("colored by type", palette.Type((int)RoomType.Shelter), palette.For(room, Grouping.Type, "SU"));
        CheckEqual("colored by size", palette.Size(3), palette.For(room, Grouping.Size, "SU"));
        CheckEqual("colored by subregion", palette.Subregion("SU", 1), palette.For(room, Grouping.Subregion, "SU"));
    }

    private static void ColorFileTests()
    {
        var edited = ColorPalette.Default
            .WithType(2, new Hsl(0.25f, 0.5f, 0.75f))
            .WithSubregions("SU", new[] { new Hsl(0.1f, 0.2f, 0.3f), new Hsl(0.4f, 0.5f, 0.6f) });
        var text = ColorFile.Serialize(edited);
        var read = ColorFile.Parse(text.Split('\n'), ColorPalette.Default);
        CheckSequence("types survive a round trip", edited.Types, read.Types);
        CheckSequence("sizes survive a round trip", edited.Sizes, read.Sizes);
        CheckSequence("subregion colors survive a round trip", edited.Subregions["SU"], read.Subregions["SU"]);
        Check("the text ends with the end marker", text.EndsWith("[END]"));

        var lines = new[]
        {
            "// comment", "[TYPE]", "0.5:0.5:0.5", "bad", "0,25:0,5:0,75", "", "[SIZE]", "0.1:0.2:0.3",
            "[SUBREGION]", "[GW]", "0.9:0.8:0.7", "oops", "0.6:0.5:0.4", "[END]", "[TYPE]", "0:0:0"
        };
        var bad = new List<string>();
        var parsed = ColorFile.Parse(lines, ColorPalette.Default, bad.Add);
        CheckEqual("a color is read", new Hsl(0.5f, 0.5f, 0.5f), parsed.Type(0));
        CheckEqual("a color that cannot be read keeps its old value", ColorPalette.Default.Type(1), parsed.Type(1));
        CheckEqual("a decimal comma from an old file is accepted", new Hsl(0.25f, 0.5f, 0.75f), parsed.Type(2));
        CheckEqual("sizes are read in their own section", new Hsl(0.1f, 0.2f, 0.3f), parsed.Size(1));
        CheckEqual("a bad subregion color is replaced by the default, so the later ones keep their place",
            new Hsl(0.6f, 0.5f, 0.4f), parsed.Subregion("GW", 2));
        CheckEqual("reading stops at the end marker", ColorPalette.Default.Type(3), parsed.Type(3));
        CheckSequence("bad lines are reported", new[] { "bad", "oops" }, bad);
    }

    private static void ShippedColorsTests()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Colors.txt");
        var bad = new List<string>();
        var shipped = ColorFile.Parse(System.IO.File.ReadAllLines(path), ColorPalette.Default, bad.Add);
        CheckEqual("the colors file that ships with Warp has nothing unreadable", 0, bad.Count);
        CheckSequence("its type colors are the built-in ones", ColorPalette.Default.Types, shipped.Types);
        CheckSequence("its size colors are the built-in ones", ColorPalette.Default.Sizes, shipped.Sizes);
        Check("it has colors for some regions", shipped.Subregions.ContainsKey("SU") && shipped.Subregions.Count > 3);
        CheckEqual("each region's list starts with the color for none", ColorPalette.DefaultSubregion(0),
            shipped.Subregions["SU"][0]);
    }

    private static void StatsTests()
    {
        var rooms = new[]
        {
            new RoomInfo("SU_A", Cameras: 1), new RoomInfo("SU_B", Cameras: 3), new RoomInfo("GATE_X", RoomType.Gate, 2),
            new RoomInfo("SU_S", RoomType.Shelter, 1), new RoomInfo("SU_W", RoomType.SwarmRoom, 1)
        };
        var stats = RegionStats.Of(rooms);
        CheckEqual("rooms are counted", 5, stats.Rooms);
        CheckEqual("screens are added up", 8, stats.Screens);
        CheckEqual("rooms of a type", 1, stats.Count(RoomType.Gate));
        CheckEqual("a type with none counts zero", 0, stats.Count(RoomType.ScavTrader));
        CheckSequence("the screen breakdown is in order of size",
            new[] { new KeyValuePair<int, int>(1, 3), new KeyValuePair<int, int>(2, 1), new KeyValuePair<int, int>(3, 1) },
            stats.RoomsByCameras);

        var (summary, breakdown) = stats.Describe(text => text);
        var nl = Environment.NewLine;
        CheckEqual("the summary lists the counts and hides empty extras",
            "ROOMS: 5" + nl + "SCREENS: 8" + nl + nl + "Gates: 1" + nl + "Shelters: 1" + nl + "Swarm Rooms: 1" + nl, summary);
        CheckEqual("the breakdown has a line for each size",
            nl + "1 Screen: 3" + nl + "2 Screen: 1" + nl + "3 Screen: 1", breakdown);
        CheckEqual("an empty region has no breakdown", "", RegionStats.Of(Array.Empty<RoomInfo>()).Describe(t => t).Breakdown);
        CheckEqual("text goes through the translator", "X ROOMS: 0",
            RegionStats.Of(Array.Empty<RoomInfo>()).Describe(t => "X " + t).Summary.Split('\n')[0].TrimEnd('\r'));
    }

    private static void RegionListTests()
    {
        var ids = new[] { "SU", "HI", "CC", "DS", "SU" };
        string? Name(string id) => id switch
        {
            "SU" => "Outskirts",
            "HI" => "Industrial Complex",
            "CC" => null,
            _ => "Drainage"
        };
        var favourites = new HashSet<string> { "HI" };

        var alphabetical = RegionList.DropdownEntries(ids, Name, favourites, true);
        CheckSequence("favourites are marked and come first, the rest follow alphabetically; repeats are dropped",
            new[] { "> Industrial Complex", "CC", "Drainage", "Outskirts" }, alphabetical.Select(e => e.Display));
        CheckSequence("each entry keeps its id", new[] { "HI", "CC", "DS", "SU" }, alphabetical.Select(e => e.Id));
        CheckSequence("without sorting they stay as listed", new[] { "Outskirts", "> Industrial Complex", "CC", "Drainage" },
            RegionList.DropdownEntries(ids, Name, favourites, false).Select(e => e.Display));

        CheckSequence("buttons: favourites first then the rest sorted", new[] { "HI", "CC", "DS", "SU" },
            RegionList.ButtonOrder(ids.Distinct(), favourites, true));
        CheckSequence("buttons: unsorted keeps the game's order after the favourites", new[] { "HI", "SU", "CC", "DS" },
            RegionList.ButtonOrder(ids.Distinct(), favourites, false));
        CheckSequence("favourite buttons are always sorted", new[] { "CC", "HI", "SU" },
            RegionList.ButtonOrder(new[] { "SU", "HI", "CC" }, new HashSet<string> { "SU", "HI", "CC" }, false));
    }

    private static void ExportTests()
    {
        var regions = new[]
        {
            new KeyValuePair<string, RoomInfo[]>("SU", new[] { new RoomInfo("SU_A", Cameras: 2, Subregion: 1) }),
            new KeyValuePair<string, RoomInfo[]>("HI", Array.Empty<RoomInfo>())
        };
        var nl = Environment.NewLine;
        CheckEqual("each region lists its rooms and a total",
            "---WARP Room List Export---" + nl + nl
            + "---SU---" + nl + "SU_A: | Cameras: 2, Subregion: 1" + nl + "---Total Rooms: 1---" + nl + nl
            + "---HI---" + nl + "---Total Rooms: 0---" + nl + nl,
            RoomListExport.Format(regions));
    }
}
