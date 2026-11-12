#nullable enable
using System.Collections.Generic;

namespace Warp.Model;

/// <summary>A room button's place in the grid. Page 1 holds the columns that would not fit on screen.</summary>
internal readonly record struct PlacedButton(int RoomIndex, float X, float Y, int Page);

/// <summary>The heading above a run of rooms that share a group, such as "SHELTER" or "SUB 2".</summary>
internal readonly record struct PlacedLabel(int GroupKey, float X, float Y, int Page);

/// <summary>
/// Where the room buttons and group headings go, worked out from the sorted rooms alone. The grid fills columns
/// from the right edge of the screen to the left, 21 buttons to a column. A new group starts a new column. Columns
/// that would run into the left-hand control panel go on a second page.
/// </summary>
internal sealed record RoomGridLayout(PlacedButton[] Buttons, PlacedLabel[] Labels)
{
    /// <summary>Whether some columns did not fit, so the menu needs page buttons.</summary>
    public bool HasSecondPage
    {
        get
        {
            foreach (var button in Buttons)
            {
                if (button.Page == 1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The left edge of the room area. A button further left than this goes on the second page.</summary>
    public const float PageEdge = 250f;

    /// <summary>
    /// Page 1 is laid out at the left of the screen, where it would be hidden behind the control panel. It is shown
    /// by moving it right by this much.
    /// </summary>
    public const float ShownShift = 1042f;

    /// <summary>Page 1 while page 0 is showing: moved off the left edge.</summary>
    public const float HiddenLeft = -2000f;

    /// <summary>Page 0 while page 1 is showing: moved off the right edge.</summary>
    public const float HiddenRight = 3042f;

    private const float ButtonTop = 80f;
    private const float LabelTop = 45f;
    private const float ColumnWidth = 75f;
    private const float RowHeight = 30f;
    private const int RowsPerColumn = 21;

    /// <summary>
    /// Lays out the rooms, which must already be in display order. The screen width is the usable width, without the
    /// margin on the right. A room without a short name has no button, but still opens a new group heading.
    /// </summary>
    public static RoomGridLayout Compute(
        IReadOnlyList<RoomInfo> rooms, Grouping grouping, float screenWidth, float screenHeight)
    {
        var buttons = new List<PlacedButton>();
        var labels = new List<PlacedLabel>();
        var column = 0;
        var row = 0;
        var shift = 0f;
        int? group = null;

        for (var i = 0; i < rooms.Count; i++)
        {
            var key = rooms[i].GroupKey(grouping);
            if (group != key)
            {
                if (group != null)
                {
                    (column, shift) = StartGroup(grouping, row, column, shift);
                    row = 0;
                }
                group = key;
                labels.Add(Place(new PlacedLabel(key, screenWidth - (50f + shift) - ColumnWidth * column,
                    screenHeight - LabelTop, 0)));
            }
            if (rooms[i].ShortName == null)
            {
                continue;
            }
            var x = screenWidth - (80f + shift) - ColumnWidth * column;
            var y = screenHeight - ButtonTop - RowHeight * row;
            buttons.Add(Place(new PlacedButton(i, x, y, 0)));
            if (row < RowsPerColumn - 1)
            {
                row++;
            }
            else
            {
                column++;
                row = 0;
                shift -= 10f;
            }
        }
        return new RoomGridLayout(buttons.ToArray(), labels.ToArray());
    }

    /// <summary>
    /// How the column position changes when a new group starts. A group that left a column part filled moves on to
    /// the next column and pulls the grid 5 units closer. The subregion grouping alone pushes the grid 5 units
    /// further apart when the previous group left no column part filled.
    /// </summary>
    private static (int Column, float Shift) StartGroup(Grouping grouping, int row, int column, float shift)
    {
        if (row > 0)
        {
            return (column + 1, shift - 5f);
        }
        return grouping == Grouping.Subregion ? (column, shift + 5f) : (column, shift);
    }

    private static PlacedButton Place(PlacedButton button) => button with { Page = button.X < PageEdge ? 1 : 0 };

    private static PlacedLabel Place(PlacedLabel label) => label with { Page = label.X < PageEdge ? 1 : 0 };

    /// <summary>
    /// The horizontal shift that puts something on page <paramref name="itemPage"/> in the right place while
    /// <paramref name="shownPage"/> is showing. Page 0 shows where it was laid out. Showing page 1 slides its columns
    /// from the left of the screen to the right and sends page 0 off to the right.
    /// </summary>
    public static float ShiftFor(int itemPage, int shownPage)
    {
        if (itemPage == shownPage)
        {
            return shownPage == 0 ? 0f : ShownShift;
        }
        return shownPage == 0 ? HiddenLeft : HiddenRight;
    }
}
