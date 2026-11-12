#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Warp.Model;

/// <summary>A color as hue, saturation and lightness, each from 0 to 1.</summary>
internal readonly record struct Hsl(float Hue, float Saturation, float Lightness);

/// <summary>
/// The colors of the room buttons: one for each room type, one for each camera count, and a list of colors for the
/// subregions of each region. Never changed once made; the editing methods return a new palette.
/// </summary>
internal sealed class ColorPalette
{
    public const int TypeCount = 6;
    public const int SizeCount = 9;

    private readonly Hsl[] _types;
    private readonly Hsl[] _sizes;
    private readonly IReadOnlyDictionary<string, Hsl[]> _subregions;

    public ColorPalette(Hsl[] types, Hsl[] sizes, IReadOnlyDictionary<string, Hsl[]> subregions)
    {
        if (types.Length != TypeCount || sizes.Length != SizeCount)
        {
            throw new ArgumentException("A palette needs " + TypeCount + " type colors and " + SizeCount + " size colors.");
        }
        _types = types;
        _sizes = sizes;
        _subregions = subregions;
    }

    public static readonly ColorPalette Default = new(
        new Hsl[]
        {
            new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.63f), new(0.51f, 0.99f, 0.61f),
            new(0.73f, 0.99f, 0.76f), new(0.06f, 0.99f, 0.63f), new(0.99f, 0.99f, 0.66f)
        },
        new Hsl[]
        {
            new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.68f), new(0.08f, 0.99f, 0.61f),
            new(0f, 0.99f, 0.69f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f),
            new(0.6f, 0.99f, 0.59f), new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f)
        },
        new Dictionary<string, Hsl[]>());

    /// <summary>The colors for subregions, used until a region is given colors of its own. The first is for "none".</summary>
    private static readonly Hsl[] DefaultSubregions =
    {
        new(0.46f, 0.99f, 0.99f), new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.68f), new(0.08f, 0.99f, 0.61f),
        new(0f, 0.99f, 0.69f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f), new(0.6f, 0.99f, 0.59f),
        new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f),
        new(0.6f, 0.99f, 0.59f), new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f)
    };

    private const float GoldenRatio = 0.618034f;

    /// <summary>
    /// The color a subregion gets before the player changes it. The first fifteen are fixed. Beyond them the hues are
    /// spread around the color wheel by the golden ratio, which keeps neighbouring subregions apart without chance.
    /// </summary>
    public static Hsl DefaultSubregion(int index)
    {
        if (index < DefaultSubregions.Length)
        {
            return DefaultSubregions[index];
        }
        var hue = (index * GoldenRatio) % 1f;
        return new Hsl(hue, 0.85f, 0.85f);
    }

    public Hsl Type(int type) => _types[type];

    public Hsl Size(int cameras) => _sizes[Math.Min(Math.Max(cameras, 1), SizeCount) - 1];

    public IReadOnlyList<Hsl> Types => _types;

    public IReadOnlyList<Hsl> Sizes => _sizes;

    /// <summary>The regions that have colors of their own, with those colors.</summary>
    public IReadOnlyDictionary<string, Hsl[]> Subregions => _subregions;

    /// <summary>
    /// The colors of a region's subregions, as many as asked for. Subregions the palette holds no color for yet get
    /// the default for their index, without being stored; call <see cref="WithSubregionCount"/> to keep them.
    /// </summary>
    public Hsl Subregion(string region, int index)
    {
        return _subregions.TryGetValue(region, out var colors) && index < colors.Length
            ? colors[index]
            : DefaultSubregion(index);
    }

    /// <summary>
    /// The palette with the region's subregion list grown to at least this many colors, each new one the default for
    /// its index. The same palette when it is long enough already.
    /// </summary>
    public ColorPalette WithSubregionCount(string region, int count)
    {
        _subregions.TryGetValue(region, out var colors);
        if (colors != null && colors.Length >= count)
        {
            return this;
        }
        var grown = Enumerable.Range(0, count)
            .Select(i => colors != null && i < colors.Length ? colors[i] : DefaultSubregion(i))
            .ToArray();
        return WithSubregions(region, grown);
    }

    public ColorPalette WithType(int type, Hsl color) => new(Replace(_types, type, color), _sizes, _subregions);

    public ColorPalette WithSize(int index, Hsl color) => new(_types, Replace(_sizes, index, color), _subregions);

    /// <summary>The palette with one subregion's color changed, after growing the region's list to reach it.</summary>
    public ColorPalette WithSubregion(string region, int index, Hsl color)
    {
        var colors = WithSubregionCount(region, index + 1)._subregions[region];
        return WithSubregions(region, Replace(colors, index, color));
    }

    public ColorPalette WithSubregions(string region, Hsl[] colors)
    {
        var copy = new Dictionary<string, Hsl[]>(_subregions.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            [region] = colors
        };
        return new ColorPalette(_types, _sizes, copy);
    }

    /// <summary>The color of a room's button when the buttons are colored by <paramref name="view"/>.</summary>
    public Hsl For(RoomInfo room, Grouping view, string region) => view switch
    {
        Grouping.Type => Type((int)room.Type),
        Grouping.Size => Size(room.Cameras),
        _ => Subregion(region, room.Subregion)
    };

    private static Hsl[] Replace(Hsl[] colors, int index, Hsl color)
    {
        var copy = (Hsl[])colors.Clone();
        copy[index] = color;
        return copy;
    }
}
