using UnityEngine;
using Warp.Model;
using Warp.Preview;

namespace Warp.UI;

/// <summary>The colors the menu uses for states, and the conversion from the palette's colors.</summary>
internal static class UiColors
{
    /// <summary> A sort or view button that is chosen. </summary>
    public static readonly Color Selected = new(0.2f, 1f, 0.2f);
    /// <summary> A sort, view or category button that is not chosen. </summary>
    public static readonly Color Unselected = new(0.4f, 0.4f, 0.4f);
    /// <summary> A sort or view button before it is given a state. </summary>
    public static readonly Color ChoiceButton = new(0.9f, 0.9f, 0.9f);
    /// <summary> A favourite region, and the favourite button when the region is one. </summary>
    public static readonly Color Favourite = new(1f, 0.3f, 0.3f);
    /// <summary> The favourite button when the region is not a favourite. </summary>
    public static readonly Color Plain = new(0.7f, 0.7f, 0.7f);
    /// <summary> A page button that can be pressed. </summary>
    public static readonly Color Active = new(1f, 0.9f, 0.3f);
    /// <summary> The STATS button while the stats are showing. </summary>
    public static readonly Color Stats = new(1f, 0.85f, 0f);
    /// <summary> A page button that cannot be pressed. </summary>
    public static readonly Color Disabled = new(0.3f, 0.3f, 0.3f);
    /// <summary> The message shown after a failed warp. </summary>
    public static readonly Color Error = new(1f, 0f, 0f);

    /// <summary> The "Warp Menu" heading. </summary>
    public static readonly Color Title = new(0.33f, 0.33f, 0.33f);

    /// <summary> The game's color for a palette color. </summary>
    public static Color ToColor(Hsl color) => new HSLColor(color.Hue, color.Saturation, color.Lightness).rgb;

    /// <summary> The preview's color for a palette color. </summary>
    public static Rgba ToRgba(Hsl color)
    {
        Color rgb = ToColor(color);
        return new Rgba(rgb.r, rgb.g, rgb.b, rgb.a);
    }

    /// <summary>The color of a button for a region: bright when its rooms are loaded, dim when not, red for favourites.</summary>
    public static Color RegionButton(bool loaded, bool favourite)
    {
        if (loaded)
        {
            return favourite ? Favourite : new Color(0.8f, 0.8f, 0.8f);
        }
        return favourite ? new Color(0.45f, 0.35f, 0.35f) : new Color(0.35f, 0.35f, 0.35f);
    }
}
