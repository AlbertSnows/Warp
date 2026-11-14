#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Warp.Util;

namespace Warp.Model;

/// <summary>
/// The colors file (Colors.txt). It has a [TYPE] section with one "hue:saturation:lightness" line per room type, a
/// [SIZE] section in the same form, and a [SUBREGION] section in which "[SU]" starts the list of one region. The file
/// that ships with Warp also has a [DEFAULT-SUBREGION] section, the colors for a subregion whose region has no list;
/// it is where the built-in colors come from, and the file Warp saves leaves it out. Lines that start with "//" and
/// blank lines are ignored, and "[END]" stops the reading.
/// </summary>
internal static class ColorFile
{
    private const string TypeSection = "[TYPE]";
    private const string SizeSection = "[SIZE]";
    private const string SubregionSection = "[SUBREGION]";
    private const string End = "[END]";

    /// <summary> The section of the shipped file that holds the colors for subregions of a region with no list. </summary>
    public const string DefaultSubregionSection = "[DEFAULT-SUBREGION]";

    /// <summary> The name the shipped colors file is built into the plugin under. </summary>
    public const string ResourceName = "Warp.Resources.Colors.txt";

    /// <summary>
    /// The lines that mean something: trimmed, with the blank lines and comments dropped, and nothing from the end
    /// marker on.
    /// </summary>
    private static IEnumerable<string> Meaningful(IEnumerable<string> lines) => lines
        .Select(raw => raw.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal))
        .TakeWhile(line => line != End);

    /// <summary>
    /// The colors of one section, in order. A line that cannot be read is left out. Empty if the file has no such
    /// section.
    /// </summary>
    public static Hsl[] ReadSection(IEnumerable<string> lines, string section) =>
    [
        .. Meaningful(lines)
            .SkipWhile(line => line != section)
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("[", StringComparison.Ordinal))
            .Select(ParseColor)
            .OfType<Hsl>()
    ];

    /// <summary>
    /// The palette the file describes, starting from <paramref name="start"/>. A color that cannot be read is
    /// reported to <paramref name="onBadLine"/> and keeps its old value. Colors past the end of a section are ignored.
    /// </summary>
    public static ColorPalette Parse(IEnumerable<string> lines, ColorPalette start, Action<string>? onBadLine = null)
    {
        Hsl[] types = [.. start.Types];
        Hsl[] sizes = [.. start.Sizes];
        var regions = new Dictionary<string, List<Hsl>>();
        string? section = null;
        string? region = null;
        int index = 0;

        foreach (string line in Meaningful(lines))
        {
            if (line is TypeSection or SizeSection or SubregionSection or DefaultSubregionSection)
            {
                section = line;
                region = null;
                index = 0;
            }
            else if (section == SubregionSection && line.StartsWith("[", StringComparison.Ordinal))
            {
                region = line.Trim('[', ']');
                regions[region] = [];
            }
            else
            {
                Hsl? color = ParseColor(line);
                if (color == null)
                {
                    onBadLine?.Invoke(line);
                }
                // A line that cannot be read keeps the old color, or for a subregion takes the default for its place
                switch (section)
                {
                    case TypeSection when index < types.Length:
                        types[index] = color ?? types[index];
                        break;
                    case SizeSection when index < sizes.Length:
                        sizes[index] = color ?? sizes[index];
                        break;
                    case SubregionSection when region != null:
                        regions[region].Add(color ?? ColorPalette.DefaultSubregion(regions[region].Count));
                        break;
                }
                index++;
            }
        }

        var subregions = start.Subregions.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        foreach (KeyValuePair<string, List<Hsl>> pair in regions)
        {
            subregions[pair.Key] = [.. pair.Value];
        }
        return new ColorPalette(types, sizes, subregions);
    }

    /// <summary> The text of the colors file for the palette. The inverse of <see cref="Parse"/>. </summary>
    public static string Serialize(ColorPalette palette)
    {
        var builder = new StringBuilder();
        builder.AppendLine(TypeSection);
        AppendColors(builder, palette.Types);
        builder.AppendLine(SizeSection);
        AppendColors(builder, palette.Sizes);
        builder.AppendLine(SubregionSection);
        // In order of name, so saving twice writes the same file
        foreach (KeyValuePair<string, IReadOnlyList<Hsl>> pair in palette.Subregions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine("[" + pair.Key + "]");
            AppendColors(builder, pair.Value);
        }
        builder.Append(End);
        return builder.ToString();
    }

    private static void AppendColors(StringBuilder builder, IEnumerable<Hsl> colors)
    {
        foreach (Hsl color in colors)
        {
            builder.AppendLine(Format(color));
        }
    }

    /// <summary> A color as "hue:saturation:lightness", each rounded to two places. </summary>
    private static string Format(Hsl color)
        => string.Join(":", Round(color.Hue), Round(color.Saturation), Round(color.Lightness));

    private static string Round(float value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads "hue:saturation:lightness". Numbers written by this version use a decimal point. A file written by an
    /// older version on a computer set to another language may use a comma, so that is accepted too. Each number has
    /// to be from 0 to 1, and one that is not, or is not a number at all (NaN and infinity parse as numbers), makes the
    /// line a bad one. Anything after the third field is ignored.
    /// </summary>
    private static Hsl? ParseColor(string line)
    {
        string[] fields = line.Split(':');
        float?[] numbers = [.. fields.Take(3).Select(ParseUnit)];
        return numbers.Length == 3 && numbers.All(number => number != null)
            ? new Hsl(numbers[0]!.Value, numbers[1]!.Value, numbers[2]!.Value)
            : null;
    }

    /// <summary> A color channel from its text, or null if it is not a number from 0 to 1. </summary>
    private static float? ParseUnit(string text)
    {
        // A file written on a computer set to another language may use a decimal comma
        float? number = Numbers.ParseFloat(text.Replace(',', '.'));
        return number != null && IsUnit(number.Value) ? number : null;
    }

    /// <summary> Whether a number is a usable color channel: from 0 to 1, which leaves out NaN and infinity. </summary>
    private static bool IsUnit(float number) => number is >= 0f and <= 1f;
}
