#nullable enable
using System.Collections.Generic;

namespace Warp.Preview;

/// <summary>
/// Draws a room's tiles as a small map: each tile is 3 by 3 pixels, with walls, floors, slopes, beams, water and the
/// shortcuts in different shades or colors.
/// </summary>
internal static class PreviewPainter
{
    /// <summary>Pixels along a tile's edge.</summary>
    public const int TileSize = 3;

    private static readonly Rgba Background = Rgba.Grey(0.35f);
    private static readonly Rgba WallBehind = Rgba.Grey(0.25f);
    private static readonly Rgba Rock = Rgba.Grey(0.2f);
    private static readonly Rgba FloorTop = Rgba.Grey(0.21f);
    private static readonly Rgba ShortcutPath = Rgba.Grey(0.5f);
    private static readonly Rgba Water = new(0f, 0.5f, 1f);
    private const float WaterTint = 0.2f;

    private static readonly Dictionary<int, Rgba> ShortcutMarks = new()
    {
        [2] = new Rgba(0.2f, 1f, 0.2f),
        [3] = new Rgba(1f, 0.2f, 0.2f),
        [4] = new Rgba(0.2f, 0.2f, 1f),
        [5] = new Rgba(1f, 1f, 0.2f)
    };

    private static readonly (int X, int Y)[] Plus = { (1, 0), (0, 1), (2, 1), (1, 2), (1, 1) };

    public static PixelCanvas Paint(RoomTiles room)
    {
        var canvas = new PixelCanvas(room.Width * TileSize, room.Height * TileSize, Background);
        for (var x = 0; x < room.Width; x++)
        {
            for (var y = 0; y < room.Height; y++)
            {
                PaintTile(canvas, room, x, y);
            }
        }
        return canvas;
    }

    private static void PaintTile(PixelCanvas canvas, RoomTiles room, int tx, int ty)
    {
        var tile = room.At(tx, ty);
        int x = tx * TileSize, y = ty * TileSize;

        if (tile.WallBehind)
        {
            canvas.Fill(x, y, 3, 3, WallBehind);
        }
        if (tile.Terrain == Terrain.Solid)
        {
            canvas.Fill(x, y, 3, 3, Rock);
        }
        PaintSlope(canvas, room.SlopeAt(tx, ty), x, y);
        if (tile.Terrain == Terrain.Floor)
        {
            canvas.Fill(x, y + 1, 3, 2, FloorTop);
        }
        if (tile.VerticalBeam)
        {
            canvas.Fill(x + 1, y, 1, 3, Rock);
        }
        if (tile.HorizontalBeam)
        {
            canvas.Fill(x, y + 1, 3, 1, Rock);
        }
        if (ty < room.WaterLevel)
        {
            TintWater(canvas, x, y);
        }
        PaintShortcut(canvas, tile.ShortCut, x, y);
        if (tile.Terrain == Terrain.ShortcutEntrance)
        {
            PaintEntrance(canvas, room, tx, ty, x, y);
        }
    }

    /// <summary>A slope is a 2 by 2 block of rock in the corner it leans to, with one more pixel at each of two corners.</summary>
    private static void PaintSlope(PixelCanvas canvas, SlopeDirection direction, int x, int y)
    {
        switch (direction)
        {
            case SlopeDirection.UpRight:
                Slope(canvas, x, y, x, y + 2, x + 2, y);
                break;
            case SlopeDirection.UpLeft:
                Slope(canvas, x + 1, y, x + 2, y + 2, x, y);
                break;
            case SlopeDirection.DownRight:
                Slope(canvas, x, y + 1, x + 2, y + 2, x, y);
                break;
            case SlopeDirection.DownLeft:
                Slope(canvas, x + 1, y + 1, x, y + 2, x + 2, y);
                break;
        }
    }

    private static void Slope(PixelCanvas canvas, int blockX, int blockY, int ax, int ay, int bx, int by)
    {
        canvas.Fill(blockX, blockY, 2, 2, Rock);
        canvas.Set(ax, ay, Rock);
        canvas.Set(bx, by, Rock);
    }

    private static void TintWater(PixelCanvas canvas, int x, int y)
    {
        for (var dy = 0; dy < TileSize; dy++)
        {
            for (var dx = 0; dx < TileSize; dx++)
            {
                canvas.Set(x + dx, y + dy, Rgba.Lerp(canvas.Get(x + dx, y + dy), Water, WaterTint));
            }
        }
    }

    private static void PaintShortcut(PixelCanvas canvas, int shortcut, int x, int y)
    {
        if (shortcut == 1)
        {
            canvas.Set(x + 1, y + 1, ShortcutPath);
        }
        else if (ShortcutMarks.TryGetValue(shortcut, out var color))
        {
            foreach (var (dx, dy) in Plus)
            {
                canvas.Set(x + dx, y + dy, color);
            }
        }
    }

    /// <summary>A shortcut entrance is drawn as a short line on the side the shortcut path leaves from.</summary>
    private static void PaintEntrance(PixelCanvas canvas, RoomTiles room, int tx, int ty, int x, int y)
    {
        if (room.At(tx - 1, ty).ShortCut > 0)
        {
            Line(canvas, x, y, (2, 0), (2, 1), (2, 2));
        }
        if (room.At(tx + 1, ty).ShortCut > 0)
        {
            Line(canvas, x, y, (0, 0), (0, 1), (0, 2));
        }
        if (room.At(tx, ty + 1).ShortCut > 0)
        {
            Line(canvas, x, y, (0, 0), (1, 0), (2, 0));
        }
        if (room.At(tx, ty - 1).ShortCut > 0)
        {
            Line(canvas, x, y, (0, 2), (1, 2), (2, 2));
        }
    }

    private static void Line(PixelCanvas canvas, int x, int y, params (int X, int Y)[] pixels)
    {
        foreach (var (dx, dy) in pixels)
        {
            canvas.Set(x + dx, y + dy, ShortcutPath);
        }
        canvas.Set(x + 1, y + 1, ShortcutPath);
    }
}
