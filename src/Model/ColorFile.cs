#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Warp.Model;

/// <summary>
/// The colors file (Colors.txt). It has a [TYPE] section with one "hue:saturation:lightness" line per room type, a
/// [SIZE] section in the same form, and a [SUBREGION] section in which "[SU]" starts the list of one region. Lines
/// that start with "//" and blank lines are ignored, and "[END]" stops the reading.
/// </summary>
internal static class ColorFile
{
    private const string TypeSection = "[TYPE]";
    private const string SizeSection = "[SIZE]";
    private const string SubregionSection = "[SUBREGION]";
    private const string End = "[END]";

    /// <summary>
    /// The palette the file describes, starting from <paramref name="start"/>. A color that cannot be read is
    /// reported to <paramref name="onBadLine"/> and keeps its old value. Colors past the end of a section are ignored.
    /// </summary>
    public static ColorPalette Parse(IEnumerable<string> lines, ColorPalette start, Action<string>? onBadLine = null)
    {
        var types = start.Types.ToArray();
        var sizes = start.Sizes.ToArray();
        var regions = new Dictionary<string, List<Hsl>>();
        string? section = null;
        string? region = null;
        var index = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//"))
            {
                continue;
            }
            if (line == End)
            {
                break;
            }
            if (line is TypeSection or SizeSection or SubregionSection)
            {
                section = line;
                region = null;
                index = 0;
                continue;
            }
            if (section == SubregionSection && line.StartsWith("["))
            {
                region = line.Trim('[', ']');
                regions[region] = new List<Hsl>();
                continue;
            }
            if (!TryParseColor(line, out var color))
            {
                onBadLine?.Invoke(line);
                if (section == SubregionSection && region != null)
                {
                    regions[region].Add(ColorPalette.DefaultSubregion(regions[region].Count));
                }
                index++;
                continue;
            }
            switch (section)
            {
                case TypeSection when index < types.Length:
                    types[index] = color;
                    break;
                case SizeSection when index < sizes.Length:
                    sizes[index] = color;
                    break;
                case SubregionSection when region != null:
                    regions[region].Add(color);
                    break;
            }
            index++;
        }

        var subregions = start.Subregions.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in regions)
        {
            subregions[pair.Key] = pair.Value.ToArray();
        }
        return new ColorPalette(types, sizes, subregions);
    }

    public static string Serialize(ColorPalette palette)
    {
        var text = new StringBuilder();
        text.AppendLine(TypeSection);
        palette.Types.ToList().ForEach(color => text.AppendLine(Format(color)));
        text.AppendLine(SizeSection);
        palette.Sizes.ToList().ForEach(color => text.AppendLine(Format(color)));
        text.AppendLine(SubregionSection);
        foreach (var pair in palette.Subregions)
        {
            text.AppendLine("[" + pair.Key + "]");
            pair.Value.ToList().ForEach(color => text.AppendLine(Format(color)));
        }
        text.Append(End);
        return text.ToString();
    }

    private static string Format(Hsl color)
    {
        return string.Join(":", Round(color.Hue), Round(color.Saturation), Round(color.Lightness));
    }

    private static string Round(float value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads "hue:saturation:lightness". Numbers written by this version use a decimal point. A file written by an
    /// older version on a computer set to another language may use a comma, so that is accepted too.
    /// </summary>
    private static bool TryParseColor(string line, out Hsl color)
    {
        color = default;
        var fields = line.Split(':');
        if (fields.Length < 3)
        {
            return false;
        }
        var numbers = new float[3];
        for (var i = 0; i < 3; i++)
        {
            if (!TryParseNumber(fields[i], out numbers[i]))
            {
                return false;
            }
        }
        color = new Hsl(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    private static bool TryParseNumber(string text, out float number)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
               || float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
