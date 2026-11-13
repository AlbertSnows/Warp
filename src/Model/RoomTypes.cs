#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>
/// Everything the menu says and reads about one room type, in one place.
/// </summary>
/// <param name="Type">The type this describes.</param>
/// <param name="WorldTag">The tag the world file uses for it. Empty for an ordinary room, which has none.</param>
/// <param name="Heading">The heading over a column of rooms of this type, which is also its translation key.</param>
/// <param name="KeyLabel">The line in the color key, which is also its translation key.</param>
/// <param name="EditorName">The name shown in the color editor when the type's color is selected.</param>
internal sealed record RoomTypeInfo(RoomType Type, string WorldTag, string Heading, string KeyLabel, string EditorName);

/// <summary>
/// The table of room types. The order is the order of <see cref="RoomType"/>, so a type is also its index here and in
/// the palette; a test checks it.
/// </summary>
internal static class RoomTypes
{
    public static readonly IReadOnlyList<RoomTypeInfo> All =
    [
        new(RoomType.Room, "", "ROOM", "Room", "Room"),
        new(RoomType.Gate, "GATE", "GATE", "Gate", "Gate"),
        new(RoomType.Shelter, "SHELTER", "SHELTER", "Shelter", "Shelter"),
        new(RoomType.SwarmRoom, "SWARMROOM", "SWARM", "SwarmRoom", "Swarmroom"),
        new(RoomType.ScavTrader, "SCAVTRADER", "TRADER", "ScavTrader", "Scavenger Trader"),
        new(RoomType.ScavOutpost, "SCAVOUTPOST", "OUTPOST", "ScavOutpost", "Scavenger Outpost")
    ];

    /// <summary> The description of a type. </summary>
    public static RoomTypeInfo Of(RoomType type) => All[(int)type];

    /// <summary> The type a world file tag stands for. A missing or unknown tag is an ordinary room. </summary>
    public static RoomType FromWorldTag(string tag)
        => All.FirstOrDefault(info => info.WorldTag.Length > 0 && info.WorldTag == tag)?.Type ?? RoomType.Room;
}
