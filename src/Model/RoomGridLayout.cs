#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>
/// A place in the grid. Page 1 holds the columns that would not fit on screen: a spot further left than
/// <see cref="RoomGridLayout.PageEdge"/> is on it.
/// </summary>
internal readonly record struct GridSpot(float X, float Y, int Page)
{
    /// <summary> The spot at this position, with the page its position puts it on. </summary>
    public static GridSpot At(float x, float y) => new(x, y, x < RoomGridLayout.PageEdge ? 1 : 0);
}

/// <summary>A room button's place in the grid.</summary>
internal readonly record struct PlacedButton(int RoomIndex, GridSpot Spot);

/// <summary>The heading above a run of rooms that share a group, such as "SHELTER" or "SUB 2".</summary>
internal readonly record struct PlacedLabel(int GroupKey, GridSpot Spot);

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
            return Buttons.Any(button => button.Spot.Page == 1);
        }
    }

    /// <summary>The left edge of the room area. A button further left than this goes on the second page.</summary>
    public const float PageEdge = 250f;

    /// <summary>
    /// Page 1 is laid out at the left of the screen, where it would be hidden behind the control panel. It is shown
    /// by moving it right by this much.
    /// </summary>
    private const float ShownShift = 1042f;

    /// <summary>Page 1 while page 0 is showing: moved off the left edge.</summary>
    private const float HiddenLeft = -2000f;

    /// <summary>Page 0 while page 1 is showing: moved off the right edge.</summary>
    private const float HiddenRight = 3042f;

    private const float ButtonTop = 80f;
    private const float LabelTop = 45f;
    private const float ColumnWidth = 75f;
    private const float RowHeight = 30f;
    private const int RowsPerColumn = 21;

    /// <summary>
    /// Lays out the rooms, which must already be in display order. The screen width is the usable width, without the
    /// margin on the right. Every room gets a button.
    /// </summary>
    public static RoomGridLayout Compute(
        IReadOnlyList<RoomInfo> rooms, Grouping grouping, float screenWidth, float screenHeight)
    {
        var buttons = new List<PlacedButton>();
        var labels = new List<PlacedLabel>();
        int column = 0;
        int row = 0;
        float shift = 0f;
        int? group = null;

        for (int i = 0; i < rooms.Count; i++)
        {
            int key = rooms[i].GroupKey(grouping);
            if (group != key)
            {
                if (group != null)
                {
                    (column, shift) = StartGroup(grouping, row, column, shift);
                    row = 0;
                }
                group = key;
                labels.Add(new PlacedLabel(key, GridSpot.At(screenWidth - (50f + shift) - ColumnWidth * column, screenHeight - LabelTop)));
            }
            float x = screenWidth - (80f + shift) - ColumnWidth * column;
            float y = screenHeight - ButtonTop - RowHeight * row;
            buttons.Add(new PlacedButton(i, GridSpot.At(x, y)));
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
        return new RoomGridLayout([.. buttons], [.. labels]);
    }

    /// <summary>
    /// How the column position changes when a new group starts. A group that left a column part filled moves on to
    /// the next column and pulls the grid 5 units closer. The subregion grouping alone pushes the grid 5 units
    /// further apart when the previous group filled its column exactly, so the next group starts at the top of a fresh one.
    /// </summary>
    private static (int Column, float Shift) StartGroup(Grouping grouping, int row, int column, float shift)
    {
        if (row > 0)
        {
            return (column + 1, shift - 5f);
        }
        return grouping == Grouping.Subregion ? (column, shift + 5f) : (column, shift);
    }

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
