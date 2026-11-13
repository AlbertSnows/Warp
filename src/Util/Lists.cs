using System.Collections.Generic;

namespace Warp.Util;

/// <summary> Small operations on lists. </summary>
internal static class Lists
{
    /// <summary>
    /// Removes the later copies of each of the items from the list, keeping the first. Items that are not in the list
    /// are ignored, and so are the copies of anything not named in <paramref name="items"/>.
    /// </summary>
    public static void RemoveDuplicates<T>(List<T> list, IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            int first = list.IndexOf(item);
            for (int i = list.Count - 1; first >= 0 && i > first; i--)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], item))
                {
                    list.RemoveAt(i);
                }
            }
        }
    }
}
