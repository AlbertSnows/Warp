#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Warp.Preview;

/// <summary>The kinds of terrain a room file's tiles can have. The numbers are the game's (Room.Tile.TerrainType).</summary>
internal enum Terrain
{
    Air = 0,
    Solid = 1,
    Slope = 2,
    Floor = 3,
    ShortcutEntrance = 4
}

/// <summary>The way a slope tile leans, named by the corner the solid part is in.</summary>
internal enum SlopeDirection
{
    Broken,
    UpRight,
    UpLeft,
    DownRight,
    DownLeft
}

/// <summary>
/// One tile of a room.
/// </summary>
/// <param name="ShortCut">0 for none, 1 for a shortcut's path, 2 an exit, 3 a den, 4 a whack-a-mole hole, 5 a gate room.</param>
internal readonly record struct Tile(
    Terrain Terrain, bool VerticalBeam, bool HorizontalBeam, bool WallBehind, int ShortCut);

/// <summary>
/// The tiles of a room, read from its room file. Tile (0, 0) is the bottom left.
/// </summary>
internal sealed class RoomTiles
{
    private const int SizeLine = 1;
    private const int TileLine = 11;

    private readonly Tile[,] _tiles;

    /// <summary>A room from a grid that is already made. Used by <see cref="Parse"/>, and by the tests to build rooms by hand.</summary>
    internal RoomTiles(Tile[,] tiles, int waterLevel)
    {
        _tiles = tiles;
        WaterLevel = waterLevel;
    }

    /// <summary> How many tiles wide the room is. </summary>
    public int Width => _tiles.GetLength(0);

    /// <summary> How many tiles high the room is. </summary>
    public int Height => _tiles.GetLength(1);

    /// <summary>The highest row that is under water, or -1 for a room with no water.</summary>
    public int WaterLevel
    {
        get;
    }

    /// <summary>
    /// Reads a room file's lines, after the game has brought an old file to the current version. The second line holds
    /// the size ("72*43") and the water level ("|-1" for none); the twelfth holds the tiles, a column at a time from
    /// the top left, each as "terrain,flag,flag" and ending in "|".
    /// </summary>
    public static RoomTiles Parse(IReadOnlyList<string> lines)
    {
        string[] header = lines[SizeLine].Split('|');
        string[] size = header[0].Split('*');
        int width = Convert.ToInt32(size[0], CultureInfo.InvariantCulture);
        int height = Convert.ToInt32(size[1], CultureInfo.InvariantCulture);
        int water = header[1] == "-1" ? -1 : Convert.ToInt32(header[1], CultureInfo.InvariantCulture);

        var tiles = new Tile[width, height];
        string[] cells = lines[TileLine].Split('|');
        int x = 0, y = height - 1;
        for (int i = 0; i < cells.Length - 1 && x < width; i++)
        {
            tiles[x, y] = ParseTile(cells[i].Split(','));
            if (--y >= 0)
            {
                continue;
            }

            x++;
            y = height - 1;
        }
        return new RoomTiles(tiles, water);
    }

    /// <summary> A tile from one "terrain,flag,flag" cell of a room file. </summary>
    private static Tile ParseTile(string[] fields)
    {
        var tile = new Tile((Terrain)int.Parse(fields[0], NumberStyles.Any, CultureInfo.InvariantCulture), false, false, false, 0);
        for (int i = 1; i < fields.Length; i++)
        {
            tile = fields[i] switch
            {
                "1" => tile with { VerticalBeam = true },
                "2" => tile with { HorizontalBeam = true },
                "3" => tile.ShortCut < 1 ? tile with { ShortCut = 1 } : tile,
                "4" => tile with { ShortCut = 2 },
                "5" => tile with { ShortCut = 3 },
                "9" => tile with { ShortCut = 4 },
                "12" => tile with { ShortCut = 5 },
                "6" => tile with { WallBehind = true },
                _ => tile
            };
        }
        return tile;
    }

    /// <summary>
    /// The tile at a position. Past the edge of the room it is the nearest tile inside, except that a shortcut entrance
    /// or shortcut path there reads as plain solid rock (or as its own terrain, without the shortcut).
    /// </summary>
    public Tile At(int x, int y)
    {
        if (x > -1 && y > -1 && x < Width && y < Height)
        {
            return _tiles[x, y];
        }
        Tile edge = _tiles[Math.Min(Math.Max(x, 0), Width - 1), Math.Min(Math.Max(y, 0), Height - 1)];
        if (edge.Terrain != Terrain.ShortcutEntrance && edge.ShortCut == 0)
        {
            return edge;
        }
        return edge with
        {
            Terrain = edge.Terrain != Terrain.ShortcutEntrance ? edge.Terrain : Terrain.Solid,
            ShortCut = 0
        };
    }

    /// <summary>Which way a slope tile leans, from the solid tiles next to it. Broken if it is not a slope, or touches no rock.</summary>
    public SlopeDirection SlopeAt(int x, int y)
    {
        if (At(x, y).Terrain != Terrain.Slope)
        {
            return SlopeDirection.Broken;
        }
        if (IsSolid(x - 1, y))
        {
            return IsSolid(x, y - 1) ? SlopeDirection.UpRight : IsSolid(x, y + 1) ? SlopeDirection.DownRight : SlopeDirection.Broken;
        }
        if (IsSolid(x + 1, y))
        {
            return IsSolid(x, y - 1) ? SlopeDirection.UpLeft : IsSolid(x, y + 1) ? SlopeDirection.DownLeft : SlopeDirection.Broken;
        }
        return SlopeDirection.Broken;
    }

    private bool IsSolid(int x, int y) => At(x, y).Terrain == Terrain.Solid;
}
