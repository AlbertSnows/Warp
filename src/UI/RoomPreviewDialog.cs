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
    private ExportOptions _options = new();
    private readonly RoomInfo[] _rooms;
    private int _exportNext;
    private bool _exporting;
    private readonly string _atlasName;
    private bool _atlasUnloaded;

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
            // A constructor that throws has cleaned up after itself, so only a failure to show it is dealt with here
            Current = new RoomPreviewDialog(manager, owner, room, region, pause);
            manager.ShowDialog(Current);
        }
        catch (Exception error)
        {
            WarpLog.Exception("opening the preview of " + room, error);
            Current?.ReleaseScreen();
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
        // The region's rooms are taken once, here, so looking a room up later reads nothing from disk and cannot throw
        _rooms = RegionCatalog.Get(region).Rooms;
        _tiles = ReadTiles(room);
        _canvas = PreviewPainter.Paint(_tiles);

        _atlasName = "warpPreview_" + room;
        try
        {
            // Always made afresh, so a room file that changed since it was last previewed is not shown as it was
            UnloadAtlasNamed(_atlasName);
            Futile.atlasManager.LoadAtlasFromTexture(_atlasName, _canvas.ToTexture(), false);
            Vector2 screen = UiLayout.Screen(Custom.rainWorld);
            int centerX = Mathf.RoundToInt(screen.x / 2);
            int centerY = Mathf.RoundToInt(screen.y / 2);

            // The extra 0.01 on some positions is inherited from upstream Warp
            _dim = new FSprite("Futile_White") { x = centerX, y = centerY, scale = 5000f, color = Color.black, alpha = 0f };
            _dim.SetAnchor(0.5f, 0.5f);
            pages[0].Container.AddChild(_dim);

            _sprite = new FSprite(_atlasName) { x = centerX + 0.01f, y = centerY, alpha = 0f };
            _sprite.SetAnchor(0.5f, 0.5f);
            _sprite.scale = Mathf.Min(
                screen.x / (_tiles.Width * PreviewPainter.TileSize), screen.y / (_tiles.Height * PreviewPainter.TileSize)) * 0.83f;
            pages[0].Container.AddChild(_sprite);

            _instructions = AddLabel(Translator.Translate("Left click on the preview to warp to that location  -  Right click to dismiss"),
                centerX, 20.01f, Color.white);
            _exportLabel = AddLabel(Translator.Translate("Shift + E  -  Export options"), centerX, 45f, new Color(0.5f, 0.5f, 0.5f));
        }
        catch
        {
            // Something was added to the screen or loaded before the fault; give it back, so a failed preview leaves nothing
            ReleaseScreen();
            throw;
        }
    }

    /// <summary> Adds a centered text line to the preview, invisible until it fades in. </summary>
    private FLabel AddLabel(string text, float x, float y, Color color)
    {
        var label = new FLabel(Custom.GetFont(), text) { x = x + 0.01f, y = y, alpha = 0f, color = color };
        label.SetAnchor(0.5f, 0.5f);
        pages[0].Container.AddChild(label);
        return label;
    }

    /// <summary> Reads a room's tiles from its file, after the game has brought an old file up to date. </summary>
    private static RoomTiles ReadTiles(string room)
    {
        string path = RoomFiles.Find(room) ?? throw new FileNotFoundException("No room file for " + room);
        string[] lines = File.ReadAllLines(path);
        RoomPreprocessor.VersionFix(ref lines);
        return RoomTiles.Parse(lines);
    }

    /// <summary>Takes the preview's pictures off the screen, as when a warp starts.</summary>
    public void RemoveSprites()
    {
        _dim?.RemoveFromContainer();
        _sprite?.RemoveFromContainer();
        _instructions?.RemoveFromContainer();
        _exportLabel?.RemoveFromContainer();
        _progress?.RemoveFromContainer();
        _bar?.RemoveSprites();
    }

    // ---- Per frame ------------------------------------------------------------------------------------------------

    /// <summary> Fades the preview in when it opens and out when it closes. </summary>
    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        float dimTarget, spriteTarget, labelTarget;
        if (_opening >= 0)
        {
            float progress = Mathf.InverseLerp(FadeFrames, 0, _opening);
            dimTarget = labelTarget = Mathf.Lerp(0f, DimAlpha, progress);
            spriteTarget = progress;
        }
        else if (_closing >= 0)
        {
            _opening = -1;
            float progress = Mathf.InverseLerp(FadeFrames, 0, _closing);
            dimTarget = labelTarget = Mathf.Lerp(DimAlpha, 0f, progress);
            spriteTarget = Mathf.Lerp(1f, 0f, progress);
        }
        else
        {
            return;
        }
        // Each one moves from its own alpha, so the texts fade in and out by the same amount
        Approach(_dim, dimTarget, timeStacker);
        Approach(_sprite, spriteTarget, timeStacker);
        Approach(_instructions, labelTarget, timeStacker);
        Approach(_exportLabel, labelTarget, timeStacker);
    }

    /// <summary> Moves a node's alpha towards a target by the share of the frame that has passed. </summary>
    private static void Approach(FNode node, float target, float timeStacker) => node.alpha = Mathf.Lerp(node.alpha, target, timeStacker);

    /// <summary>
    /// Counts the fades down, closes on a right click, warps on a left click, and opens the export bar on Shift +
    /// E.
    /// </summary>
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
        if (_bar == null && UiInput.ShiftHeld && Input.GetKey(KeyCode.E))
        {
            ShowExportBar();
        }
        if (_exporting)
        {
            ExportNextRoom();
        }
    }

    /// <summary>
    /// Warps to the tile under the pointer, if it is inside the room, and starts closing the preview.
    /// </summary>
    private void WarpToClickedSpot()
    {
        Vector3 mouse = Input.mousePosition;
        if (PreviewPainter.TileAt(mouse.x, mouse.y, _sprite.x, _sprite.y, _sprite.scale, _tiles.Width, _tiles.Height) is not { } tile)
        {
            return;
        }
        PauseMenuActions.StartWarp(_pause, new WarpRequest(_region, _room, tile));
        _closing = 0;
    }

    /// <summary> Removes the preview from the screen and tells the Warp menu it has gone. </summary>
    private void Close()
    {
        ReleaseScreen();
        manager.StopSideProcess(this);
        _owner.PreviewClosed();
        Current = null;
    }

    /// <summary> Gives the preview's picture back to the game, so a previewed room does not keep a texture for the rest of the session. </summary>
    private void UnloadAtlas()
    {
        if (_atlasUnloaded)
        {
            return;
        }
        _atlasUnloaded = true;
        UnloadAtlasNamed(_atlasName);
    }

    private static void UnloadAtlasNamed(string name)
    {
        if (Futile.atlasManager.DoesContainAtlas(name))
        {
            Futile.atlasManager.UnloadAtlas(name);
        }
    }

    /// <summary> Removes the preview's pictures when the game shuts the preview down. </summary>
    public override void ShutDownProcess()
    {
        base.ShutDownProcess();
        // This runs while the game is tearing the process down, so nothing here is allowed to throw out of it
        try
        {
            ReleaseScreen();
            // The game can shut the dialog down other than through Close, and a stale Current would leave the pause menu stuck
            if (ReferenceEquals(Current, this))
            {
                Current = null;
                _owner.PreviewClosed();
            }
        }
        catch (Exception error)
        {
            Guard.Report("shutting down the room preview", error);
        }
    }

    /// <summary> Takes everything the preview put on the screen off it, and gives its picture back to the game. Safe to call again. </summary>
    private void ReleaseScreen()
    {
        pages[0].Container.RemoveAllChildren();
        UnloadAtlas();
    }

    // ---- Export ---------------------------------------------------------------------------------------------------

    /// <summary> Adds the export buttons and changes the text at the bottom to the picture's file name. </summary>
    private void ShowExportBar()
    {
        _bar = new ExportBar(this, pages[0]);
        _exportLabel.y = 20f;
        _exportLabel.color = new Color(0.8f, 0.8f, 0.8f);
        _instructions.text = "";
        ShowFileName();
    }

    /// <summary> Shows the exported file's name and size, which changes with the scale. </summary>
    private void ShowFileName()
    {
        (int width, int height) = _options.OutputSize(_canvas.Width, _canvas.Height, _options.HasBorder(TypeOf(_room)));
        _exportLabel.text = Translator.Format("{0} x {1} pixels  -  {2}_preview.png", width, height, _room);
    }

    /// <summary> Handles the export buttons: scale, border, export, export region and open folder. </summary>
    public override void Singal(MenuObject sender, string message)
    {
        switch (message)
        {
            case ExportBar.ScaleSignal:
                _options = _options.NextScale();
                _bar.ScaleText = Translator.Format("SCALE: X{0}", _options.Scale);
                ShowFileName();
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.BorderSignal:
                _options = _options.NextBorder();
                _bar.BorderText = Translator.Translate(BorderLabel(_options.Border));
                ShowFileName();
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.ExportSignal:
                string saved = PreviewExporter.TrySave(_region, _room, TypeOf(_room), _options, () => _canvas);
                _exportLabel.text = saved != null
                    ? Translator.Format("Saved: {0}", saved)
                    : Translator.Translate("The picture could not be saved. The log says why");
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                break;
            case ExportBar.ExportRegionSignal when _exporting:
                // Already exporting; the second press is ignored, and stops here
                break;
            case ExportBar.ExportRegionSignal:
                _exporting = true;
                _exportNext = 0;
                _exportLabel.text = "";
                break;
            case ExportBar.OpenSignal:
                Shell.OpenFolder(WarpFiles.ExportFolder);
                break;
            default:
                base.Singal(sender, message);
                break;
        }
    }

    /// <summary>
    /// The text on the border button for a mode, which is also its translation key. Each is written out whole, and not
    /// put together from parts, so the key can be searched for.
    /// </summary>
    private static string BorderLabel(BorderMode mode) => mode switch
    {
        BorderMode.All => "BORDERS: ALL",
        BorderMode.Shelters => "BORDERS: SHELTER",
        BorderMode.NotRooms => "BORDERS: TYPES",
        _ => "BORDERS: NONE"
    };

    /// <summary> The type of a room of the region, which decides whether its picture gets a border. An ordinary room if unknown. </summary>
    private RoomType TypeOf(string room)
    {
        RoomInfo info = _rooms.FirstOrDefault(candidate => candidate.Name == room);
        return info?.Type ?? RoomType.Room;
    }

    /// <summary>Exports one more room of the region, so a whole region is saved over many frames and the game keeps drawing.</summary>
    private void ExportNextRoom()
    {
        if (_progress == null)
        {
            _progress = new FSprite("pixel") { x = 0f, y = 0f, scaleY = 25f, scaleX = 0f };
            _progress.SetAnchor(0f, 0.5f);
            pages[0].Container.AddChild(_progress);
        }
        if (_exportNext < _rooms.Length)
        {
            RoomInfo info = _rooms[_exportNext];
            PreviewExporter.TrySave(_region, info.Name, info.Type, _options, () => PreviewPainter.Paint(ReadTiles(info.Name)));
            _exportNext++;
        }
        _progress.scaleX = Mathf.Lerp(0f, UiLayout.Screen(Custom.rainWorld).x, Mathf.InverseLerp(0, _rooms.Length, _exportNext));
        if (_exportNext < _rooms.Length)
        {
            return;
        }

        _exporting = false;
        _progress.alpha = 0f;
        _exportLabel.text = Translator.Format("Finished exporting {0} rooms", _rooms.Length);
    }

    /// <summary>The row of export buttons along the bottom of the preview.</summary>
    private sealed class ExportBar
    {
        /// <summary> The signal of the SCALE button. </summary>
        public const string ScaleSignal = "SCALE";
        /// <summary> The signal of the BORDERS button. </summary>
        public const string BorderSignal = "BORDER";
        /// <summary> The signal of the EXPORT button. </summary>
        public const string ExportSignal = "EXPORT";
        /// <summary> The signal of the EXPORT REGION button. </summary>
        public const string ExportRegionSignal = "EXPORTREGION";
        /// <summary> The signal of the OPEN FOLDER button. </summary>
        public const string OpenSignal = "OPEN";

        private static readonly Color _tint = new(0.5f, 0.5f, 0.5f);

        private readonly WarpButton _border;
        private readonly WarpButton _scale;
        private readonly WarpButton[] _all;

        public ExportBar(Menu.Menu menu, Page page)
        {
            _border = Add(menu, page, Translator.Translate("BORDERS: NONE"), BorderSignal, new Vector2(683f - 300f, 30f), 130f);
            _scale = Add(menu, page, Translator.Format("SCALE: X{0}", 1), ScaleSignal, Next(_border), 90f);
            WarpButton export = Add(menu, page, Translator.Translate("EXPORT"), ExportSignal, Next(_scale), 80f);
            WarpButton region = Add(menu, page, Translator.Translate("EXPORT REGION"), ExportRegionSignal, Next(export), 120f);
            WarpButton open = Add(menu, page, Translator.Translate("OPEN FOLDER"), OpenSignal, Next(region), 100f);
            _all = [_border, _scale, export, region, open];
        }

        /// <summary> Whether the pointer is on any of the export buttons. </summary>
        public bool IsMouseOver => _all.Any(button => button.IsMouseOverMe);

        public string BorderText
        {
            set => _border.Text = value;
        }

        public string ScaleText
        {
            set => _scale.Text = value;
        }

        /// <summary> Takes the buttons off the screen. </summary>
        public void RemoveSprites()
        {
            foreach (WarpButton button in _all)
            {
                button.RemoveSprites();
            }
        }

        /// <summary> The place for the next button in the row: to the right of this one, with a gap. </summary>
        private static Vector2 Next(WarpButton button) => button.pos + new Vector2(button.size.x + 10f, 0f);

        /// <summary> Adds one export button to the page. The text is already translated. </summary>
        private static WarpButton Add(Menu.Menu menu, Page page, string text, string signal, Vector2 pos, float width)
        {
            var button = new WarpButton(menu, page, text, signal, pos, new Vector2(width, 30f), _tint);
            page.subObjects.Add(button);
            return button;
        }
    }
}
