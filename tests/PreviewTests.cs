using Warp.Model;
using Warp.Preview;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for reading a room file and drawing its preview (src/Preview).</summary>
internal static class PreviewTests
{
    public static void Run()
    {
        Group(nameof(ParseTests), ParseTests);
        Group(nameof(EdgeTests), EdgeTests);
        Group(nameof(SlopeTests), SlopeTests);
        Group(nameof(PaintTests), PaintTests);
        Group(nameof(CanvasTests), CanvasTests);
        Group(nameof(ExportTests), ExportTests);
    }

    private static readonly Rgba _rock = Rgba.Grey(0.2f);
    private static readonly Rgba _background = Rgba.Grey(0.35f);

    private static Tile Air => new(Terrain.Air, false, false, false, 0);

    private static RoomTiles Room(Tile[,] tiles, int waterLevel = -1) => new(tiles, waterLevel);

    private static Tile Of(Terrain terrain) => Air with { Terrain = terrain };

    private static string[] RoomFile(string size, string tiles)
    {
        string[] lines = new string[12];
        lines[0] = "SU_TEST";
        lines[1] = size;
        lines[11] = tiles;
        return lines;
    }

    private static void ParseTests()
    {
        // Columns run top to bottom from the left: column 0 is (solid, floor), column 1 is (beam, wall behind)
        var room = RoomTiles.Parse(RoomFile("2*2|-1|0", "1|3|0,1|0,6|"));
        CheckEqual("width", 2, room.Width);
        CheckEqual("height", 2, room.Height);
        CheckEqual("no water", -1, room.WaterLevel);
        CheckEqual("the first tile is the top left", Terrain.Solid, room.At(0, 1).Terrain);
        CheckEqual("then down the column", Terrain.Floor, room.At(0, 0).Terrain);
        CheckEqual("a vertical beam", true, room.At(1, 1).VerticalBeam);
        CheckEqual("a wall behind", true, room.At(1, 0).WallBehind);

        var flags = RoomTiles.Parse(RoomFile("1*2|3|0", "0,2,4|0,3,3,5,12|"));
        CheckEqual("water level is read", 3, flags.WaterLevel);
        CheckEqual("a horizontal beam and an exit", (true, 2), (flags.At(0, 1).HorizontalBeam, flags.At(0, 1).ShortCut));
        CheckEqual("the highest shortcut flag wins when listed last", 5, flags.At(0, 0).ShortCut);

        var first = RoomTiles.Parse(RoomFile("1*1|-1|0", "0,3,4,3|"));
        CheckEqual("a path flag does not replace an exit", 2, first.At(0, 0).ShortCut);

        var short1 = RoomTiles.Parse(RoomFile("2*2|-1|0", "1|1|"));
        CheckEqual("a file with too few tiles leaves the rest as air", Terrain.Air, short1.At(1, 1).Terrain);
    }

    private static void EdgeTests()
    {
        Tile[,] tiles = new[,] { { Of(Terrain.Solid), Of(Terrain.ShortcutEntrance) }, { Air, Air with { ShortCut = 2 } } };
        var room = Room(tiles);
        CheckEqual("inside the room", Terrain.ShortcutEntrance, room.At(0, 1).Terrain);
        CheckEqual("outside reads the nearest tile", Terrain.Solid, room.At(-3, 0).Terrain);
        CheckEqual("an entrance past the edge reads as rock", Terrain.Solid, room.At(0, 5).Terrain);
        CheckEqual("a shortcut past the edge loses its shortcut", (Terrain.Air, 0), (room.At(5, 5).Terrain, room.At(5, 5).ShortCut));
    }

    private static void SlopeTests()
    {
        Tile[,] Grid(int x, int y) => new Tile[3, 3].Fill(Air).With(1, 1, Of(Terrain.Slope)).With(x, y, Of(Terrain.Solid));
        CheckEqual("rock to the left and below leans up and right", SlopeDirection.UpRight,
            Room(Grid(0, 1).With(1, 0, Of(Terrain.Solid))).SlopeAt(1, 1));
        CheckEqual("rock to the left and above", SlopeDirection.DownRight,
            Room(Grid(0, 1).With(1, 2, Of(Terrain.Solid))).SlopeAt(1, 1));
        CheckEqual("rock to the right and below", SlopeDirection.UpLeft,
            Room(Grid(2, 1).With(1, 0, Of(Terrain.Solid))).SlopeAt(1, 1));
        CheckEqual("rock to the right and above", SlopeDirection.DownLeft,
            Room(Grid(2, 1).With(1, 2, Of(Terrain.Solid))).SlopeAt(1, 1));
        CheckEqual("a slope with no rock beside it is broken", SlopeDirection.Broken, Room(Grid(0, 0)).SlopeAt(1, 1));
        CheckEqual("a tile that is not a slope is broken", SlopeDirection.Broken, Room(Grid(0, 1)).SlopeAt(0, 1));
    }

    private static void PaintTests()
    {
        PixelCanvas canvas = PreviewPainter.Paint(Room(new[,] { { Of(Terrain.Solid), Air }, { Of(Terrain.Floor), Air } }));
        CheckEqual("the picture is three pixels to a tile", (6, 6), (canvas.Width, canvas.Height));
        CheckEqual("air is the background", _background, canvas.Get(4, 4));
        CheckEqual("rock fills its tile", true, All(canvas, 0, 0, 3, 3, _rock));
        CheckEqual("a floor leaves its bottom row clear", (_background, Rgba.Grey(0.21f)), (canvas.Get(3, 0), canvas.Get(3, 1)));

        PixelCanvas beams = PreviewPainter.Paint(Room(new[,] { { Air with { VerticalBeam = true, HorizontalBeam = true } } }));
        CheckEqual("beams cross in the middle", (_rock, _rock, _background), (beams.Get(1, 0), beams.Get(0, 1), beams.Get(0, 0)));

        PixelCanvas water = PreviewPainter.Paint(Room(new[,] { { Air, Air } }, waterLevel: 1));
        CheckEqual("under the water level the tile is bluer", true, water.Get(0, 0).B > _background.B);
        CheckEqual("at the water level it is not", _background, water.Get(0, 3));

        PixelCanvas exit = PreviewPainter.Paint(Room(new[,] { { Air with { ShortCut = 2 } } }));
        CheckEqual("an exit is a green plus", (new Rgba(0.2f, 1f, 0.2f), _background), (exit.Get(1, 1), exit.Get(0, 0)));
        PixelCanvas path = PreviewPainter.Paint(Room(new[,] { { Air with { ShortCut = 1 } } }));
        CheckEqual("a path is a grey dot", (Rgba.Grey(0.5f), _background), (path.Get(1, 1), path.Get(0, 0)));

        PixelCanvas entrance = PreviewPainter.Paint(Room(new[,] { { Of(Terrain.ShortcutEntrance), Air with { ShortCut = 1 } } }));
        CheckEqual("an entrance with a shortcut above it draws a line along its bottom edge",
            (Rgba.Grey(0.5f), Rgba.Grey(0.5f), _background), (entrance.Get(0, 0), entrance.Get(2, 0), entrance.Get(0, 2)));

        PixelCanvas slope = PreviewPainter.Paint(Room(new[,]
        {
            { Of(Terrain.Solid), Of(Terrain.Solid), Air }, { Of(Terrain.Solid), Of(Terrain.Slope), Air }, { Air, Air, Air }
        }));
        CheckEqual("a slope fills its corner", (_rock, _rock, _background), (slope.Get(3, 3), slope.Get(4, 4), slope.Get(5, 5)));
    }

    private static bool All(PixelCanvas canvas, int x, int y, int width, int height, Rgba color)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                if (canvas.Get(x + dx, y + dy) != color)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void CanvasTests()
    {
        var canvas = new PixelCanvas(2, 1, _background);
        canvas.Set(1, 0, _rock);
        canvas.Set(9, 9, _rock);
        CheckEqual("a pixel off the canvas is ignored", 2, canvas.ToArray().Length);

        PixelCanvas scaled = canvas.Scaled(3);
        CheckEqual("scaling repeats pixels", (6, 3, _rock, _background), (scaled.Width, scaled.Height, scaled.Get(5, 2), scaled.Get(2, 1)));
        CheckEqual("a scale of one is the same canvas", true, ReferenceEquals(canvas, canvas.Scaled(1)));

        PixelCanvas bordered = canvas.WithBorder(3, new Rgba(1f, 0f, 0f));
        CheckEqual("a border adds its width on every side", (8, 7), (bordered.Width, bordered.Height));
        CheckEqual("the border has its color and the picture is inside", (new Rgba(1f, 0f, 0f), _background, _rock),
            (bordered.Get(0, 0), bordered.Get(3, 3), bordered.Get(4, 3)));

        CheckEqual("Lerp goes part of the way", Rgba.Grey(0.5f), Rgba.Lerp(Rgba.Grey(0f), Rgba.Grey(1f), 0.5f));
        CheckEqual("and stops at the end", Rgba.Grey(1f), Rgba.Lerp(Rgba.Grey(0f), Rgba.Grey(1f), 3f));
    }

    private static void ExportTests()
    {
        CheckSequence("the border button goes round", [BorderMode.All, BorderMode.Shelters, BorderMode.NotRooms, BorderMode.None
            ],
            [
                ExportOptions.Next(BorderMode.None), ExportOptions.Next(BorderMode.All),
                ExportOptions.Next(BorderMode.Shelters), ExportOptions.Next(BorderMode.NotRooms)
            ]);
        CheckSequence("scales go round", [2, 5, 10, 1],
            [ExportOptions.NextScale(1), ExportOptions.NextScale(2), ExportOptions.NextScale(5), ExportOptions.NextScale(10)
            ]);

        CheckEqual("no borders", false, ExportOptions.HasBorder(BorderMode.None, RoomType.Shelter));
        CheckEqual("all borders", true, ExportOptions.HasBorder(BorderMode.All, RoomType.Room));
        CheckEqual("shelter borders: a shelter", true, ExportOptions.HasBorder(BorderMode.Shelters, RoomType.Shelter));
        CheckEqual("shelter borders: a gate", false, ExportOptions.HasBorder(BorderMode.Shelters, RoomType.Gate));
        CheckEqual("type borders: an ordinary room", false, ExportOptions.HasBorder(BorderMode.NotRooms, RoomType.Room));
        CheckEqual("type borders: a trader", true, ExportOptions.HasBorder(BorderMode.NotRooms, RoomType.ScavTrader));

        var preview = new PixelCanvas(2, 2, _background);
        PixelCanvas plain = ExportOptions.Compose(preview, 2, null);
        PixelCanvas framed = ExportOptions.Compose(preview, 2, new Rgba(1f, 0f, 0f));
        CheckEqual("a plain picture is just scaled", (4, 4), (plain.Width, plain.Height));
        CheckEqual("a framed picture has the border scaled too", (16, 16), (framed.Width, framed.Height));

        CheckEqual("the middle of the preview is the middle of the room", new TileCoord(5, 4),
            ExportOptions.TileAt(100f, 200f, 100f, 200f, 2f, 10, 8));
        CheckEqual("each tile is three texture pixels wide", new TileCoord(6, 4),
            ExportOptions.TileAt(106f, 200f, 100f, 200f, 2f, 10, 8));
        CheckEqual("outside the room is nothing", null, ExportOptions.TileAt(0f, 0f, 100f, 200f, 2f, 10, 8));

        // Tiles are 6 pixels wide on screen here (scale 2, three texture pixels to a tile), so tile 5 of a room 10
        // wide spans 100 to 106 when the sprite is centered on 100.
        CheckEqual("even room: left half of a tile", new TileCoord(5, 4), ExportOptions.TileAt(101f, 201f, 100f, 200f, 2f, 10, 8));
        CheckEqual("even room: right half of the same tile", new TileCoord(5, 4), ExportOptions.TileAt(105f, 205f, 100f, 200f, 2f, 10, 8));
        CheckEqual("even room: the next tile starts at its edge", new TileCoord(6, 5), ExportOptions.TileAt(106f, 206f, 100f, 200f, 2f, 10, 8));
        CheckEqual("odd room: left half of the middle tile", new TileCoord(4, 3), ExportOptions.TileAt(98f, 198f, 100f, 200f, 2f, 9, 7));
        CheckEqual("odd room: right half of the middle tile", new TileCoord(4, 3), ExportOptions.TileAt(102f, 202f, 100f, 200f, 2f, 9, 7));
        CheckEqual("odd room: the tiles either side", (new TileCoord(3, 2), new TileCoord(5, 4)),
            (ExportOptions.TileAt(96f, 196f, 100f, 200f, 2f, 9, 7)!.Value, ExportOptions.TileAt(104f, 204f, 100f, 200f, 2f, 9, 7)!.Value));
        CheckEqual("the room's own edge is outside it", null, ExportOptions.TileAt(70f, 200f, 100f, 200f, 2f, 10, 8));
        CheckEqual("just inside the edge is the first tile", new TileCoord(0, 4), ExportOptions.TileAt(71f, 201f, 100f, 200f, 2f, 10, 8));
    }
}

/// <summary>Small helpers for building tile grids in the tests above.</summary>
internal static class TileGridExtensions
{
    public static Tile[,] Fill(this Tile[,] grid, Tile tile)
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                grid[x, y] = tile;
            }
        }
        return grid;
    }

    public static Tile[,] With(this Tile[,] grid, int x, int y, Tile tile)
    {
        grid[x, y] = tile;
        return grid;
    }
}
