using System;
using System.IO;
using System.Linq;
using Menu;
using RWCustom;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Preview;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// A map of a room, opened by right-clicking its button. Left click on the map to warp to that spot; right click to
/// close it. Shift + E shows the export bar, which saves the map as a picture.
/// </summary>
internal sealed class RoomPreviewDialog : Dialog
{
    private const int FadeFrames = 10;
    private const int OpenCooldown = 30;
    private const float DimAlpha = 0.8f;

    private readonly WarpContainer _owner;
    private readonly PauseMenu _pause;
    private readonly string _room;
    private readonly string _region;
    private readonly RoomTiles _tiles;
    private readonly PixelCanvas _canvas;
    private readonly FSprite _dim;
    private readonly FSprite _sprite;
    private readonly FLabel _instructions;
    private readonly FLabel _exportLabel;
    private ExportBar _bar;
    private FSprite _progress;
    private int _cooldown = OpenCooldown;
    private int _opening = FadeFrames;
    private int _closing = -1;
    private float _lastDimAlpha;
    private float _lastSpriteAlpha;
    private int _scale = 1;
    private BorderMode _border = BorderMode.None;
    private int _exportTotal;
    private int _exportNext;
    private bool _exporting;

    /// <summary>The preview that is open, or null. The pause menu holds still while one is.</summary>
    public static RoomPreviewDialog Current
    {
        get; private set;
    }

    /// <summary>Opens the preview of a room. A room whose file cannot be read is logged and nothing opens.</summary>
    public static void Open(ProcessManager manager, WarpContainer owner, string room, string region, PauseMenu pause)
    {
        try
        {
            Current = new RoomPreviewDialog(manager, owner, room, region, pause);
            manager.ShowDialog(Current);
        }
        catch (Exception error)
        {
            WarpLog.Exception("opening the preview of " + room, error);
            Current = null;
            owner.PreviewClosed();
        }
    }

    private RoomPreviewDialog(ProcessManager manager, WarpContainer owner, string room, string region, PauseMenu pause)
        : base(manager)
    {
        _owner = owner;
        _pause = pause;
        _room = room;
        _region = region;
        _tiles = ReadTiles(room);
        _canvas = PreviewPainter.Paint(_tiles);

        var atlasName = "warpPreview_" + room;
        if (!Futile.atlasManager.DoesContainAtlas(atlasName))
        {
            Futile.atlasManager.LoadAtlasFromTexture(atlasName, ToTexture(_canvas), false);
        }
        var screen = Custom.rainWorld.options.ScreenSize;
        var centerX = Mathf.RoundToInt(screen.x / 2);
        var centerY = Mathf.RoundToInt(screen.y / 2);

        _dim = new FSprite("Futile_White", true) { x = centerX, y = centerY, scale = 5000f, color = Color.black, alpha = 0f };
        _dim.SetAnchor(0.5f, 0.5f);
        pages[0].Container.AddChild(_dim);

        _sprite = new FSprite(atlasName, true) { x = centerX + 0.01f, y = centerY, alpha = 0f };
        _sprite.SetAnchor(0.5f, 0.5f);
        _sprite.scale = Mathf.Min(
            screen.x / (_tiles.Width * PreviewPainter.TileSize), screen.y / (_tiles.Height * PreviewPainter.TileSize)) * 0.83f;
        pages[0].Container.AddChild(_sprite);

        _instructions = AddLabel(Translator.Translate("Left click on the preview to warp to that location  -  Right click to dismiss"),
            centerX, 20.01f, Color.white);
        _exportLabel = AddLabel(Translator.Translate("Shift + E  -  Export options"), centerX, 45f, new Color(0.5f, 0.5f, 0.5f));
    }

    private FLabel AddLabel(string text, float x, float y, Color color)
    {
        var label = new FLabel(Custom.GetFont(), text) { x = x + 0.01f, y = y, alpha = 0f, color = color };
        label.SetAnchor(0.5f, 0.5f);
        pages[0].Container.AddChild(label);
        return label;
    }

    private static RoomTiles ReadTiles(string room)
    {
        var lines = File.ReadAllLines(WorldLoader.FindRoomFile(room, false, ".txt"));
        RoomPreprocessor.VersionFix(ref lines);
        return RoomTiles.Parse(lines);
    }

    private static Texture2D ToTexture(PixelCanvas canvas)
    {
        var texture = new Texture2D(canvas.Width, canvas.Height);
        texture.SetPixels(canvas.ToArray().Select(pixel => new Color(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray());
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return texture;
    }

    /// <summary>Takes the preview's pictures off the screen, as when a warp starts.</summary>
    public void RemoveSprites()
    {
        _dim?.RemoveFromContainer();
        _sprite?.RemoveFromContainer();
        _instructions?.RemoveFromContainer();
    }

    // ---- Per frame ------------------------------------------------------------------------------------------------

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        _lastDimAlpha = _dim.alpha;
        _lastSpriteAlpha = _sprite.alpha;
        float dimTarget, spriteTarget, labelTarget;
        if (_opening >= 0)
        {
            var progress = Mathf.InverseLerp(FadeFrames, 0, _opening);
            dimTarget = labelTarget = Mathf.Lerp(0f, DimAlpha, progress);
            spriteTarget = progress;
        }
        else if (_closing >= 0)
        {
            _opening = -1;
            var progress = Mathf.InverseLerp(FadeFrames, 0, _closing);
            dimTarget = Mathf.Lerp(DimAlpha, 0f, progress);
            spriteTarget = labelTarget = Mathf.Lerp(1f, 0f, progress);
        }
        else
        {
            return;
        }
        _dim.alpha = Mathf.Lerp(_lastDimAlpha, dimTarget, timeStacker);
        _sprite.alpha = Mathf.Lerp(_lastSpriteAlpha, spriteTarget, timeStacker);
        _instructions.alpha = Mathf.Lerp(_lastDimAlpha, labelTarget, timeStacker);
        _exportLabel.alpha = _instructions.alpha;
    }

    public override void Update()
    {
        base.Update();
        _cooldown--;
        if (_opening >= 0)
        {
            _opening--;
        }
        if (_closing >= 0)
        {
            _closing--;
        }

        if (_cooldown <= 0 && _opening == -1 && _closing == -1 && Input.GetMouseButton(1))
        {
            _closing = FadeFrames;
        }
        if (_closing < 0 && Input.GetMouseButton(0) && !(_bar?.IsMouseOver ?? false))
        {
            WarpToClickedSpot();
        }
        if (_closing == 0)
        {
            Close();
        }
        if (_bar == null && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKey(KeyCode.E))
        {
            ShowExportBar();
        }
        if (_exporting)
        {
            ExportNextRoom();
        }
    }

    private void WarpToClickedSpot()
    {
        var mouse = Input.mousePosition;
        if (ExportOptions.TileAt(mouse.x, mouse.y, _sprite.x, _sprite.y, _sprite.scale, _tiles.Width, _tiles.Height) is not { } tile)
        {
            return;
        }
        WarpState.RequestWarp(new WarpRequest(_region, _room, tile));
        _pause.Singal(null, "CONTINUE");
        _closing = 0;
    }

    private void Close()
    {
        pages[0].Container.RemoveAllChildren();
        manager.StopSideProcess(this);
        _owner.PreviewClosed();
        Current = null;
    }

    public override void ShutDownProcess()
    {
        base.ShutDownProcess();
        pages[0].Container.RemoveAllChildren();
    }

    // ---- Export ---------------------------------------------------------------------------------------------------

    private void ShowExportBar()
    {
        _bar = new ExportBar(this, pages[0]);
        _exportLabel.y = 20f;
        _exportLabel.color = new Color(0.8f, 0.8f, 0.8f);
        _instructions.text = "";
        ShowFileName();
    }

    private void ShowFileName()
    {
        _exportLabel.text = Translator.Translate("{0} x {1} pixels  -  {2}_preview.png")
            .Replace("{0}", (_canvas.Width * _scale).ToString())
            .Replace("{1}", (_canvas.Height * _scale).ToString())
            .Replace("{2}", _room);
    }

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        switch (message)
        {
            case ExportBar.ScaleSignal:
                _scale = ExportOptions.NextScale(_scale);
                _bar.ScaleText = Translator.Translate("SCALE: X" + _scale);
                ShowFileName();
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.BorderSignal:
                _border = ExportOptions.Next(_border);
                _bar.BorderText = Translator.Translate("BORDERS: " + BorderName(_border));
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.ExportSignal:
                _exportLabel.text = Translator.Translate("Saved: {0}").Replace("{0}", Save(_room, _canvas));
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.ExportRegionSignal when !_exporting:
                _exporting = true;
                _exportTotal = RegionCatalog.Get(_region).Rooms.Length;
                _exportNext = 0;
                _exportLabel.text = "";
                break;
            case ExportBar.OpenSignal:
                Application.OpenURL(WarpFiles.ExportFolder);
                break;
        }
    }

    private static string BorderName(BorderMode mode) => mode switch
    {
        BorderMode.All => "ALL",
        BorderMode.Shelters => "SHELTER",
        BorderMode.NotRooms => "TYPES",
        _ => "NONE"
    };

    /// <summary>Saves one room's picture, with the chosen border and scale, and returns where it went.</summary>
    private string Save(string room, PixelCanvas preview)
    {
        var info = RegionCatalog.Get(_region).Rooms.FirstOrDefault(candidate => candidate.Name == room);
        var type = info?.Type ?? RoomType.Room;
        Rgba? border = ExportOptions.HasBorder(_border, type) ? UiColors.ToRgba(WarpState.Palette.Type((int)type)) : null;
        var picture = ExportOptions.Compose(preview, _scale, border);

        var folder = WarpFiles.ExportFolderOf(_region);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, room + "_preview.png");
        var texture = ToTexture(picture);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
        return path;
    }

    /// <summary>Exports one more room of the region, so a whole region is saved over many frames and the game keeps drawing.</summary>
    private void ExportNextRoom()
    {
        if (_progress == null)
        {
            _progress = new FSprite("pixel", true) { x = 0f, y = 0f, scaleY = 25f, scaleX = 0f };
            _progress.SetAnchor(0f, 0.5f);
            pages[0].Container.AddChild(_progress);
        }
        if (_exportNext < _exportTotal)
        {
            var room = RegionCatalog.Get(_region).Rooms[_exportNext].Name;
            try
            {
                Save(room, PreviewPainter.Paint(ReadTiles(room)));
            }
            catch (Exception error)
            {
                WarpLog.Exception("exporting " + room, error);
            }
            _exportNext++;
        }
        _progress.scaleX = Mathf.Lerp(0f, Custom.rainWorld.options.ScreenSize.x, Mathf.InverseLerp(0, _exportTotal, _exportNext));
        if (_exportNext >= _exportTotal)
        {
            _exporting = false;
            _progress.alpha = 0f;
            _exportLabel.text = Translator.Translate("Finished exporting {0} rooms").Replace("{0}", _exportTotal.ToString());
        }
    }

    /// <summary>The row of export buttons along the bottom of the preview.</summary>
    private sealed class ExportBar
    {
        public const string ScaleSignal = "SCALE";
        public const string BorderSignal = "BORDER";
        public const string ExportSignal = "EXPORT";
        public const string ExportRegionSignal = "EXPORTREGION";
        public const string OpenSignal = "OPEN";

        private static readonly Color Tint = new(0.5f, 0.5f, 0.5f);

        private readonly WarpButton _border;
        private readonly WarpButton _scale;
        private readonly WarpButton[] _all;

        public ExportBar(Menu.Menu menu, Page page)
        {
            _border = Add(menu, page, "BORDERS: NONE", BorderSignal, new Vector2(683f - 300f, 30f), 130f);
            _scale = Add(menu, page, "SCALE: X1", ScaleSignal, Next(_border), 90f);
            var export = Add(menu, page, "EXPORT", ExportSignal, Next(_scale), 80f);
            var region = Add(menu, page, "EXPORT REGION", ExportRegionSignal, Next(export), 120f);
            var open = Add(menu, page, "OPEN FOLDER", OpenSignal, Next(region), 100f);
            _all = new[] { _border, _scale, export, region, open };
        }

        public bool IsMouseOver => _all.Any(button => button.IsMouseOverMe);

        public string BorderText
        {
            set => _border.Text = value;
        }

        public string ScaleText
        {
            set => _scale.Text = value;
        }

        private static Vector2 Next(WarpButton button) => button.pos + new Vector2(button.size.x + 10f, 0f);

        private static WarpButton Add(Menu.Menu menu, Page page, string text, string signal, Vector2 pos, float width)
        {
            var button = new WarpButton(menu, page, Translator.Translate(text), signal, pos, new Vector2(width, 30f), Tint);
            page.subObjects.Add(button);
            return button;
        }
    }
}
