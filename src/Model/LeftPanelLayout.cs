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

    /// <summary>The height of the title line, which the region picker hangs from.</summary>
    public static float TitleY(float screenHeight) => screenHeight - 39f;

    /// <summary> The height of the STATS button, 72 under the top of the screen. </summary>
    public static float StatsButtonYFor(float screenHeight) => screenHeight - 72f;

    /// <summary>The height of the region dropdown, 65 under the title.</summary>
    public static float DropdownYFor(float screenHeight) => TitleY(screenHeight) - 65f;

    /// <summary> Works out where everything goes for the given state of the panel. </summary>
    public static LeftPanelLayout Compute(LeftPanelInput input)
    {
        float statsButtonY = StatsButtonYFor(input.ScreenHeight);
        float dropdownY = DropdownYFor(input.ScreenHeight);
        float dropOffset = DropOffsetFor(input);
        float filterY = dropdownY - (input.DropdownMode ? 55f : 65f) - dropOffset;
        float[] buttonRows = [.. Enumerable.Range(0, 3).Select(i => filterY - 35f - 30f * i)];
        float keyY = buttonRows[2] - 15f;
        float[] keyYs =
        [
            .. Enumerable.Range(0, input.ColorKeyEntries)
                .Select(i => keyY - (input.ColorKeyIsRow ? 15f : 15f + 15f * i))
        ];

        (float? subLabelY, float[] subYs) = SubregionList(input, keyYs);
        return new LeftPanelLayout(
            filterY, filterY + 15f, buttonRows, keyY, keyYs, subLabelY, subYs,
            input.HasStats ? StatsFor(input, statsButtonY, dropOffset, keyYs, subYs) : null);
    }

    /// <summary>
    /// The heights of the separate subregion list: its heading, 25 under the last line of the color key, and its
    /// lines, the first 20 under the heading and each next 15 lower. No heading and no lines when there is no list,
    /// or no key for it to hang from.
    /// </summary>
    private static (float? HeadingY, float[] LineYs) SubregionList(LeftPanelInput input, float[] keyYs)
    {
        bool hasList = input.SubregionEntries > 0 && keyYs.Length > 0;
        float headingY = hasList ? keyYs[keyYs.Length - 1] - 25f : 0f;
        float[] lineYs = hasList
            ? [.. Enumerable.Range(0, input.SubregionEntries).Select(i => headingY - 20f - 15f * i)]
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
        bool subregionsRunLow = subYs.Length > 0 && subYs[subYs.Length - 1] < 320f;
        if (buttonMode || subregionsRunLow)
        {
            float x = buttonMode ? (GridColumns(input.RegionButtons) == WideColumns ? 130f : 110f) : 160f;
            return new StatsPlacement(x, 0f, statsButtonY - 15f);
        }
        float lastKeyY = keyYs.Length > 0 ? keyYs[keyYs.Length - 1] : 0f;
        float textY = (subYs.Length > 0 ? subYs[subYs.Length - 1] : lastKeyY) - 20f;
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
        float inset = columns == 4 ? 17f : 0f;
        return (-55f - inset + 35f * (index % columns), -(35f + ButtonGridRowHeight * (index / columns)));
    }
}
