#nullable enable
using System;
using System.Collections.Generic;
using Warp.Model;

namespace Warp.Preview;

/// <summary>
/// Draws a room's tiles as a small map: each tile is 3 by 3 pixels, with walls, floors, slopes, beams, water and the
/// shortcuts in different shades or colors.
/// </summary>
internal static class PreviewPainter
{
    /// <summary>Pixels along a tile's edge.</summary>
    public const int TileSize = 3;

    private static readonly Rgba _background = Rgba.Grey(0.35f);
    private static readonly Rgba _wallBehind = Rgba.Grey(0.25f);
    private static readonly Rgba _rock = Rgba.Grey(0.2f);
    private static readonly Rgba _floorTop = Rgba.Grey(0.21f);
    private static readonly Rgba _shortcutPath = Rgba.Grey(0.5f);
    private static readonly Rgba _water = new(0f, 0.5f, 1f);
    private const float WaterTint = 0.2f;

    private static readonly Dictionary<ShortcutKind, Rgba> _shortcutMarks = new()
    {
        [ShortcutKind.Exit] = new Rgba(0.2f, 1f, 0.2f),
        [ShortcutKind.Den] = new Rgba(1f, 0.2f, 0.2f),
        [ShortcutKind.WhackAMole] = new Rgba(0.2f, 0.2f, 1f),
        [ShortcutKind.GateRoom] = new Rgba(1f, 1f, 0.2f)
    };

    private static readonly (int X, int Y)[] _plus = [(1, 0), (0, 1), (2, 1), (1, 2), (1, 1)];

    /// <summary>
    /// The room tile under the pointer in the enlarged preview, or null if the pointer is outside the room. The
    /// preview is centered on the sprite and each tile is <see cref="TileSize"/> texture pixels.
    /// </summary>
    public static TileCoord? TileAt(
        float pointerX, float pointerY, float spriteX, float spriteY, float spriteScale, int width, int height)
    {
        float x = (pointerX - spriteX) / (spriteScale * TileSize) + width / 2f;
        float y = (pointerY - spriteY) / (spriteScale * TileSize) + height / 2f;
        if (x <= 0 || x >= width || y <= 0 || y >= height)
        {
            return null;
        }
        return new TileCoord((int)Math.Floor(x), (int)Math.Floor(y));
    }

    /// <summary> Draws the room, three pixels to a tile. </summary>
    public static PixelCanvas Paint(RoomTiles room)
    {
        var canvas = new PixelCanvas(room.Width * TileSize, room.Height * TileSize, _background);
        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                PaintTile(canvas, room, x, y);
            }
        }
        return canvas;
    }

    /// <summary>
    /// Draws one tile: wall behind, rock, slope, floor, beams, water tint, then shortcut marks, in that order, each
    /// over the last.
    /// </summary>
    private static void PaintTile(PixelCanvas canvas, RoomTiles room, int tx, int ty)
    {
        Tile tile = room.At(tx, ty);
        int x = tx * TileSize, y = ty * TileSize;

        if (tile.WallBehind)
        {
            canvas.Fill(x, y, TileSize, TileSize, _wallBehind);
        }
        if (tile.Terrain == Terrain.Solid)
        {
            canvas.Fill(x, y, TileSize, TileSize, _rock);
        }
        PaintSlope(canvas, room.SlopeAt(tx, ty), x, y);
        if (tile.Terrain == Terrain.Floor)
        {
            canvas.Fill(x, y + 1, TileSize, 2, _floorTop);
        }
        if (tile.VerticalBeam)
        {
            canvas.Fill(x + 1, y, 1, TileSize, _rock);
        }
        if (tile.HorizontalBeam)
        {
            canvas.Fill(x, y + 1, TileSize, 1, _rock);
        }
        if (ty < room.WaterLevel)
        {
            TintWater(canvas, x, y);
        }
        PaintShortcut(canvas, tile.Shortcut, x, y);
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
            case SlopeDirection.Broken:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }

    /// <summary> Draws one slope: a 2 by 2 block of rock, and one extra pixel at each of two corners. </summary>
    private static void Slope(PixelCanvas canvas, int blockX, int blockY, int ax, int ay, int bx, int by)
    {
        canvas.Fill(blockX, blockY, 2, 2, _rock);
        canvas.Set(ax, ay, _rock);
        canvas.Set(bx, by, _rock);
    }

    /// <summary> Tints a tile's pixels blue, to show it is under water. </summary>
    private static void TintWater(PixelCanvas canvas, int x, int y)
    {
        for (int dy = 0; dy < TileSize; dy++)
        {
            for (int dx = 0; dx < TileSize; dx++)
            {
                canvas.Set(x + dx, y + dy, Rgba.Lerp(canvas.Get(x + dx, y + dy), _water, WaterTint));
            }
        }
    }

    /// <summary>
    /// Marks a tile that is part of a shortcut: a dot for the path, a colored plus for the end of it.
    /// </summary>
    private static void PaintShortcut(PixelCanvas canvas, ShortcutKind shortcut, int x, int y)
    {
        if (shortcut == ShortcutKind.Path)
        {
            canvas.Set(x + 1, y + 1, _shortcutPath);
        }
        else if (_shortcutMarks.TryGetValue(shortcut, out Rgba color))
        {
            foreach ((int dx, int dy) in _plus)
            {
                canvas.Set(x + dx, y + dy, color);
            }
        }
    }

    /// <summary>A shortcut entrance is drawn as a short line on the side the shortcut path leaves from.</summary>
    private static void PaintEntrance(PixelCanvas canvas, RoomTiles room, int tx, int ty, int x, int y)
    {
        if (room.At(tx - 1, ty).Shortcut != ShortcutKind.None)
        {
            Line(canvas, x, y, (2, 0), (2, 1), (2, 2));
        }
        if (room.At(tx + 1, ty).Shortcut != ShortcutKind.None)
        {
            Line(canvas, x, y, (0, 0), (0, 1), (0, 2));
        }
        if (room.At(tx, ty + 1).Shortcut != ShortcutKind.None)
        {
            Line(canvas, x, y, (0, 0), (1, 0), (2, 0));
        }
        if (room.At(tx, ty - 1).Shortcut != ShortcutKind.None)
        {
            Line(canvas, x, y, (0, 2), (1, 2), (2, 2));
        }
    }

    /// <summary> Draws a short line in the shortcut color, with the dot in the middle of the tile. </summary>
    private static void Line(PixelCanvas canvas, int x, int y, params (int X, int Y)[] pixels)
    {
        foreach ((int dx, int dy) in pixels)
        {
            canvas.Set(x + dx, y + dy, _shortcutPath);
        }
        canvas.Set(x + 1, y + 1, _shortcutPath);
    }
}
