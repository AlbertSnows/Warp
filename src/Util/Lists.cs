using System.Collections.Generic;

namespace Warp.Util;

/// <summary>
/// Small operations on lists.
/// A file of its own because it holds code with no game types in it that <c>PlayerTransfer</c>, which needs the game,
/// would otherwise own and the tests could not reach.
/// </summary>
internal static class Lists
{
    /// <summary>
    /// Removes the later copies of each of the candidates from the target list, keeping the first. A candidate that is
    /// not in the target is ignored, and so are the copies of anything not named in <paramref name="candidates"/>.
    /// </summary>
    public static void RemoveDuplicates<T>(List<T> target, IEnumerable<T> candidates)
    {
        foreach (T item in candidates)
        {
            int first = target.IndexOf(item);
            for (int i = target.Count - 1; first >= 0 && i > first; i--)
            {
                if (EqualityComparer<T>.Default.Equals(target[i], item))
                {
                    target.RemoveAt(i);
                }
            }
        }
    }
}
