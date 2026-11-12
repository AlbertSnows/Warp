using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// Where the player picks a region: a dropdown list, or a grid of buttons that shows every region at once. Picking one
/// calls <see cref="RegionChosen"/>. Favourite regions come first in both.
/// </summary>
internal sealed class RegionPicker : RectangularMenuObject
{
    private const string ButtonPrefix = "reg-";

    private readonly RainWorldGame _game;
    private readonly Vector2 _buttonOrigin;
    private readonly MenuTabWrapper _tabs;
    private readonly OpComboBox _dropdown;
    private readonly List<WarpButton> _buttons = new();
    private bool _updatingList;

    public RegionPicker(Menu.Menu menu, MenuObject owner, RainWorldGame game, string selectedRegion, Vector2 buttonOrigin)
        : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _game = game;
        _buttonOrigin = buttonOrigin;
        _tabs = new MenuTabWrapper(menu, this);
        subObjects.Add(_tabs);

        var height = game.rainWorld.options.ScreenSize.y;
        _dropdown = new OpComboBox(new Configurable<string>(selectedRegion),
            new Vector2(0f, LeftPanelLayout.DropdownYFor(height)), 150f, DropdownItems())
        {
            listHeight = 29
        };
        _dropdown.OnListOpen += _ => SetOpen(true);
        _dropdown.OnListClose += _ => SetOpen(false);
        _dropdown.OnValueChanged += (_, value, _) => Choose(value, reload: ShiftHeld);
        new UIelementWrapper(_tabs, _dropdown);
        DropdownMode = true;
    }

    /// <summary>Called when the player picks a region. The second argument asks for its files to be read again.</summary>
    public event Action<string, bool> RegionChosen;

    /// <summary>Called when the dropdown list opens or closes, which moves the controls under it.</summary>
    public event Action ListToggled;

    public bool DropdownMode
    {
        get; private set;
    }

    /// <summary>Whether the dropdown's list is open and covering what is under it.</summary>
    public bool DropdownOpen
    {
        get; private set;
    }

    public int ButtonCount => _buttons.Count;

    public bool IsHot => _buttons.Any(button => button.Selected || button.IsMouseOverMe);

    private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private void SetOpen(bool open)
    {
        DropdownOpen = open;
        ListToggled?.Invoke();
    }

    private void Choose(string region, bool reload)
    {
        if (!_updatingList)
        {
            RegionChosen?.Invoke(region, reload);
        }
    }

    /// <summary>Shows the dropdown, or the grid of buttons.</summary>
    public void SetDropdownMode(bool dropdown, string selectedRegion)
    {
        DropdownMode = dropdown;
        ClearButtons();
        if (dropdown)
        {
            ReplaceDropdownItems(selectedRegion);
            _dropdown.Show();
        }
        else
        {
            _dropdown.Hide();
            DropdownOpen = false;
            BuildButtons();
        }
    }

    /// <summary>Redraws the list after the favourites or their order changed.</summary>
    public void Refresh(string selectedRegion)
    {
        if (DropdownMode)
        {
            ReplaceDropdownItems(selectedRegion);
        }
        else
        {
            ClearButtons();
            BuildButtons();
        }
    }

    private List<string> RegionIds => _game.overWorld.regions.Select(region => region.name).ToList();

    private List<ListItem> DropdownItems()
    {
        var preferences = WarpState.Preferences;
        return RegionList.DropdownEntries(RegionIds, FullName, WarpState.Favourites, preferences.Alphabetical)
            .Select((entry, i) => new ListItem(entry.Id, entry.Display, i))
            .ToList();
    }

    private string FullName(string region)
    {
        var name = Translator.Translate(Region.GetRegionFullName(region, _game.StoryCharacter));
        return name == Translator.Translate("Unknown Region") ? null : name;
    }

    /// <summary>
    /// Swaps the dropdown's entries for the current ones. The list cannot be emptied, so a placeholder holds it open
    /// while the old entries go and the new ones come in.
    /// </summary>
    private void ReplaceDropdownItems(string selectedRegion)
    {
        _updatingList = true;
        try
        {
            const string placeholder = "TOPI PLEASE";
            var oldNames = _dropdown.GetItemList().Select(item => item.name).ToArray();
            _dropdown.AddItems(false, new[] { new ListItem(placeholder, 0) });
            _dropdown.RemoveItems(false, oldNames);
            _dropdown.AddItems(false, DropdownItems().ToArray());
            _dropdown.RemoveItems(false, new[] { placeholder });
            _dropdown.ForceValue(selectedRegion);
        }
        finally
        {
            _updatingList = false;
        }
    }

    private void BuildButtons()
    {
        var order = RegionList.ButtonOrder(RegionIds, WarpState.Favourites, WarpState.Preferences.Alphabetical);
        for (var i = 0; i < order.Count; i++)
        {
            var (dx, dy) = LeftPanelLayout.RegionButtonOffset(i, order.Count);
            var button = new WarpButton(menu, this, order[i], ButtonPrefix + order[i],
                _buttonOrigin + new Vector2(dx, dy), new Vector2(30f, 23f), Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey));
            _buttons.Add(button);
            subObjects.Add(button);
        }
    }

    private void ClearButtons()
    {
        foreach (var button in _buttons)
        {
            button.RemoveSprites();
            RemoveSubObject(button);
        }
        _buttons.Clear();
    }

    public override void Update()
    {
        base.Update();
        foreach (var button in _buttons)
        {
            button.Tint = UiColors.RegionButton(RegionCatalog.IsLoaded(button.Text), WarpState.Favourites.Contains(button.Text));
        }
    }

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        if (message.StartsWith(ButtonPrefix))
        {
            menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            Choose(message.Substring(ButtonPrefix.Length), reload: ShiftHeld);
        }
    }
}
