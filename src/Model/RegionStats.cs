#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warp.Model;

/// <summary>
/// Counts for one region, shown in the stats panel. Never changed once made.
/// </summary>
/// <param name="Rooms">How many rooms the region has.</param>
/// <param name="Screens">The camera count of all rooms added up.</param>
/// <param name="CountsByType">How many rooms there are of each type that has any.</param>
/// <param name="RoomsByCameras">For each camera count that occurs, how many rooms have it, smallest first.</param>
internal sealed record RegionStats(
    int Rooms,
    int Screens,
    IReadOnlyDictionary<RoomType, int> CountsByType,
    IReadOnlyList<KeyValuePair<int, int>> RoomsByCameras)
{
    public int Count(RoomType type) => CountsByType.TryGetValue(type, out var count) ? count : 0;

    public static RegionStats Of(IReadOnlyCollection<RoomInfo> rooms)
    {
        return new RegionStats(
            rooms.Count,
            rooms.Sum(room => room.Cameras),
            rooms.GroupBy(room => room.Type).ToDictionary(group => group.Key, group => group.Count()),
            rooms.GroupBy(room => room.Cameras).OrderBy(group => group.Key)
                .Select(group => new KeyValuePair<int, int>(group.Key, group.Count())).ToList());
    }

    /// <summary>
    /// The text of the stats panel in two parts, a summary and the screen-size breakdown, each translated with
    /// <paramref name="translate"/>. Swarm rooms, traders and tolls are listed only when the region has some.
    /// </summary>
    public (string Summary, string Breakdown) Describe(Func<string, string> translate)
    {
        var summary = new StringBuilder();
        summary.AppendLine(translate("ROOMS: ") + Rooms);
        summary.AppendLine(translate("SCREENS: ") + Screens);
        summary.AppendLine();
        summary.AppendLine(translate("Gates: ") + Count(RoomType.Gate));
        summary.AppendLine(translate("Shelters: ") + Count(RoomType.Shelter));
        AppendIfAny(summary, translate("Swarm Rooms: "), Count(RoomType.SwarmRoom));
        AppendIfAny(summary, translate("Scav Traders: "), Count(RoomType.ScavTrader));
        AppendIfAny(summary, translate("Scav Tolls: "), Count(RoomType.ScavOutpost));

        var breakdown = new StringBuilder();
        foreach (var pair in RoomsByCameras)
        {
            breakdown.AppendLine().Append(pair.Key + translate(" Screen: ") + pair.Value);
        }
        return (summary.ToString(), breakdown.ToString());
    }

    private static void AppendIfAny(StringBuilder text, string label, int count)
    {
        if (count > 0)
        {
            text.AppendLine(label + count);
        }
    }
}
