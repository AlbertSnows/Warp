using System.Linq;
using Warp.Model;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for where things go: the room grid and the left-hand panel.</summary>
internal static class LayoutTests
{
    public static void Run()
    {
        Group(nameof(GridTests), GridTests);
        Group(nameof(GridGroupTests), GridGroupTests);
        Group(nameof(GridPageTests), GridPageTests);
        Group(nameof(LeftPanelDropdownTests), LeftPanelDropdownTests);
        Group(nameof(LeftPanelButtonTests), LeftPanelButtonTests);
        Group(nameof(RegionButtonTests), RegionButtonTests);
    }

    private static RoomInfo[] Rooms(int count, RoomType type = RoomType.Room, string prefix = "SU_R") =>
        [.. Enumerable.Range(0, count).Select(i => new RoomInfo(prefix + i.ToString("000"), type))];

    private static void GridTests()
    {
        var layout = RoomGridLayout.Compute(Rooms(3), Grouping.Type, 1000f, 700f);
        CheckSequence("a column runs down from the top right",
            [(920f, 620f), (920f, 590f), (920f, 560f)], layout.Buttons.Select(b => (b.Spot.X, b.Spot.Y)));
        CheckSequence("the heading sits above the column", [(950f, 655f)], layout.Labels.Select(l => (l.Spot.X, l.Spot.Y)));
        CheckSequence("buttons point back at their rooms", [0, 1, 2], layout.Buttons.Select(b => b.RoomIndex));
        Check("a short list needs no second page", !layout.HasSecondPage);

        var empty = RoomGridLayout.Compute([], Grouping.Type, 1000f, 700f);
        Check("no rooms, no buttons or headings", empty.Buttons.Length == 0 && empty.Labels.Length == 0);
    }

    private static void GridGroupTests()
    {
        RoomInfo[] rooms = [.. Rooms(2), .. Rooms(1, RoomType.Shelter, "SU_S")];
        var layout = RoomGridLayout.Compute(rooms, Grouping.Type, 1000f, 700f);
        CheckSequence("a new group starts a new column, pulled in by 5",
            [(920f, 620f), (920f, 590f), (850f, 620f)], layout.Buttons.Select(b => (b.Spot.X, b.Spot.Y)));
        CheckSequence("each group gets a heading with its key",
            [((int)RoomType.Room, 950f), ((int)RoomType.Shelter, 880f)],
            layout.Labels.Select(l => (l.GroupKey, l.Spot.X)));

        var tall = RoomGridLayout.Compute(Rooms(22), Grouping.Type, 1000f, 700f);
        CheckEqual("21 buttons fill a column", (920f, 20), (tall.Buttons[0].Spot.X, (int)((620f - tall.Buttons[20].Spot.Y) / 30f)));
        CheckEqual("the 22nd starts the next column, pulled in by 10", (855f, 620f),
            (tall.Buttons[21].Spot.X, tall.Buttons[21].Spot.Y));

        // A group that fills its column exactly leaves the next group at the top of a fresh column, which only the
        // subregion grouping nudges out by 5
        RoomInfo[] afterFullColumn =
        [
            .. Enumerable.Range(0, 21).Select(i => new RoomInfo("SU_R" + i)),
            new("SU_X", RoomType.Shelter, Subregion: 1)
        ];
        var bySubregion = RoomGridLayout.Compute(afterFullColumn, Grouping.Subregion, 1000f, 700f);
        CheckEqual("after a full column, subregions nudge the next group out by 5", (880f, 850f),
            (bySubregion.Labels[1].Spot.X, bySubregion.Buttons[21].Spot.X));
        var byType = RoomGridLayout.Compute(afterFullColumn, Grouping.Type, 1000f, 700f);
        CheckEqual("and the other groupings do not", (885f, 855f), (byType.Labels[1].Spot.X, byType.Buttons[21].Spot.X));
    }

    private static void GridPageTests()
    {
        var layout = RoomGridLayout.Compute(Rooms(21 * 11 + 1), Grouping.Type, 1000f, 700f);
        Check("a long list needs a second page", layout.HasSecondPage);
        CheckEqual("the first button is on the first page", 0, layout.Buttons.First().Spot.Page);
        CheckEqual("the last, past the left limit, is on the second", 1, layout.Buttons.Last().Spot.Page);
        Check("pages split where the grid reaches the control panel",
            layout.Buttons.All(b => b.Spot.Page == (b.Spot.X < RoomGridLayout.PageEdge ? 1 : 0)));

        CheckEqual("page 0 shows where it was laid out", 0f, RoomGridLayout.ShiftFor(0, 0));
        CheckEqual("page 1 is off to the left while page 0 shows", -2000f, RoomGridLayout.ShiftFor(1, 0));
        CheckEqual("page 1 is slid into view when it shows", 1042f, RoomGridLayout.ShiftFor(1, 1));
        CheckEqual("page 0 is off to the right while page 1 shows", 3042f, RoomGridLayout.ShiftFor(0, 1));
    }

    private static LeftPanelInput Input(float height = 800f, bool dropdown = true, bool open = false, int regions = 0,
        int keys = 3, bool row = false, int subregions = 0, bool stats = false) =>
        new(height, dropdown, open, regions, keys, row, subregions, stats);

    private static void LeftPanelDropdownTests()
    {
        var layout = LeftPanelLayout.Compute(Input());
        CheckEqual("the stats button is near the top", 728f, LeftPanelLayout.StatsButtonYFor(800f));
        CheckEqual("the filter row sits under the dropdown", 641f, layout.FilterLabelY);
        CheckEqual("the toggles sit above the filter label", 656f, layout.ToggleY);
        CheckSequence("sort and view rows are 30 apart", [606f, 576f, 546f], layout.ButtonRowYs);
        CheckEqual("the key label is under the last row", 531f, layout.KeyLabelY);
        CheckSequence("key lines fall in a column", [516f, 501f, 486f], layout.ColorKeyYs);
        CheckSequence("size keys run side by side", [516f, 516f, 516f],
            LeftPanelLayout.Compute(Input(row: true)).ColorKeyYs);
        Check("no subregion list unless asked for", layout.SubregionLabelY == null && layout.SubregionYs.Length == 0);

        var open = LeftPanelLayout.Compute(Input(open: true));
        CheckEqual("an open dropdown pushes the controls out of sight", 641f - 1500f, open.FilterLabelY);

        var withSubregions = LeftPanelLayout.Compute(Input(subregions: 2, stats: true));
        CheckEqual("the subregion list hangs under the key", 461f, withSubregions.SubregionLabelY);
        CheckSequence("its lines are 15 apart", [441f, 426f], withSubregions.SubregionYs);
        CheckEqual("stats go under the list when there is room", new StatsPlacement(0f, 0f, 406f), withSubregions.Stats);

        var crowded = LeftPanelLayout.Compute(Input(height: 600f, subregions: 2, stats: true));
        CheckEqual("stats move to a column on the side when the list runs low", new StatsPlacement(160f, 0f, 513f),
            crowded.Stats);
        CheckEqual("no stats panel, no placement", null, LeftPanelLayout.Compute(Input()).Stats);
    }

    private static void LeftPanelButtonTests()
    {
        var layout = LeftPanelLayout.Compute(Input(dropdown: false, regions: 60, stats: true));
        CheckEqual("the region grid pushes the controls down by its height", 696f - 65f - (27f * 14f - 5f), layout.FilterLabelY);
        CheckEqual("stats sit beside a wide grid", new StatsPlacement(130f, 0f, 713f), layout.Stats);
        CheckEqual("and closer beside a narrower one", new StatsPlacement(110f, 0f, 713f),
            LeftPanelLayout.Compute(Input(dropdown: false, regions: 30, stats: true)).Stats);
    }

    private static void RegionButtonTests()
    {
        CheckEqual("three columns for a normal install", 3, LeftPanelLayout.GridColumns(20));
        CheckEqual("49 regions still have three columns", 3, LeftPanelLayout.GridColumns(49));
        CheckEqual("50 regions have four", 4, LeftPanelLayout.GridColumns(50));
        CheckEqual("and so do 51", 4, LeftPanelLayout.GridColumns(51));
        CheckEqual("stats sit for three columns at 49 regions", new StatsPlacement(110f, 0f, 713f),
            LeftPanelLayout.Compute(Input(dropdown: false, regions: 49, stats: true)).Stats);
        CheckEqual("and for four columns at exactly 50", new StatsPlacement(130f, 0f, 713f),
            LeftPanelLayout.Compute(Input(dropdown: false, regions: 50, stats: true)).Stats);
        CheckEqual("four for a crowded one", 4, LeftPanelLayout.GridColumns(50));
        CheckEqual("the first button", (-55f, -35f), LeftPanelLayout.RegionButtonOffset(0, 20));
        CheckEqual("buttons fill rows left to right", (-55f + 35f * 2, -35f), LeftPanelLayout.RegionButtonOffset(2, 20));
        CheckEqual("and then the next row down", (-55f, -62f), LeftPanelLayout.RegionButtonOffset(3, 20));
        CheckEqual("a four column grid is inset", (-72f + 35f, -62f), LeftPanelLayout.RegionButtonOffset(5, 60));
        CheckEqual("the last row of nothing is the first", 0, LeftPanelLayout.LastRow(0));
        CheckEqual("seven buttons in three columns end on row 2", 2, LeftPanelLayout.LastRow(7));
    }
}
