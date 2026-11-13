#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Warp.Model;

/// <summary>The text of the room list file that F5 and F6 write.</summary>
internal static class RoomListExport
{
    /// <summary>
    /// A listing of every region that has been read: one line for each room with its camera count and subregion,
    /// then the number of rooms in the region.
    /// </summary>
    public static string Format(IEnumerable<KeyValuePair<string, RoomInfo[]>> regions)
    {
        var text = new StringBuilder();
        text.AppendLine("---WARP Room List Export---");
        text.AppendLine();
        foreach (KeyValuePair<string, RoomInfo[]> region in regions)
        {
            text.Append("---" + region.Key + "---");
            foreach (RoomInfo room in region.Value)
            {
                text.AppendLine();
                text.Append(room.Name + ": | Cameras: " + room.Cameras + ", Subregion: " + room.Subregion);
            }
            text.AppendLine();
            text.Append("---Total Rooms: " + region.Value.Length + "---");
            text.AppendLine();
            text.AppendLine();
        }
        return text.ToString();
    }
}
