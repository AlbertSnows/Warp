#nullable enable
using System.Linq;

namespace Warp.Model;

/// <summary>What decides where the controls in the menu's left-hand panel go.</summary>
/// <param name="ScreenHeight">The height of the screen.</param>
/// <param name="DropdownMode">Whether regions are picked from a dropdown, and not from buttons.</param>
/// <param name="DropdownOpen">Whether the dropdown's list is open, which covers everything under it.</param>
/// <param name="RegionButtons">How many region buttons there are. Zero in dropdown mode.</param>
/// <param name="ColorKeyEntries">How many lines the color key has.</param>
/// <param name="ColorKeyIsRow">Whether the key lists its entries side by side (room sizes) and not as a column.</param>
/// <param name="SubregionEntries">How many lines the separate subregion list has. Zero when it is not shown.</param>
/// <param name="HasStats">Whether the stats panel is open.</param>
internal readonly record struct LeftPanelInput(
    float ScreenHeight,
    bool DropdownMode,
    bool DropdownOpen,
    int RegionButtons,
    int ColorKeyEntries,
    bool ColorKeyIsRow,
    int SubregionEntries,
    bool HasStats);

/// <summary>Where the stats text goes: a column beside the region buttons, or under the key.</summary>
internal readonly record struct StatsPlacement(float PanelX, float PanelY, float TextY);

/// <summary>
/// The vertical positions of everything in the left-hand panel, from the top: the title, the stats button, the region
/// picker, the three toggle buttons, the sort and view buttons, the color key, the subregion list and the stats. The
/// controls move as the picker changes height, and these functions work the positions out from that alone.
/// </summary>
internal sealed record LeftPanelLayout(
    float FilterLabelY,
    float ToggleY,
    float[] ButtonRowYs,
    float KeyLabelY,
    float[] ColorKeyYs,
    float? SubregionLabelY,
    float[] SubregionYs,
    StatsPlacement? Stats)
{
    /// <summary>How far down the dropdown's open list pushes the controls under it, out of sight.</summary>
    private const float OpenListOffset = 1500f;

    /// <summary> With this many regions or more, the region button grid gets a fourth column. </summary>
    private const int WideGridThreshold = 50;
    private const int WideColumns = 4;
    private const int NarrowColumns = 3;
    private const float ButtonGridRowHeight = 27f;

    // Gaps in the stack, in menu units, each measured from the thing above it
    private const float TitleBelowTop = 39f;
    private const float StatsButtonBelowTop = 72f;
    private const float DropdownBelowTitle = 65f;
    private const float FilterBelowDropdown = 55f;
    private const float FilterBelowButtonGrid = 65f;
    private const float TogglesAboveFilter = 15f;
    private const int ButtonRowCount = 3;
    private const float FirstButtonRowBelowFilter = 35f;
    private const float ButtonRowSpacing = 30f;
    private const float KeyBelowLastButtonRow = 15f;
    private const float KeyFirstLineBelowHeading = 15f;
    private const float KeyLineSpacing = 15f;
    private const float SubregionHeadingBelowKey = 25f;
    private const float SubregionFirstLineBelowHeading = 20f;
    private const float SubregionLineSpacing = 15f;

    // The stats text. Below this height the subregion list leaves no room under it, so the stats go beside the buttons
    private const float SubregionListLowLimit = 320f;
    private const float StatsTextBelowStatsButton = 15f;
    private const float StatsTextBelowLastLine = 20f;
    private const float StatsXBesideWideGrid = 130f;
    private const float StatsXBesideNarrowGrid = 110f;
    private const float StatsXBesideDropdown = 160f;

    // The region button grid, measured from the stats button
    private const float GridColumnSpacing = 35f;
    private const float GridFirstColumnLeft = 55f;
    private const float GridFirstRowBelow = 35f;
    private const float WideGridInset = 17f;

    /// <summary>The height of the title line, which the region picker hangs from.</summary>
    public static float TitleY(float screenHeight) => screenHeight - TitleBelowTop;

    /// <summary> The height of the STATS button. </summary>
    public static float StatsButtonYFor(float screenHeight) => screenHeight - StatsButtonBelowTop;

    /// <summary>The height of the region dropdown, under the title.</summary>
    public static float DropdownYFor(float screenHeight) => TitleY(screenHeight) - DropdownBelowTitle;

    /// <summary> Works out where everything goes for the given state of the panel. </summary>
    public static LeftPanelLayout Compute(LeftPanelInput input)
    {
        float statsButtonY = StatsButtonYFor(input.ScreenHeight);
        float dropdownY = DropdownYFor(input.ScreenHeight);
        float dropOffset = DropOffsetFor(input);
        float filterY = dropdownY - (input.DropdownMode ? FilterBelowDropdown : FilterBelowButtonGrid) - dropOffset;
        float[] buttonRows =
        [
            .. Enumerable.Range(0, ButtonRowCount)
                .Select(i => filterY - FirstButtonRowBelowFilter - ButtonRowSpacing * i)
        ];
        float keyY = buttonRows[ButtonRowCount - 1] - KeyBelowLastButtonRow;
        float[] keyYs =
        [
            .. Enumerable.Range(0, input.ColorKeyEntries)
                .Select(i => keyY - KeyFirstLineBelowHeading - (input.ColorKeyIsRow ? 0f : KeyLineSpacing * i))
        ];

        (float? subLabelY, float[] subYs) = SubregionList(input, keyYs);
        return new LeftPanelLayout(
            filterY, filterY + TogglesAboveFilter, buttonRows, keyY, keyYs, subLabelY, subYs,
            input.HasStats ? StatsFor(input, statsButtonY, dropOffset, keyYs, subYs) : null);
    }

    /// <summary>
    /// The heights of the separate subregion list: its heading, under the last line of the color key, and its lines,
    /// the first under the heading and each next one lower. No heading and no lines when there is no list, or no key
    /// for it to hang from.
    /// </summary>
    private static (float? HeadingY, float[] LineYs) SubregionList(LeftPanelInput input, float[] keyYs)
    {
        bool hasList = input.SubregionEntries > 0 && keyYs.Length > 0;
        float headingY = hasList ? keyYs[keyYs.Length - 1] - SubregionHeadingBelowKey : 0f;
        float[] lineYs = hasList
            ? [.. Enumerable.Range(0, input.SubregionEntries)
                .Select(i => headingY - SubregionFirstLineBelowHeading - SubregionLineSpacing * i)]
            : [];
        return (hasList ? headingY : null, lineYs);
    }

    /// <summary>
    /// How far the controls under the region picker are pushed down: not at all for a closed dropdown, far off screen
    /// for an open one, and by the height of the button grid in button mode.
    /// </summary>
    private static float DropOffsetFor(LeftPanelInput input)
    {
        if (input.DropdownMode)
        {
            return input.DropdownOpen ? OpenListOffset : 0f;
        }
        return ButtonGridRowHeight * LastRow(input.RegionButtons) - 5f;
    }

    /// <summary>
    /// Where the stats text goes: in a column beside the region buttons, or under the key if the key leaves room.
    /// </summary>
    private static StatsPlacement StatsFor(
        LeftPanelInput input, float statsButtonY, float dropOffset, float[] keyYs, float[] subYs)
    {
        bool buttonMode = !input.DropdownMode;
        bool subregionsRunLow = subYs.Length > 0 && subYs[subYs.Length - 1] < SubregionListLowLimit;
        if (buttonMode || subregionsRunLow)
        {
            float x = buttonMode
                ? (GridColumns(input.RegionButtons) == WideColumns ? StatsXBesideWideGrid : StatsXBesideNarrowGrid)
                : StatsXBesideDropdown;
            return new StatsPlacement(x, 0f, statsButtonY - StatsTextBelowStatsButton);
        }
        float lastKeyY = keyYs.Length > 0 ? keyYs[keyYs.Length - 1] : 0f;
        float textY = (subYs.Length > 0 ? subYs[subYs.Length - 1] : lastKeyY) - StatsTextBelowLastLine;
        return new StatsPlacement(0f, -dropOffset, textY);
    }

    /// <summary>How many columns the region button grid has. A crowded install gets one more.</summary>
    public static int GridColumns(int regionCount) => regionCount >= WideGridThreshold ? WideColumns : NarrowColumns;

    /// <summary> The index of the last row of the region button grid, which is what decides its height. </summary>
    public static int LastRow(int buttonCount) => buttonCount == 0 ? 0 : (buttonCount - 1) / GridColumns(buttonCount);

    /// <summary>
    /// Where region button <paramref name="index"/> goes, as an offset from the stats button. The buttons fill the
    /// grid row by row.
    /// </summary>
    public static (float X, float Y) RegionButtonOffset(int index, int regionCount)
    {
        int columns = GridColumns(regionCount);
        float inset = columns == WideColumns ? WideGridInset : 0f;
        return (
            -GridFirstColumnLeft - inset + GridColumnSpacing * (index % columns),
            -(GridFirstRowBelow + ButtonGridRowHeight * (index / columns)));
    }
}
