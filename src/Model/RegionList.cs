#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>A region as the region list shows it: the id the game knows it by, and the text to display.</summary>
internal readonly record struct RegionEntry(string Id, string Display);

/// <summary>
/// How the installed regions are put in order for the dropdown and for the region buttons. Favourites come first in
/// both.
/// </summary>
internal static class RegionList
{
    private const string FavouriteMark = "> ";

    /// <summary>
    /// The dropdown's entries, one for each region id (the first one wins if an id is listed twice). The display text
    /// is the region's full name, or the id when the game has no name for it. A favourite is marked with "> " in
    /// front, which also sorts it to the top when the list is alphabetical.
    /// </summary>
    public static List<RegionEntry> DropdownEntries(
        IEnumerable<string> regionIds,
        Func<string, string?> fullName,
        IReadOnlyCollection<string> favourites,
        bool alphabetical)
    {
        var entries = new List<RegionEntry>();
        var seen = new HashSet<string>();
        foreach (var id in regionIds.Where(id => seen.Add(id)))
        {
            var display = fullName(id) ?? id;
            entries.Add(new RegionEntry(id, favourites.Contains(id) ? FavouriteMark + display : display));
        }
        return alphabetical
            ? entries.OrderBy(entry => entry.Display, StringComparer.CurrentCulture).ToList()
            : entries;
    }

    /// <summary>
    /// The region ids in the order the region buttons are laid out: favourites first, in alphabetical order, then the
    /// rest, in alphabetical order when asked and as the game lists them otherwise.
    /// </summary>
    public static List<string> ButtonOrder(
        IEnumerable<string> regionIds, IReadOnlyCollection<string> favourites, bool alphabetical)
    {
        var ids = regionIds.ToList();
        var favouriteIds = ids.Where(favourites.Contains).OrderBy(id => id, StringComparer.CurrentCulture);
        var others = ids.Where(id => !favourites.Contains(id));
        if (alphabetical)
        {
            others = others.OrderBy(id => id, StringComparer.CurrentCulture);
        }
        return favouriteIds.Concat(others).ToList();
    }
}
