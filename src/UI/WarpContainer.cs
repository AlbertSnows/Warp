using System;
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
    private bool _errorSoundPending;
    private string _denShown;
    private bool _denLabelShown;
    private StatsPanel _stats;
    private ColorEditor _editor;

    public WarpContainer(PauseMenu menu, MenuObject owner, Vector2 pos) : base(menu, owner, pos, Vector2.zero)
    {
        try
        {
            _pause = menu;
            Vector2 screen = UiLayout.Screen(menu.game.rainWorld);
            Preferences preferences = WarpState.Preferences;
            WarpState.SelectRegion(menu.game.world.region.name);

            _background = new FSprite("LinearGradient200")
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

            _statsButton = new WarpButton(menu, this, Translator.Translate("STATS"), WarpSignals.Stats,
                new Vector2(75f, LeftPanelLayout.StatsButtonYFor(screen.y)), new Vector2(45f, 20f), StatsTint(preferences.ShowStats));
            subObjects.Add(_statsButton);

            _picker = new RegionPicker(menu, this, menu.game, WarpState.SelectedRegion, _statsButton.pos);
            subObjects.Add(_picker);
            _picker.RegionChosen += ChooseRegion;
            _picker.ListToggled += ApplyLayout;
            if (!preferences.DropdownMode)
            {
                _picker.SetDropdownMode(false, WarpState.SelectedRegion);
            }

            _listToggle = AddToggle("warpList", WarpSignals.ToggleRegionList, 22f);
            _colorToggle = AddToggle("warpColor", WarpSignals.Colors, 57f);
            _favouriteToggle = AddToggle("warpFav", WarpSignals.Favourite, 92f);

            _legend = new Legend(menu, this);
            subObjects.Add(_legend);
            _legend.Changed += Refresh;
            _grid = new RoomGrid(menu, this, menu.game);
            subObjects.Add(_grid);

            if (!string.IsNullOrEmpty(WarpState.Error))
            {
                var errorLabel = new MenuLabel(menu, this, WarpState.Error, new Vector2(682f, 72f), Vector2.zero, true)
                {
                    label = { color = UiColors.Error }
                };
                subObjects.Add(errorLabel);
                _errorSoundPending = true;
            }
            Refresh();
        }
        catch
        {
            // Take back what was added to the screen, so the pause menu is left as the game made it
            RemoveSprites();
            throw;
        }
    }

    /// <summary>Whether the room preview is open, which keeps the controls from moving under it.</summary>
    private bool PreviewVisible
    {
        get;
        set;
    }

    /// <summary> The STATS button's color for the panel being on or off. </summary>
    private static Color StatsTint(bool on) => on ? UiColors.Stats : Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);

    /// <summary> Adds a text label with the given alignment. </summary>
    private MenuLabel AddLabel(string text, Vector2 labelPos, FLabelAlignment alignment)
    {
        var label = new MenuLabel(menu, this, text, labelPos, Vector2.zero, false)
        {
            label =
            {
                alignment = alignment
            }
        };
        subObjects.Add(label);
        return label;
    }

    /// <summary> Adds one of the three small symbol buttons. Their height is set by <see cref="ApplyLayout"/>. </summary>
    private WarpSymbolButton AddToggle(string symbol, string signal, float x)
    {
        var toggle = new WarpSymbolButton(menu, this, symbol, signal, new Vector2(x, 0f), 30f);
        subObjects.Add(toggle);
        return toggle;
    }

    /// <summary> Shows the stats of the selected region, making the panel first if it is not there. </summary>
    private void ShowStats()
    {
        if (_stats == null)
        {
            _stats = new StatsPanel(menu, this);
            subObjects.Add(_stats);
        }
        _stats.Show(RegionStats.Of(RegionCatalog.Get(WarpState.SelectedRegion).Rooms));
    }

    // ---- Showing the selected region ----------------------------------------------------------------------------

    private void ChooseRegion(string region, bool reload)
    {
        WarpState.SelectRegion(region);
        if (reload)
        {
            RegionCatalog.Reload(region);
        }
        Refresh();
    }


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
        RegionData regionData = RegionCatalog.Get(WarpState.SelectedRegion);
        Preferences preferences = WarpState.Preferences;
        _grid.Show(regionData, preferences.Sort, preferences.View, WarpState.Palette);
        _legend.Rebuild(regionData, preferences.Sort, preferences.View, WarpState.Palette);
        if (preferences.ShowStats)
        {
            ShowStats();
        }
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (PreviewVisible)
        {
            return;
        }
        var layout = LeftPanelLayout.Compute(new LeftPanelInput(
            UiLayout.Screen(_pause.game.rainWorld).y, _picker.DropdownMode, _picker.DropdownOpen, _picker.ButtonCount,
            _legend.KeyEntryCount, _legend.KeyIsRow, _legend.SubregionEntryCount, _stats != null));
        _legend.Place(layout);
        foreach (WarpSymbolButton toggle in new[] { _listToggle, _colorToggle, _favouriteToggle })
        {
            toggle.SetY(layout.ToggleY);
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
        this.SetY(WarpState.Preferences.ShowMenu ? 0f : UiLayout.OffScreenY);

        _favouriteToggle.Tint = WarpState.Favourites.Contains(WarpState.SelectedRegion) ? UiColors.Favourite : UiColors.Plain;
        ShowDenPosition();
        ShowHint();
        OpenPreviewOnRightClick();
    }

    /// <summary> A help line, the text of which is also its translation key, and when it applies. </summary>
    private sealed record Hint(Func<WarpContainer, bool> IsShown, string Text);

    /// <summary> The help lines, in order. When more than one applies the last one is shown. </summary>
    private static readonly Hint[] _hints =
    [
        new(container => container._grid.IsHot, "Left click: Warp    -    Right click: Preview"),
        new(container => container._picker.IsHot, "Left click: Load Region    -    Shift click: Reload Region"),
        new(container => container._listToggle.IsHot(),
            "Toggle the region list between list view and button view #SEPARATOR# Shift click to toggle alphabetical sorting"),
        // The spelling "customiser" is the key in the translation files, so it has to stay as it is
        new(container => container._colorToggle.IsHot(),
            "Opens the colour customiser where you can change the colors for room types, sizes and subregions"),
        new(container => container._favouriteToggle.IsHot(),
            "Toggle whether this region is favourited, putting it to the top of the list")
    ];

    /// <summary> The help line at the bottom of the pause menu, for whatever the pointer is over. Only the one shown is translated. </summary>
    private void ShowHint()
    {
        Hint hint = _hints.LastOrDefault(candidate => candidate.IsShown(this));
        if (hint == null)
        {
            return;
        }

        _pause.infoLabel.text = Translator.TranslateWithSeparator(hint.Text);
        _pause.infoLabel.alpha = 1f;
    }


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

    /// <summary> Shows the den position, in words that depend on whether one is set. Translated only when it changes. </summary>
    private void ShowDenPosition()
    {
        string den = WarpState.DenPosition;
        if (_denLabelShown && den == _denShown)
        {
            return;
        }

        string denText = den == null
            ? "Den Position: {0} #SEPARATOR# Hold shift + click a room to assign"
            : "Den Position: {0} #SEPARATOR# Press C to clear";
        _denLabel.label.text = Translator.FormatWithSeparator(denText, Translator.Translate(den ?? "NONE"));
        _denShown = den;
        _denLabelShown = true;
    }

    public override void RemoveSprites()
    {
        base.RemoveSprites();
        _background?.RemoveFromContainer();
    }

    // ---- Signals --------------------------------------------------------------------------------------------------

    public override void Singal(MenuObject sender, string message)
    {
        switch (message)
        {
            case WarpSignals.Stats:
                ToggleStats();
                break;
            case WarpSignals.Favourite:
                ToggleFavourite();
                break;
            case WarpSignals.ToggleRegionList:
                ToggleRegionList();
                break;
            case WarpSignals.Colors:
                OpenColorEditor();
                break;
            default:
                // Not ours: a room button's message, which the pause menu hook has to see
                base.Singal(sender, message);
                break;
        }
    }

    private void ToggleStats()
    {
        bool show = !WarpState.Preferences.ShowStats;
        WarpState.UpdatePreferences(p => p with { ShowStats = show });
        _statsButton.Tint = StatsTint(show);
        RemoveStats();
        if (show)
        {
            ShowStats();
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
        this.RemoveAll([_stats]);
        _stats = null;
    }

    private void ToggleFavourite()
    {
        string region = WarpState.SelectedRegion;
        bool added = WarpState.ToggleFavourite(region);
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
        bool toDropdown = !_picker.DropdownMode;
        bool flipOrder = !toDropdown && UiInput.ShiftHeld;
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
        this.RemoveAll([_editor]);
        _editor = null;
        Refresh();
    }
}
