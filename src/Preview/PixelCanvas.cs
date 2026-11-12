#nullable enable
using System;

namespace Warp.Preview;

/// <summary>A color with red, green, blue and alpha from 0 to 1.</summary>
internal readonly record struct Rgba(float R, float G, float B, float A = 1f)
{
    public static Rgba Grey(float level) => new(level, level, level);

    public static Rgba Lerp(Rgba from, Rgba to, float t)
    {
        t = Math.Min(Math.Max(t, 0f), 1f);
        return new Rgba(
            from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t,
            from.B + (to.B - from.B) * t, from.A + (to.A - from.A) * t);
    }
}

/// <summary>
/// A picture as a grid of pixels, with (0, 0) at the bottom left as in Unity's textures. The drawing code works on
/// this so it can be tested, and the menu turns the result into a texture.
/// </summary>
internal sealed class PixelCanvas
{
    private readonly Rgba[] _pixels;

    public PixelCanvas(int width, int height, Rgba fill)
    {
        Width = width;
        Height = height;
        _pixels = new Rgba[width * height];
        for (var i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = fill;
        }
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public Rgba Get(int x, int y) => _pixels[y * Width + x];

    /// <summary>Sets a pixel. A position off the canvas is ignored.</summary>
    public void Set(int x, int y, Rgba color)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height)
        {
            _pixels[y * Width + x] = color;
        }
    }

    public void Fill(int x, int y, int width, int height, Rgba color)
    {
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                Set(x + dx, y + dy, color);
            }
        }
    }

    /// <summary>The pixels row by row from the bottom left, which is the order Unity's SetPixels wants.</summary>
    public Rgba[] ToArray() => (Rgba[])_pixels.Clone();

    /// <summary>The canvas with each pixel repeated as a square of <paramref name="factor"/> pixels a side.</summary>
    public PixelCanvas Scaled(int factor)
    {
        if (factor == 1)
        {
            return this;
        }
        var result = new PixelCanvas(Width * factor, Height * factor, default);
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                result._pixels[y * result.Width + x] = Get(x / factor, y / factor);
            }
        }
        return result;
    }

    /// <summary>The canvas with a border of the given width and color all the way round.</summary>
    public PixelCanvas WithBorder(int width, Rgba color)
    {
        var result = new PixelCanvas(Width + 2 * width, Height + 2 * width, color);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                result.Set(x + width, y + width, Get(x, y));
            }
        }
        return result;
    }
}
