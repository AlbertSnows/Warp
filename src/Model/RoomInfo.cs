#nullable enable
using System;

namespace Warp.Model;

/// <summary>
/// One room of a region, as read from the region's files. Never changed once made; the loader builds the finished
/// value in one go.
/// </summary>
/// <param name="Name">The full room name, for example "SU_A01" or "GATE_SU_HI".</param>
/// <param name="Type">What the world file calls the room.</param>
/// <param name="Cameras">How many screens the room has. At least 1, whatever the room file says.</param>
/// <param name="Subregion">The index into the region's subregion names. 0 is "no subregion".</param>
internal sealed record RoomInfo(
    string Name,
    RoomType Type = RoomType.Room,
    int Cameras = 1,
    int Subregion = 0)
{
    /// <summary>
    /// The name without the region prefix ("A01" for "SU_A01"), which is what a button shows. Null when the name has
    /// no underscore, and such a room gets no button.
    /// </summary>
    public string? ShortName
    {
        get
        {
            int underscore = Name.IndexOf('_');
            return underscore < 0 || underscore == Name.Length - 1 ? null : Name.Substring(underscore + 1);
        }
    }

    /// <summary>
    /// The number the room is grouped by under the given grouping: its type, camera count or subregion index.
    /// </summary>
    public int GroupKey(Grouping grouping) => grouping switch
    {
        Grouping.Type => (int)Type,
        Grouping.Size => Cameras,
        Grouping.Subregion => Subregion,
        _ => throw new ArgumentOutOfRangeException(nameof(grouping))
    };
}

/// <summary>
/// Everything known about one region: its rooms, in the order they were read, and its subregion names. Index 0 of
/// the names is the label for "no subregion".
/// </summary>
internal sealed record RegionData(string Region, RoomInfo[] Rooms, string[] SubregionNames)
{
    /// <summary> The name of subregion 0, which a room with no subregion belongs to, when the game gives none. </summary>
    public const string NoSubregion = "None";
}
