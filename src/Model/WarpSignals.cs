#nullable enable
using System;

namespace Warp.Model;

/// <summary>
/// The messages Warp's buttons send. A button sends its message up through its owners to the pause menu, where every
/// mod's hook can see it. So a part of the menu that handles a message does not pass it on, and the two messages the
/// pause menu hook itself reads carry a prefix of their own, so no other mod's message can be taken for them. The
/// rest are named here too, so a part that makes a button and the part that reads its message cannot disagree, and each
/// can be read by a function that is tested.
/// </summary>
internal static class WarpSignals
{
    private const string Prefix = "warp:";
    private const string RoomPrefix = Prefix + "room:";

    /// <summary> The message of the pause menu's HIDE / WARP button. </summary>
    public const string Toggle = Prefix + "toggle";

    /// <summary> The message of the button for a room. </summary>
    public static string ForRoom(string room) => RoomPrefix + room;

    /// <summary> The room a message is for, or null if it is not a room button's message. </summary>
    public static string? RoomOf(string message) => After(message, RoomPrefix);

    // ---- Messages that stay inside the Warp menu ------------------------------------------------------------------

    /// <summary> The LEFT button of the room grid. </summary>
    public const string PageLeft = "PAGELEFT";

    /// <summary> The RIGHT button of the room grid. </summary>
    public const string PageRight = "PAGERIGHT";

    /// <summary> The STATS button. </summary>
    public const string Stats = "STATS";

    /// <summary> The favourite button. </summary>
    public const string Favourite = "FAV";

    /// <summary> The button that switches between the dropdown and the region buttons. </summary>
    public const string ToggleRegionList = "TOGGLE";

    /// <summary> The button that opens the color editor. </summary>
    public const string Colors = "COLORS";

    /// <summary> The DONE button of the color editor. </summary>
    public const string EditorDone = "DONE";

    /// <summary> The SAVE button of the color editor. </summary>
    public const string EditorSave = "SAVE";

    private const string RegionPrefix = "reg-";
    private const string SortPrefix = "S";
    private const string ViewPrefix = "V";
    private const string CategoryPrefix = "C";
    private const string SwatchPrefix = "X";

    /// <summary> The message of the button for a region. </summary>
    public static string ForRegion(string region) => RegionPrefix + region;

    /// <summary> The region a message is for, or null if it is not a region button's message. </summary>
    public static string? RegionOf(string message) => After(message, RegionPrefix);

    /// <summary> The message of the sort button for a grouping. </summary>
    public static string ForSort(Grouping grouping) => SortPrefix + grouping.Code();

    /// <summary> The message of the view button for a grouping. </summary>
    public static string ForView(Grouping grouping) => ViewPrefix + grouping.Code();

    /// <summary> A sort or view button's choice: which of the two, and the grouping. </summary>
    public readonly record struct GroupingChoice(bool Sorting, Grouping Grouping);

    /// <summary> The choice a sort or view button's message stands for, or null if it is neither. </summary>
    public static GroupingChoice? GroupingChoiceOf(string message)
    {
        if (message.Length < 2)
        {
            return null;
        }
        bool sorting = message.StartsWith(SortPrefix, StringComparison.Ordinal);
        if (!sorting && !message.StartsWith(ViewPrefix, StringComparison.Ordinal))
        {
            return null;
        }
        return Groupings.FromCode(message.Substring(1)) is { } grouping ? new GroupingChoice(sorting, grouping) : null;
    }

    /// <summary> The message of a category button of the color editor. </summary>
    public static string ForCategory(Grouping grouping) => CategoryPrefix + grouping.Code();

    /// <summary> The category a message stands for, or null if it is not a category button's. </summary>
    public static Grouping? CategoryOf(string message) => After(message, CategoryPrefix) is { } code && Groupings.FromCode(code) is { } grouping ? grouping : null;

    /// <summary> The message of a color swatch of the color editor, given its index. </summary>
    public static string ForSwatch(int index) => SwatchPrefix + index;

    /// <summary> The swatch a message stands for, or null if it is not a swatch's message. </summary>
    public static int? SwatchOf(string message)
    {
        return After(message, SwatchPrefix) is { } digits
               && int.TryParse(digits, System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture, out int index)
            ? index
            : null;
    }

    /// <summary> What comes after the prefix, or null if the message does not start with it or has nothing after it. </summary>
    private static string? After(string message, string prefix)
    {
        return message.StartsWith(prefix, StringComparison.Ordinal) && message.Length > prefix.Length
            ? message.Substring(prefix.Length)
            : null;
    }
}
