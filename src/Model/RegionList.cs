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
    /// front, and favourites come first. The rest follow in alphabetical order of the displayed name when asked, which
    /// uses the player's language because these are translated names, and as the game lists them otherwise.
    /// </summary>
    public static List<RegionEntry> DropdownEntries(
        IEnumerable<string> regionIds,
        Func<string, string?> fullName,
        IReadOnlyCollection<string> favourites,
        bool alphabetical)
    {
        IEnumerable<RegionEntry> entries = regionIds
            .Distinct()
            .Select(id => new RegionEntry(id, DisplayName(id, fullName, favourites)));
        // OrderBy keeps the order it was given among equal keys, which is how the game's own order survives
        IOrderedEnumerable<RegionEntry> favouritesFirst = entries.OrderBy(entry => favourites.Contains(entry.Id) ? 0 : 1);
        return alphabetical
            ? [.. favouritesFirst.ThenBy(entry => entry.Display, StringComparer.CurrentCulture)]
            : [.. favouritesFirst];
    }

    /// <summary>The text for a region: its full name, or its id if it has none, with the favourite mark in front if needed.</summary>
    private static string DisplayName(string id, Func<string, string?> fullName, IReadOnlyCollection<string> favourites)
    {
        string name = fullName(id) ?? id;
        return favourites.Contains(id) ? FavouriteMark + name : name;
    }

    /// <summary>
    /// The region ids in the order the region buttons are laid out: favourites first, in alphabetical order, then the
    /// rest, in alphabetical order when asked and as the game lists them otherwise.
    /// </summary>
    public static List<string> ButtonOrder(
        IEnumerable<string> regionIds, IReadOnlyCollection<string> favourites, bool alphabetical)
    {
        var ids = regionIds.ToList();
        IOrderedEnumerable<string> favouriteIds = ids.Where(favourites.Contains).OrderBy(id => id, StringComparer.Ordinal);
        IEnumerable<string> others = ids.Where(id => !favourites.Contains(id));
        if (alphabetical)
        {
            others = others.OrderBy(id => id, StringComparer.Ordinal);
        }
        return [.. favouriteIds, .. others];
    }
}
