#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>
/// The short names of the groupings: the label of a button and the end of its message. Kept next to
/// <see cref="Grouping"/> so the buttons that send them and the code that reads them agree.
/// </summary>
internal static class Groupings
{
    /// <summary> Every grouping, in the order of the buttons. </summary>
    public static readonly IReadOnlyList<Grouping> All = [.. Enum.GetValues(typeof(Grouping)).Cast<Grouping>()];

    /// <summary> The short name of a grouping: TYPE, SIZE or SUB. </summary>
    public static string Code(this Grouping grouping) => grouping switch
    {
        Grouping.Type => "TYPE",
        Grouping.Size => "SIZE",
        Grouping.Subregion => "SUB",
        _ => throw new ArgumentOutOfRangeException(nameof(grouping))
    };

    /// <summary> The grouping with this short name, or null if there is none. Case matters. </summary>
    public static Grouping? FromCode(string code)
    {
        foreach (Grouping grouping in All)
        {
            if (grouping.Code() == code)
            {
                return grouping;
            }
        }
        return null;
    }
}
