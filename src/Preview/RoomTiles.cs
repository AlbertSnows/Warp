#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Warp.Util;

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
/// <param name="Terrain">What the tile is made of.</param>
/// <param name="VerticalBeam">Whether a vertical pole runs through the tile.</param>
/// <param name="HorizontalBeam">Whether a horizontal pole runs through the tile.</param>
/// <param name="WallBehind">Whether there is a back wall behind the tile.</param>
/// <param name="Shortcut">What part of a shortcut the tile is, if any.</param>
internal readonly record struct Tile(
    Terrain Terrain, bool VerticalBeam, bool HorizontalBeam, bool WallBehind, ShortcutKind Shortcut);

/// <summary> What part of a shortcut a tile is. </summary>
internal enum ShortcutKind
{
    None,
    Path,
    Exit,
    Den,
    WhackAMole,
    GateRoom
}

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
        // The cells run down each column in turn, starting at the top of the first; the last entry is empty
        int cellCount = Math.Min(cells.Length - 1, width * height);
        for (int i = 0; i < cellCount; i++)
        {
            tiles[i / height, height - 1 - i % height] = ParseTile(cells[i].Split(','));
        }
        return new RoomTiles(tiles, water);
    }

    /// <summary> The terrain a number in a room file stands for. A number that is not a known terrain reads as air. </summary>
    private static Terrain ParseTerrain(string text)
    {
        int? number = Numbers.ParseInt(text);
        return number != null && Enum.IsDefined(typeof(Terrain), number.Value) ? (Terrain)number.Value : Terrain.Air;
    }

    /// <summary> A tile from one "terrain,flag,flag" cell of a room file. </summary>
    private static Tile ParseTile(string[] fields)
    {
        var tile = new Tile(ParseTerrain(fields[0]), false, false, false, ShortcutKind.None);
        for (int i = 1; i < fields.Length; i++)
        {
            tile = fields[i] switch
            {
                "1" => tile with { VerticalBeam = true },
                "2" => tile with { HorizontalBeam = true },
                "3" => tile.Shortcut == ShortcutKind.None ? tile with { Shortcut = ShortcutKind.Path } : tile,
                "4" => tile with { Shortcut = ShortcutKind.Exit },
                "5" => tile with { Shortcut = ShortcutKind.Den },
                "9" => tile with { Shortcut = ShortcutKind.WhackAMole },
                "12" => tile with { Shortcut = ShortcutKind.GateRoom },
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
        if (edge.Terrain != Terrain.ShortcutEntrance && edge.Shortcut == ShortcutKind.None)
        {
            return edge;
        }
        return edge with
        {
            Terrain = edge.Terrain != Terrain.ShortcutEntrance ? edge.Terrain : Terrain.Solid,
            Shortcut = ShortcutKind.None
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
