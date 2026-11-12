#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>
/// The orders the room list can be shown in. Every order falls back to the room name, so the result does not depend
/// on the order the rooms came in.
/// </summary>
internal static class RoomSorting
{
    /// <summary>
    /// The rooms in the order the grouping asks for: by type, camera count or subregion, then by name.
    /// </summary>
    public static RoomInfo[] Sort(IEnumerable<RoomInfo> rooms, Grouping grouping)
    {
        return rooms
            .OrderBy(room => room.GroupKey(grouping))
            .ThenBy(room => room.Name, StringComparer.CurrentCulture)
            .ToArray();
    }
}
