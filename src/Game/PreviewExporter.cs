using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Warp.Model;
using Warp.Preview;
using Warp.Util;

namespace Warp.Game;

/// <summary>
/// Saves a room's preview as a picture: puts the picture together with the chosen scale and border, makes sure it is not
/// too big for a texture, and writes the PNG into the export folder of the region.
/// </summary>
internal static class PreviewExporter
{
    /// <summary>
    /// Saves one room's picture and returns where it went. Returns null and logs the reason if it could not be saved,
    /// whether the picture cannot be made, is too big or the disk refuses it. The picture is made inside, by
    /// <paramref name="preview"/>, so a failure to make it is caught too.
    /// </summary>
    public static string TrySave(string region, string room, RoomType type, ExportOptions options, Func<PixelCanvas> preview)
    {
        try
        {
            return Save(region, room, type, options, preview());
        }
        catch (Exception error)
        {
            WarpLog.Exception("exporting " + room, error);
            return null;
        }
    }

    /// <summary> The preview's color for a palette color. </summary>
    private static Rgba ToRgba(Hsl color)
    {
        var rgb = HslColors.ToColor(color);
        return new Rgba(rgb.r, rgb.g, rgb.b, rgb.a);
    }

    private static string Save(string region, string room, RoomType type, ExportOptions options, PixelCanvas preview)
    {
        Rgba? border = options.HasBorder(type) ? ToRgba(WarpState.Palette.Type(type)) : null;
        (int width, int height) = options.OutputSize(preview.Width, preview.Height, border != null);
        if (!ExportOptions.Fits(width, height))
        {
            throw new InvalidOperationException(
                "The picture would be " + width + " by " + height + " pixels, and Unity allows at most "
                + ExportOptions.MaxTextureSize + " on a side. Choose a smaller scale.");
        }

        string folder = WarpFiles.ExportFolderOf(region);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, room + "_preview.png");
        Texture2D texture = options.Compose(preview, border).ToTexture();
        try
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
        return path;
    }
}

/// <summary> Turns a canvas into something Unity can draw. </summary>
internal static class PixelCanvasExtensions
{
    /// <summary> A texture with the canvas's pixels, drawn without smoothing so each pixel stays sharp. </summary>
    public static Texture2D ToTexture(this PixelCanvas canvas)
    {
        var texture = new Texture2D(canvas.Width, canvas.Height);
        texture.SetPixels([.. canvas.ToArray().Select(pixel => new Color(pixel.R, pixel.G, pixel.B, pixel.A))]);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return texture;
    }
}
