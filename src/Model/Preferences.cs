#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warp.Model;

/// <summary>
/// What the player has chosen in the Warp menu and wants kept between sessions. Never changed once made.
/// </summary>
internal sealed record Preferences(
    bool ShowMenu = true,
    bool ShowStats = false,
    Grouping Sort = Grouping.Type,
    Grouping View = Grouping.Type,
    bool DropdownMode = true,
    bool Alphabetical = true)
{
    /// <summary>
    /// The preferences after the player sorts by a grouping. Sorting by something also colors by it, because that is
    /// what makes the sort visible.
    /// </summary>
    public Preferences WithSort(Grouping grouping) => this with { Sort = grouping, View = grouping };

    /// <summary> The preferences after the player colors by a grouping. The sort is left as it was. </summary>
    public Preferences WithView(Grouping grouping) => this with { View = grouping };

    /// <summary>
    /// The settings file's text: one digit for each setting, in the order of the fields above, with the groupings as
    /// 0 (type), 1 (size) or 2 (subregion). Example: "100011".
    /// </summary>
    public string Serialize()
    {
        return new StringBuilder()
            .Append(Digit(ShowMenu))
            .Append(Digit(ShowStats))
            .Append((int)Sort)
            .Append((int)View)
            .Append(Digit(DropdownMode))
            .Append(Digit(Alphabetical))
            .ToString();
    }

    /// <summary>
    /// Reads the settings file's text. A file written by an older version may stop early, and the settings it does not
    /// reach keep their defaults. Null when the text is not a settings file at all, which means the caller should
    /// start again from the defaults.
    /// </summary>
    public static Preferences? Parse(string text)
    {
        string digits = text.Trim();
        if (digits.Length > 0 && digits.Any(c => c < '0' || c > '9'))
        {
            return null;
        }
        var result = new Preferences();
        for (int i = 0; i < Math.Min(digits.Length, 6); i++)
        {
            int value = digits[i] - '0';
            int largest = i is 2 or 3 ? (int)Grouping.Subregion : 1;
            if (value > largest)
            {
                return null;
            }
            result = i switch
            {
                0 => result with { ShowMenu = value == 1 },
                1 => result with { ShowStats = value == 1 },
                2 => result with { Sort = (Grouping)value },
                3 => result with { View = (Grouping)value },
                4 => result with { DropdownMode = value == 1 },
                _ => result with { Alphabetical = value == 1 }
            };
        }
        return result;
    }

    /// <summary> A flag as the digit the settings file uses for it. </summary>
    private static int Digit(bool value) => value ? 1 : 0;
}

/// <summary>
/// The regions the player has marked as favourites. The file holds the names separated by colons.
/// </summary>
internal static class FavouriteSet
{
    /// <summary> The text of the favourites file: the regions joined with colons. </summary>
    public static string Serialize(IEnumerable<string> regions) => string.Join(":", regions.OrderBy(name => name, StringComparer.Ordinal));

    /// <summary> The regions named in a favourites file. Blank names are ignored. </summary>
    public static HashSet<string> Parse(string text)
        => [.. text.Split(':').Select(name => name.Trim()).Where(name => name.Length > 0)];

    /// <summary>The set with the region added if it was absent and removed if it was there.</summary>
    public static HashSet<string> Toggle(IReadOnlyCollection<string> favourites, string region)
    {
        var result = new HashSet<string>(favourites);
        if (!result.Remove(region))
        {
            result.Add(region);
        }
        return result;
    }
}
