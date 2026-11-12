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
    float StatsButtonY,
    float DropdownY,
    float DropOffset,
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
    public const float OpenListOffset = 1500f;

    public const int WideGridThreshold = 50;
    private const float ButtonGridRowHeight = 27f;

    /// <summary>The height of the title line, which the region picker hangs from.</summary>
    public static float TitleY(float screenHeight) => screenHeight - 39f;

    public static float StatsButtonYFor(float screenHeight) => screenHeight - 72f;

    /// <summary>The height of the region dropdown, 65 under the title.</summary>
    public static float DropdownYFor(float screenHeight) => TitleY(screenHeight) - 65f;

    public static LeftPanelLayout Compute(LeftPanelInput input)
    {
        var statsButtonY = StatsButtonYFor(input.ScreenHeight);
        var dropdownY = DropdownYFor(input.ScreenHeight);
        var dropOffset = DropOffsetFor(input);
        var filterY = dropdownY - (input.DropdownMode ? 55f : 65f) - dropOffset;
        var buttonRows = Enumerable.Range(0, 3).Select(i => filterY - 35f - 30f * i).ToArray();
        var keyY = buttonRows[2] - 15f;
        var keyYs = Enumerable.Range(0, input.ColorKeyEntries)
            .Select(i => keyY - (input.ColorKeyIsRow ? 15f : 15f + 15f * i)).ToArray();

        float? subLabelY = null;
        var subYs = new float[0];
        if (input.SubregionEntries > 0 && keyYs.Length > 0)
        {
            subLabelY = keyYs[keyYs.Length - 1] - 25f;
            subYs = Enumerable.Range(0, input.SubregionEntries).Select(i => subLabelY.Value - 20f - 15f * i).ToArray();
        }

        return new LeftPanelLayout(
            statsButtonY, dropdownY, dropOffset, filterY, filterY + 15f, buttonRows, keyY, keyYs, subLabelY, subYs,
            input.HasStats ? StatsFor(input, statsButtonY, dropOffset, keyYs, subYs) : null);
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

    private static StatsPlacement StatsFor(
        LeftPanelInput input, float statsButtonY, float dropOffset, float[] keyYs, float[] subYs)
    {
        var buttonMode = !input.DropdownMode;
        var subregionsRunLow = subYs.Length > 0 && subYs[subYs.Length - 1] < 320f;
        if (buttonMode || subregionsRunLow)
        {
            var x = buttonMode ? (input.RegionButtons > WideGridThreshold ? 130f : 110f) : 160f;
            return new StatsPlacement(x, 0f, statsButtonY - 15f);
        }
        var lastKeyY = keyYs.Length > 0 ? keyYs[keyYs.Length - 1] : 0f;
        var textY = (subYs.Length > 0 ? subYs[subYs.Length - 1] : lastKeyY) - 20f;
        return new StatsPlacement(0f, -dropOffset, textY);
    }

    /// <summary>How many columns the region button grid has. A crowded install gets one more.</summary>
    public static int GridColumns(int regionCount) => regionCount >= WideGridThreshold ? 4 : 3;

    public static int LastRow(int buttonCount) => buttonCount == 0 ? 0 : (buttonCount - 1) / GridColumns(buttonCount);

    /// <summary>
    /// Where region button <paramref name="index"/> goes, as an offset from the stats button. The buttons fill the
    /// grid row by row.
    /// </summary>
    public static (float X, float Y) RegionButtonOffset(int index, int regionCount)
    {
        var columns = GridColumns(regionCount);
        var inset = columns == 4 ? 17f : 0f;
        return (-55f - inset + 35f * (index % columns), -(35f + ButtonGridRowHeight * (index / columns)));
    }
}
