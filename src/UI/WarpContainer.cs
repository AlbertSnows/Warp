using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The Warp menu on the pause screen: the region picker, the sort and view buttons with their color key, the grid of
/// room buttons and the optional stats. It ties the parts together. The parts draw themselves and report what the
/// player did; this class decides what that changes.
/// </summary>
internal sealed class WarpContainer : RectangularMenuObject
{
    private const float HiddenY = -2000f;

    private readonly RainWorldGame _game;
    private readonly PauseMenu _pause;
    private readonly FSprite _background;
    private readonly MenuLabel _denLabel;
    private readonly WarpButton _statsButton;
    private readonly WarpSymbolButton _listToggle;
    private readonly WarpSymbolButton _colorToggle;
    private readonly WarpSymbolButton _favouriteToggle;
    private readonly RegionPicker _picker;
    private readonly Legend _legend;
    private readonly RoomGrid _grid;
    private readonly MenuLabel _errorLabel;
    private bool _errorSoundPending;
    private StatsPanel _stats;
    private ColorEditor _editor;

    public WarpContainer(PauseMenu menu, MenuObject owner, Vector2 pos) : base(menu, owner, pos, Vector2.zero)
    {
        _game = menu.game;
        _pause = menu;
        var screen = _game.rainWorld.options.ScreenSize;
        var preferences = WarpState.Preferences;
        WarpState.SelectRegion(_game.world.region.name);

        _background = new FSprite("LinearGradient200", true)
        {
            color = new Color(0.01f, 0.01f, 0.01f),
            rotation = 90f,
            scaleY = 2f,
            scaleX = 1250f,
            alpha = 0.5f
        };
        _background.SetAnchor(1f, 0f);
        Container.AddChild(_background);

        AddLabel(Translator.Translate("Warp Menu"), new Vector2(70f, LeftPanelLayout.TitleY(screen.y)), FLabelAlignment.Center)
            .label.color = UiColors.Title;
        _denLabel = AddLabel(Translator.Translate("Den Position: NONE"), new Vector2(screen.x - 42f, screen.y - 20f), FLabelAlignment.Right);

        _statsButton = new WarpButton(menu, this, Translator.Translate("STATS"), "STATS",
            new Vector2(75f, LeftPanelLayout.StatsButtonYFor(screen.y)), new Vector2(45f, 20f), StatsTint(preferences.ShowStats));
        subObjects.Add(_statsButton);

        _picker = new RegionPicker(menu, this, _game, WarpState.SelectedRegion, _statsButton.pos);
        subObjects.Add(_picker);
        _picker.RegionChosen += ChooseRegion;
        _picker.ListToggled += ApplyLayout;
        if (!preferences.DropdownMode)
        {
            _picker.SetDropdownMode(false, WarpState.SelectedRegion);
        }

        _listToggle = AddToggle("warpList", "TOGGLE", 22f);
        _colorToggle = AddToggle("warpColor", "COLORS", 57f);
        _favouriteToggle = AddToggle("warpFav", "FAV", 92f);

        _legend = new Legend(menu, this);
        subObjects.Add(_legend);
        _legend.Changed += OnSortOrViewChanged;
        _grid = new RoomGrid(menu, this, _game);
        subObjects.Add(_grid);

        if (preferences.ShowStats)
        {
            _stats = AddStats();
        }
        if (WarpState.Error != "")
        {
            _errorLabel = new MenuLabel(menu, this, WarpState.Error, new Vector2(682f, 72f), Vector2.zero, true);
            _errorLabel.label.color = UiColors.Error;
            subObjects.Add(_errorLabel);
            _errorSoundPending = true;
        }
        Refresh();
    }

    /// <summary>Whether the room preview is open, which keeps the controls from moving under it.</summary>
    public bool PreviewVisible
    {
        get; private set;
    }

    private static Color StatsTint(bool on) => on ? UiColors.Stats : Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);

    private MenuLabel AddLabel(string text, Vector2 pos, FLabelAlignment alignment)
    {
        var label = new MenuLabel(menu, this, text, pos, Vector2.zero, false);
        label.label.alignment = alignment;
        subObjects.Add(label);
        return label;
    }

    private WarpSymbolButton AddToggle(string symbol, string signal, float x)
    {
        var toggle = new WarpSymbolButton(menu, this, symbol, signal, new Vector2(x, 0f), 30f);
        subObjects.Add(toggle);
        return toggle;
    }

    private StatsPanel AddStats()
    {
        var stats = new StatsPanel(menu, this);
        subObjects.Add(stats);
        return stats;
    }

    // ---- Showing the selected region ----------------------------------------------------------------------------

    private void ChooseRegion(string region, bool reload)
    {
        WarpState.SelectRegion(region);
        if (reload || !RegionCatalog.IsLoaded(region))
        {
            RegionCatalog.Reload(region);
        }
        Refresh();
    }

    private void OnSortOrViewChanged() => Refresh();

    /// <summary>
    /// Shows the selected region in the grid, the key and the stats. Does nothing to the grid while the color editor
    /// is open, which has the screen to itself.
    /// </summary>
    private void Refresh()
    {
        if (_editor != null)
        {
            return;
        }
        var data = RegionCatalog.Get(WarpState.SelectedRegion);
        var preferences = WarpState.Preferences;
        _grid.Show(data, preferences.Sort, preferences.View, WarpState.Palette);
        _legend.Rebuild(data, preferences.Sort, preferences.View, WarpState.Palette);
        _stats?.Show(RegionStats.Of(data.Rooms));
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (PreviewVisible)
        {
            return;
        }
        var layout = LeftPanelLayout.Compute(new LeftPanelInput(
            _game.rainWorld.options.ScreenSize.y, _picker.DropdownMode, _picker.DropdownOpen, _picker.ButtonCount,
            _legend.KeyEntryCount, _legend.KeyIsRow, _legend.SubregionEntryCount, _stats != null));
        _legend.Place(layout);
        foreach (var toggle in new[] { _listToggle, _colorToggle, _favouriteToggle })
        {
            toggle.pos.y = layout.ToggleY;
            toggle.lastPos.y = layout.ToggleY;
        }
        if (_stats != null && layout.Stats is { } placement)
        {
            _stats.Place(placement);
        }
    }

    // ---- Per frame ------------------------------------------------------------------------------------------------

    public override void Update()
    {
        base.Update();
        if (_errorSoundPending)
        {
            menu.PlaySound(SoundID.HUD_Game_Over_Prompt);
            WarpState.ClearError();
            _errorSoundPending = false;
        }
        var y = WarpState.Preferences.ShowMenu ? 0f : HiddenY;
        pos.y = y;
        lastPos.y = y;

        _favouriteToggle.Tint = WarpState.Favourites.Contains(WarpState.SelectedRegion) ? UiColors.Favourite : UiColors.Plain;
        ShowHint();
        OpenPreviewOnRightClick();
    }

    /// <summary>The help line at the bottom of the pause menu, for whatever the pointer is over.</summary>
    private void ShowHint()
    {
        var hints = new (bool Hot, string Text)[]
        {
            (_grid.IsHot, Translator.Translate("Left click: Warp    -    Right click: Preview")),
            (_picker.IsHot, Translator.Translate("Left click: Load Region    -    Shift click: Reload Region")),
            (IsHot(_listToggle), Translator.TranslateWithSeparator(
                "Toggle the region list between list view and button view #SEPARATOR# Shift click to toggle alphabetical sorting")),
            (IsHot(_colorToggle), Translator.Translate(
                "Opens the colour customiser where you can change the colors for room types, sizes and subregions")),
            (IsHot(_favouriteToggle), Translator.Translate(
                "Toggle whether this region is favourited, putting it to the top of the list"))
        };
        // Later entries win when more than one is hot
        var hint = hints.LastOrDefault(entry => entry.Hot);
        if (hint.Hot)
        {
            _pause.infoLabel.text = hint.Text;
            _pause.infoLabel.alpha = 1f;
        }
    }

    private static bool IsHot(ButtonTemplate button) => button.Selected || button.IsMouseOverMe;

    private void OpenPreviewOnRightClick()
    {
        if (PreviewVisible || !Input.GetMouseButton(1) || _grid.HoveredRoom is not { } room)
        {
            return;
        }
        PreviewVisible = true;
        _pause.infoLabel.alpha = 0f;
        menu.PlaySound(SoundID.MENU_MultipleChoice_Clicked);
        ApplyLayout();
        RoomPreviewDialog.Open(menu.manager, this, room, WarpState.SelectedRegion, _pause);
    }

    /// <summary>Called by the room preview when it closes.</summary>
    public void PreviewClosed()
    {
        PreviewVisible = false;
        ApplyLayout();
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        var den = WarpState.DenPosition;
        _denLabel.label.text = Translator.TranslateWithSeparator(den == null
                ? "Den Position: {0} #SEPARATOR# Hold shift + click a room to assign"
                : "Den Position: {0} #SEPARATOR# Press C to clear")
            .Replace("{0}", Translator.Translate(den ?? "NONE"));
    }

    public override void RemoveSprites()
    {
        base.RemoveSprites();
        _background.RemoveFromContainer();
    }

    // ---- Signals --------------------------------------------------------------------------------------------------

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        switch (message)
        {
            case "STATS":
                ToggleStats();
                break;
            case "FAV":
                ToggleFavourite();
                break;
            case "TOGGLE":
                ToggleRegionList();
                break;
            case "COLORS":
                OpenColorEditor();
                break;
        }
    }

    private void ToggleStats()
    {
        var show = !WarpState.Preferences.ShowStats;
        WarpState.UpdatePreferences(p => p with { ShowStats = show });
        _statsButton.Tint = StatsTint(show);
        RemoveStats();
        if (show)
        {
            _stats = AddStats();
            _stats.Show(RegionStats.Of(RegionCatalog.Get(WarpState.SelectedRegion).Rooms));
        }
        ApplyLayout();
        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    }

    private void RemoveStats()
    {
        if (_stats == null)
        {
            return;
        }
        RemoveSubObject(_stats);
        _stats.RemoveSprites();
        _stats = null;
    }

    private void ToggleFavourite()
    {
        var region = WarpState.SelectedRegion;
        var added = WarpState.ToggleFavourite(region);
        WarpLog.Info((added ? "Fav region: " : "Unfav region: ") + region);
        menu.PlaySound(added ? SoundID.MENU_Player_Join_Game : SoundID.MENU_Button_Standard_Button_Pressed);
        _picker.Refresh(region);
        ApplyLayout();
    }

    /// <summary>
    /// Switches between the dropdown and the grid of region buttons. Shift while switching to the buttons also turns
    /// alphabetical order on or off.
    /// </summary>
    private void ToggleRegionList()
    {
        var toDropdown = !_picker.DropdownMode;
        var flipOrder = !toDropdown && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        WarpState.UpdatePreferences(p => p with
        {
            DropdownMode = toDropdown,
            Alphabetical = flipOrder ? !p.Alphabetical : p.Alphabetical
        });
        _picker.SetDropdownMode(toDropdown, WarpState.SelectedRegion);
        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        ApplyLayout();
    }

    private void OpenColorEditor()
    {
        if (_editor != null)
        {
            menu.PlaySound(SoundID.MENU_Error_Ping);
            return;
        }
        _grid.Clear();
        _legend.ClearKey();
        RemoveStats();
        _editor = new ColorEditor(menu, this, WarpState.Preferences.View);
        _editor.Closed += CloseColorEditor;
        subObjects.Add(_editor);
        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    }

    private void CloseColorEditor()
    {
        _editor.RemoveSprites();
        RemoveSubObject(_editor);
        _editor = null;
        if (WarpState.Preferences.ShowStats)
        {
            _stats = AddStats();
        }
        Refresh();
    }
}
