#nullable enable
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

/// <summary>The choices on the preview's export bar, and how an exported picture is put together.</summary>
internal static class ExportOptions
{
    /// <summary>Pixels of border on each side of an exported picture.</summary>
    private const int BorderWidth = 3;

    /// <summary>The next mode when the border button is pressed, going round.</summary>
    public static BorderMode Next(BorderMode mode) => mode == BorderMode.NotRooms ? BorderMode.None : mode + 1;

    /// <summary> Whether a room of this type gets a border under the chosen mode. </summary>
    public static bool HasBorder(BorderMode mode, RoomType type) => mode switch
    {
        BorderMode.All => true,
        BorderMode.Shelters => type == RoomType.Shelter,
        BorderMode.NotRooms => type != RoomType.Room,
        _ => false
    };

    /// <summary>The next scale when the scale button is pressed: 1, 2, 5, 10 and back to 1.</summary>
    public static int NextScale(int scale) => scale switch
    {
        1 => 2,
        2 => 5,
        5 => 10,
        _ => 1
    };

    /// <summary>The picture to save: the preview with its border if it has one, enlarged by the scale.</summary>
    public static PixelCanvas Compose(PixelCanvas preview, int scale, Rgba? border)
    {
        PixelCanvas bordered = border is { } color ? preview.WithBorder(BorderWidth, color) : preview;
        return bordered.Scaled(scale);
    }

    /// <summary>
    /// The room tile under the pointer in the enlarged preview, or null if the pointer is outside the room. The
    /// preview is centered on the sprite and each tile is <see cref="PreviewPainter.TileSize"/> texture pixels.
    /// </summary>
    public static TileCoord? TileAt(
        float pointerX, float pointerY, float spriteX, float spriteY, float spriteScale, int width, int height)
    {
        float x = (pointerX - spriteX) / (spriteScale * PreviewPainter.TileSize) + width / 2f;
        float y = (pointerY - spriteY) / (spriteScale * PreviewPainter.TileSize) + height / 2f;
        if (x <= 0 || x >= width || y <= 0 || y >= height)
        {
            return null;
        }
        return new TileCoord((int)System.Math.Floor(x), (int)System.Math.Floor(y));
    }
}
