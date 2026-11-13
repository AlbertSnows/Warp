#nullable enable
using System.Collections.Generic;

namespace Warp.Model;

/// <summary>
/// One piece of a room icon: a filled rectangle, turned about its own center.
/// </summary>
/// <param name="X">How far right of the icon's center the piece's center is, in pixels.</param>
/// <param name="Y">How far above the icon's center the piece's center is, in pixels.</param>
/// <param name="Width">The width before turning, in pixels.</param>
/// <param name="Height">The height before turning, in pixels.</param>
/// <param name="Rotation">The turn in degrees, clockwise.</param>
internal readonly record struct IconPart(float X, float Y, float Width, float Height, float Rotation = 0f);

/// <summary>
/// The small mark on a room's button that shows its type, as a few rectangles so it can be drawn from the game's plain
/// white pixel without any picture. An icon fits in a box about 10 pixels square.
/// </summary>
internal static class RoomIcons
{
    /// <summary>The icon's size, in pixels, and so the room it needs on a button.</summary>
    public const float Size = 10f;

    private static readonly IReadOnlyList<IconPart> _none = [];

    private static readonly IReadOnlyList<IconPart> _gate = [new(-3f, 0f, 2f, 10f), new(3f, 0f, 2f, 10f)];

    private static readonly IReadOnlyList<IconPart> _shelter = [new(0f, 0f, 7f, 7f, 45f)];

    private static readonly IReadOnlyList<IconPart> _swarm =
        [new(-3.5f, 2f, 3f, 3f), new(3.5f, 2f, 3f, 3f), new(0f, -3f, 3f, 3f)];

    private static readonly IReadOnlyList<IconPart> _trader = [new(0f, 4f, 10f, 2f), new(0f, -1f, 2f, 8f)];

    private static readonly IReadOnlyList<IconPart> _outpost = [new(-2.5f, 0f, 2f, 10f), new(2f, 3.5f, 5f, 4f)];

    /// <summary>The parts of a type's icon. An ordinary room has none.</summary>
    public static IReadOnlyList<IconPart> For(RoomType type) => type switch
    {
        RoomType.Gate => _gate,
        RoomType.Shelter => _shelter,
        RoomType.SwarmRoom => _swarm,
        RoomType.ScavTrader => _trader,
        RoomType.ScavOutpost => _outpost,
        _ => _none
    };
}
