#nullable enable
using System.Globalization;

namespace Warp.Util;

/// <summary>
/// Reads numbers from text and gives the number or null, so a caller does not need an out variable. The one place the
/// framework's <c>TryParse</c> is called. Always uses a decimal point and no thousands separators, whatever language the
/// player's computer is set to.
/// A file of its own because <c>ColorFile</c>, <c>WarpSignals</c> and <c>RoomTiles</c> all read numbers from text, and
/// this is the one place that calls the framework's <c>TryParse</c>.
/// </summary>
internal static class Numbers
{
    /// <summary>The whole number the text holds, or null if it holds none. <paramref name="style"/> says what is allowed.</summary>
    public static int? ParseInt(string text, NumberStyles style = NumberStyles.Integer)
        => int.TryParse(text, style, CultureInfo.InvariantCulture, out int number) ? number : null;

    /// <summary>The number the text holds, or null if it holds none. <paramref name="style"/> says what is allowed.</summary>
    public static float? ParseFloat(string text, NumberStyles style = NumberStyles.Float)
        => float.TryParse(text, style, CultureInfo.InvariantCulture, out float number) ? number : null;
}
