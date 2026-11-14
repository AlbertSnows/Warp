namespace Warp.Util;

/// <summary>
/// Remembers which keys have been seen, so something can be done the first time only. Used to log a fault once for
/// each place it happens, and not once a frame.
/// A file of its own because <c>Guard</c> uses it, and it is built only on <c>Atom</c>, so it is tested without the
/// game.
/// </summary>
internal sealed class Once
{
    private readonly Atom<string[]> _seen = new([]);

    /// <summary> True the first time it is called with this key, and false every time after. </summary>
    public bool First(string key)
    {
        Swapped<string[]> swapped = _seen.SwapVals(seen => System.Array.IndexOf(seen, key) >= 0 ? seen : [.. seen, key]);
        return !ReferenceEquals(swapped.Old, swapped.New);
    }
}
