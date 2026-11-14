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
        var builder = new StringBuilder();
        builder.AppendLine("---WARP Room List Export---");
        builder.AppendLine();
        foreach (KeyValuePair<string, RoomInfo[]> region in regions)
        {
            builder.Append("---" + region.Key + "---");
            foreach (RoomInfo room in region.Value)
            {
                builder.AppendLine();
                builder.Append(room.Name + ": | Cameras: " + room.Cameras + ", Subregion: " + room.Subregion);
            }
            builder.AppendLine();
            builder.Append("---Total Rooms: " + region.Value.Length + "---");
            builder.AppendLine();
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
