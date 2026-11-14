namespace Warp.Model;

/// <summary>
/// What kind of room a world file lists. The order is the order of the type colors, so a value doubles as an index
/// into the palette.
/// Both enums are in one file because both are saved or used as an index by number: a room type is its place in the
/// palette, and a grouping is a digit in the settings file. A change to either is a change to saved data and should be
/// seen together.
/// </summary>
internal enum RoomType
{
    Room,
    Gate,
    Shelter,
    SwarmRoom,
    ScavTrader,
    ScavOutpost
}

/// <summary>
/// The three ways the room list can be grouped: the sort order, the color scheme and the color editor's category all
/// use the same three choices. The numbers are saved in the settings file, so the order must not change.
/// </summary>
internal enum Grouping
{
    Type,
    Size,
    Subregion
}
