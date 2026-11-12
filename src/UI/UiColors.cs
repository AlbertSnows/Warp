using RWCustom;
using UnityEngine;
using Warp.Model;
using Warp.Preview;

namespace Warp.UI;

/// <summary>The colors the menu uses for states, and the conversion from the palette's colors.</summary>
internal static class UiColors
{
    public static readonly Color Selected = new(0.2f, 1f, 0.2f);
    public static readonly Color Unselected = new(0.4f, 0.4f, 0.4f);
    public static readonly Color ChoiceButton = new(0.9f, 0.9f, 0.9f);
    public static readonly Color Favourite = new(1f, 0.3f, 0.3f);
    public static readonly Color Plain = new(0.7f, 0.7f, 0.7f);
    public static readonly Color Active = new(1f, 0.9f, 0.3f);
    public static readonly Color Stats = new(1f, 0.85f, 0f);
    public static readonly Color Disabled = new(0.3f, 0.3f, 0.3f);
    public static readonly Color Error = new(1f, 0f, 0f);
    public static readonly Color Light = new(0.8f, 0.8f, 0.8f);
    public static readonly Color Title = new(0.33f, 0.33f, 0.33f);

    public static Color ToColor(Hsl color) => new HSLColor(color.Hue, color.Saturation, color.Lightness).rgb;

    public static Rgba ToRgba(Hsl color)
    {
        var rgb = ToColor(color);
        return new Rgba(rgb.r, rgb.g, rgb.b, rgb.a);
    }

    public static Hsl ToHsl(HSLColor color) => new(color.hue, color.saturation, color.lightness);

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
