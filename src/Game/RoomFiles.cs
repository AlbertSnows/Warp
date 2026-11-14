namespace Warp.Game;

/// <summary>
/// Where a room's file is, as the game itself finds it. A file of its own so that reading a room's camera count and
/// reading its tiles for the preview find the file the same way, and neither has to depend on the other.
/// </summary>
internal static class RoomFiles
{
    /// <summary>
    /// The path of the room's text file, or null if there is none. This is the game's own lookup, so it covers every
    /// place the game does (region folders, gates, shelters, challenge levels) and any a mod adds.
    /// </summary>
    public static string Find(string room) => WorldLoader.FindRoomFile(room, false, ".txt");
}
