#nullable enable
namespace Warp.Model;

/// <summary>A tile position in a room.</summary>
internal readonly record struct TileCoord(int X, int Y);

/// <summary>
/// A warp the player has asked for and the game has not yet carried out. It is made when a room button is clicked and
/// is gone once the players have arrived or the warp failed.
/// </summary>
/// <param name="Region">The region the room is in, which may be another than the one the players are in.</param>
/// <param name="Room">The room to go to, for example "SU_A01".</param>
/// <param name="Tile">
/// Where in the room to put the players, set when the player clicked a spot in the room preview. Null to use the
/// room's first entrance.
/// </param>
internal sealed record WarpRequest(string Region, string Room, TileCoord? Tile = null);
