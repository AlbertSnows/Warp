#nullable enable
using System.Collections.Generic;

namespace Warp.Util;

/// <summary>
/// Small operations on dictionaries.
/// A file of its own because <c>Model</c> and <c>Game</c> both look things up in dictionaries, and no one of them owns
/// the helper. It is tested directly.
/// </summary>
internal static class Maps
{
    /// <summary>
    /// The value for the key, or <paramref name="fallback"/> if there is none. For a fallback that is costly or has an
    /// effect to make, ask with <c>TryGetValue</c> instead, so it is made only when it is needed.
    /// </summary>
    public static TValue GetOr<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> map, TKey key, TValue fallback)
        => map.TryGetValue(key, out TValue? value) ? value : fallback;
}
