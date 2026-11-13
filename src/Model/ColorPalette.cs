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
    /// <summary> How many room types have a color. </summary>
    public const int TypeCount = 6;
    /// <summary> How many camera counts have a color. The last one is for that many cameras or more. </summary>
    public const int SizeCount = 9;

    private readonly Hsl[] _types;
    private readonly Hsl[] _sizes;
    private readonly Dictionary<string, Hsl[]> _subregions;

    /// <summary>
    /// A palette of the colors given. It keeps copies, so the arrays passed in can be reused or changed afterwards
    /// without reaching into the palette.
    /// </summary>
    public ColorPalette(Hsl[] types, Hsl[] sizes, IReadOnlyDictionary<string, Hsl[]> subregions)
    {
        if (types.Length != TypeCount || sizes.Length != SizeCount)
        {
            throw new ArgumentException("A palette needs " + TypeCount + " type colors and " + SizeCount + " size colors.");
        }
        _types = [.. types];
        _sizes = [.. sizes];
        _subregions = subregions.ToDictionary(pair => pair.Key, pair => (Hsl[])pair.Value.Clone());
        Subregions = _subregions.ToDictionary(pair => pair.Key, IReadOnlyList<Hsl> (pair) => pair.Value);
    }

    /// <summary> The colors Warp starts with, before any file or edit. </summary>
    public static readonly ColorPalette Default = new(
        [
            new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.63f), new(0.51f, 0.99f, 0.61f),
            new(0.73f, 0.99f, 0.76f), new(0.06f, 0.99f, 0.63f), new(0.99f, 0.99f, 0.66f)
        ],
        [
            new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.68f), new(0.08f, 0.99f, 0.61f),
            new(0f, 0.99f, 0.69f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f),
            new(0.6f, 0.99f, 0.59f), new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f)
        ],
        new Dictionary<string, Hsl[]>());

    /// <summary>The colors for subregions, used until a region is given colors of its own. The first is for "none".</summary>
    private static readonly Hsl[] _defaultSubregions =
    [
        new(0.46f, 0.99f, 0.99f), new(0.33f, 0.99f, 0.67f), new(0.18f, 0.99f, 0.68f), new(0.08f, 0.99f, 0.61f),
        new(0f, 0.99f, 0.69f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f), new(0.6f, 0.99f, 0.59f),
        new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f), new(0.82f, 0.99f, 0.73f), new(0.71f, 0.99f, 0.66f),
        new(0.6f, 0.99f, 0.59f), new(0.54f, 0.99f, 0.65f), new(0.46f, 0.99f, 0.58f)
    ];

    private const float GoldenRatio = 0.618034f;

    /// <summary>
    /// The color a subregion gets before the player changes it. The first fifteen are fixed. Beyond them the hues are
    /// spread around the color wheel by the golden ratio, which keeps neighboring subregions apart without chance.
    /// </summary>
    public static Hsl DefaultSubregion(int index)
    {
        if (index < _defaultSubregions.Length)
        {
            return _defaultSubregions[index];
        }
        float hue = (index * GoldenRatio) % 1f;
        return new Hsl(hue, 0.85f, 0.85f);
    }

    /// <summary> The color of a room type, given as the type's number. </summary>
    public Hsl Type(RoomType type) => _types[(int)type];

    /// <summary>
    /// The color for a camera count. Counts past the last color share it, and counts under 1 are read as 1.
    /// </summary>
    public Hsl Size(int cameras) => _sizes[Math.Min(Math.Max(cameras, 1), SizeCount) - 1];

    /// <summary> The room type colors, in the order of <see cref="RoomType"/>. </summary>
    public IReadOnlyList<Hsl> Types => _types;

    /// <summary> The camera count colors, starting with one camera. </summary>
    public IReadOnlyList<Hsl> Sizes => _sizes;

    /// <summary>The regions that have colors of their own, with those colors.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Hsl>> Subregions
    {
        get;
    }

    /// <summary>
    /// The colors of a region's subregions, as many as asked for. Subregions the palette holds no color for yet get
    /// the default for their index, without being stored; call <see cref="WithSubregionCount"/> to keep them.
    /// </summary>
    public Hsl Subregion(string region, int index)
    {
        return _subregions.TryGetValue(region, out Hsl[]? colors) && index < colors.Length
            ? colors[index]
            : DefaultSubregion(index);
    }

    /// <summary>
    /// The palette with the region's subregion list grown to at least this many colors, each new one the default for
    /// its index. The same palette when it is long enough already.
    /// </summary>
    public ColorPalette WithSubregionCount(string region, int count)
    {
        _subregions.TryGetValue(region, out Hsl[]? colors);
        if (colors != null && colors.Length >= count)
        {
            return this;
        }
        Hsl[] grown =
        [
            .. Enumerable.Range(0, count)
                .Select(i => colors != null && i < colors.Length ? colors[i] : DefaultSubregion(i))
        ];
        return WithSubregions(region, grown);
    }

    /// <summary> The palette with one room type's color changed. </summary>
    public ColorPalette WithType(RoomType type, Hsl color) => new(Replace(_types, (int)type, color), _sizes, _subregions);

    /// <summary>
    /// The palette with one camera count's color changed. The index counts from 0, so 0 is one camera.
    /// </summary>
    public ColorPalette WithSize(int index, Hsl color) => new(_types, Replace(_sizes, index, color), _subregions);

    /// <summary>The palette with one subregion's color changed, after growing the region's list to reach it.</summary>
    public ColorPalette WithSubregion(string region, int index, Hsl color)
    {
        Hsl[] colors = WithSubregionCount(region, index + 1)._subregions[region];
        return WithSubregions(region, Replace(colors, index, color));
    }

    /// <summary> The palette with a region's whole subregion list replaced. </summary>
    internal ColorPalette WithSubregions(string region, Hsl[] colors)
    {
        var changed = new Dictionary<string, Hsl[]>(_subregions)
        {
            [region] = colors
        };
        return new ColorPalette(_types, _sizes, changed);
    }

    /// <summary>The color of a room's button when the buttons are colored by <paramref name="view"/>.</summary>
    public Hsl For(RoomInfo room, Grouping view, string region) => view switch
    {
        Grouping.Type => Type(room.Type),
        Grouping.Size => Size(room.Cameras),
        _ => Subregion(region, room.Subregion)
    };

    /// <summary> A copy of the colors with one replaced. The original is left alone. </summary>
    private static Hsl[] Replace(Hsl[] colors, int index, Hsl color)
    {
        var copy = (Hsl[])colors.Clone();
        copy[index] = color;
        return copy;
    }
}
