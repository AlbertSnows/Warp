#nullable enable
using System;
using Warp.Model;

namespace Warp.Preview;

/// <summary>Which room previews get a colored border when they are exported.</summary>
internal enum BorderMode
{
    None,
    All,
    Shelters,
    NotRooms
}

/// <summary>
/// The choices on the preview's export bar: how much to enlarge the picture, and which rooms get a border. A value;
/// the buttons make a new one each time they are pressed. A class and not a struct, so that <c>new ExportOptions()</c>
/// gets the defaults; a struct would start with a scale of 0.
/// </summary>
internal sealed record ExportOptions(int Scale = 1, BorderMode Border = BorderMode.None)
{
    /// <summary>Pixels of border on each side of an exported picture.</summary>
    private const int BorderWidth = 3;

    /// <summary>The largest picture Unity can make a texture of, along either side.</summary>
    public const int MaxTextureSize = 16384;

    /// <summary>The scales the scale button goes through.</summary>
    private static readonly int[] _scales = [1, 2, 5, 10];

    /// <summary>The border modes, in the order the border button goes through them.</summary>
    private static readonly BorderMode[] _borderModes = [BorderMode.None, BorderMode.All, BorderMode.Shelters, BorderMode.NotRooms];

    /// <summary>The options after the scale button is pressed: the next scale, going round.</summary>
    public ExportOptions NextScale() => this with { Scale = Cycle(_scales, Scale) };

    /// <summary>The options after the border button is pressed: the next mode, going round.</summary>
    public ExportOptions NextBorder() => this with { Border = Cycle(_borderModes, Border) };

    /// <summary> The value after <paramref name="current"/> in the list, wrapping round. The first if it is not in it. </summary>
    private static T Cycle<T>(T[] values, T current) => values[(Array.IndexOf(values, current) + 1) % values.Length];

    /// <summary> Whether a room of this type gets a border under the chosen mode. </summary>
    public bool HasBorder(RoomType type) => Border switch
    {
        BorderMode.All => true,
        BorderMode.Shelters => type == RoomType.Shelter,
        BorderMode.NotRooms => type != RoomType.Room,
        _ => false
    };

    /// <summary>The size of the saved picture for a preview of this size, with or without its border.</summary>
    public (int Width, int Height) OutputSize(int previewWidth, int previewHeight, bool withBorder)
    {
        int extra = withBorder ? 2 * BorderWidth : 0;
        return ((previewWidth + extra) * Scale, (previewHeight + extra) * Scale);
    }

    /// <summary> Whether a picture of that size is small enough to be made into a texture. </summary>
    public static bool Fits(int width, int height) => width <= MaxTextureSize && height <= MaxTextureSize;

    /// <summary>The picture to save: the preview with its border if it has one, enlarged by the scale.</summary>
    public PixelCanvas Compose(PixelCanvas preview, Rgba? border)
    {
        PixelCanvas bordered = border is { } color ? preview.WithBorder(BorderWidth, color) : preview;
        return bordered.Scaled(Scale);
    }
}
