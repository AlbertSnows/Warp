using UnityEngine;
using Warp.Model;

namespace Warp.Game;

/// <summary>The conversion from the palette's colors to the game's.</summary>
internal static class HslColors
{
    /// <summary> The game's color for a palette color. </summary>
    public static Color ToColor(Hsl color) => new HSLColor(color.Hue, color.Saturation, color.Lightness).rgb;
}
