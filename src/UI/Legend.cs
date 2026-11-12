using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The sort and view buttons, and under them the key that says what the button colors mean. Sorting decides how the
/// room buttons are grouped and viewing decides how they are colored. Choosing either changes the saved preferences and
/// calls <see cref="Changed"/>.
/// </summary>
internal sealed class Legend : RectangularMenuObject
{
    /// <summary>The label of each button and the end of its signal: "S" or "V" in front says sort or view.</summary>
    private static readonly string[] ButtonCodes = { "TYPE", "SIZE", "SUB" };

    private readonly MenuLabel _filterLabel;
    private readonly List<WarpButton> _sortButtons;
    private readonly List<WarpButton> _viewButtons;
    private readonly List<MenuLabel> _keyEntries = new();
    private readonly List<MenuLabel> _subregionEntries = new();
    private MenuLabel _keyLabel;
    private MenuLabel _subregionLabel;
    private bool _keyIsRow;

    public Legend(Menu.Menu menu, MenuObject owner) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _filterLabel = new MenuLabel(menu, this, Translator.TranslateWithSeparator("SORT     #SEPARATOR#     VIEW"),
            new Vector2(72f, 0f), Vector2.zero, false);
        subObjects.Add(_filterLabel);
        _sortButtons = MakeButtons("S", 22f);
        _viewButtons = MakeButtons("V", 77f);
    }

    /// <summary>Called after the player picks another sort or view, once the preferences have been saved.</summary>
    public event Action Changed;

    /// <summary>How many lines the color key has.</summary>
    public int KeyEntryCount => _keyEntries.Count;

    /// <summary>Whether the key lists its entries side by side, as it does for room sizes.</summary>
    public bool KeyIsRow => _keyIsRow;

    /// <summary>How many lines the separate subregion list has.</summary>
    public int SubregionEntryCount => _subregionEntries.Count;

    private List<WarpButton> MakeButtons(string prefix, float x)
    {
        var buttons = Enumerable.Range(0, ButtonCodes.Length)
            .Select(i => new WarpButton(menu, this, Translator.Translate(ButtonCodes[i]), prefix + ButtonCodes[i],
                new Vector2(x, 0f), new Vector2(45f, 23f), UiColors.ChoiceButton))
            .ToList();
        buttons.ForEach(subObjects.Add);
        return buttons;
    }

    /// <summary>Rebuilds the key for this region and view, and the subregion list when the rooms are grouped by subregion.</summary>
    public void Rebuild(RegionData region, Grouping sort, Grouping view, ColorPalette palette)
    {
        ClearKey();
        _keyIsRow = view == Grouping.Size;
        _keyLabel = AddLabel(KeyTitle(view), 22f);
        foreach (var (text, color) in KeyEntries(region, view, palette))
        {
            var entry = AddLabel(text, 25f);
            entry.label.color = UiColors.ToColor(color);
            _keyEntries.Add(entry);
        }
        if (_keyIsRow)
        {
            // Sizes sit next to each other, 10 apart
            for (var i = 0; i < _keyEntries.Count; i++)
            {
                _keyEntries[i].pos.x = 25f + 10f * i;
                _keyEntries[i].lastPos.x = _keyEntries[i].pos.x;
            }
        }
        if (sort == Grouping.Subregion && view != Grouping.Subregion)
        {
            _subregionLabel = AddLabel(Translator.Translate("SUBREGION"), 22f);
            for (var i = 0; i < region.SubregionNames.Length; i++)
            {
                var entry = AddLabel(i + " - " + region.SubregionNames[i], 25f);
                entry.label.color = UiColors.ToColor(ColorPalette.DefaultSubregion(i));
                _subregionEntries.Add(entry);
            }
        }
    }

    private static string KeyTitle(Grouping view)
    {
        switch (view)
        {
            case Grouping.Type:
                return Translator.Translate("ROOM TYPE");
            case Grouping.Size:
                return Translator.Translate("ROOM SIZE");
            default:
                return Translator.Translate("SUBREGION");
        }
    }

    private static IEnumerable<(string Text, Hsl Color)> KeyEntries(RegionData region, Grouping view, ColorPalette palette)
    {
        switch (view)
        {
            case Grouping.Type:
                return Enum.GetNames(typeof(RoomType))
                    .Select((name, i) => (Translator.Translate(name), palette.Type(i)));
            case Grouping.Size:
                return Enumerable.Range(1, ColorPalette.SizeCount)
                    .Select(i => (i < ColorPalette.SizeCount ? i.ToString() : i + "+", palette.Size(i)));
            default:
                return region.SubregionNames.Select((name, i) => (name, palette.Subregion(region.Region, i)));
        }
    }

    private MenuLabel AddLabel(string text, float x)
    {
        var label = new MenuLabel(menu, this, text, new Vector2(x, 0f), Vector2.zero, false);
        label.label.alignment = FLabelAlignment.Left;
        subObjects.Add(label);
        return label;
    }

    /// <summary>Removes the key and the subregion list, as when the color editor takes over the screen.</summary>
    public void ClearKey()
    {
        foreach (var item in _keyEntries.Concat(_subregionEntries).Concat(new[] { _keyLabel, _subregionLabel }))
        {
            if (item != null)
            {
                item.RemoveSprites();
                RemoveSubObject(item);
            }
        }
        _keyEntries.Clear();
        _subregionEntries.Clear();
        _keyLabel = null;
        _subregionLabel = null;
    }

    /// <summary>Puts every part at the height the panel layout gives it.</summary>
    public void Place(LeftPanelLayout layout)
    {
        SetY(_filterLabel, layout.FilterLabelY);
        for (var i = 0; i < _sortButtons.Count; i++)
        {
            SetY(_sortButtons[i], layout.ButtonRowYs[i]);
            SetY(_viewButtons[i], layout.ButtonRowYs[i]);
        }
        if (_keyLabel != null)
        {
            SetY(_keyLabel, layout.KeyLabelY);
        }
        for (var i = 0; i < _keyEntries.Count && i < layout.ColorKeyYs.Length; i++)
        {
            SetY(_keyEntries[i], layout.ColorKeyYs[i]);
        }
        if (_subregionLabel != null && layout.SubregionLabelY is { } subregionY)
        {
            SetY(_subregionLabel, subregionY);
        }
        for (var i = 0; i < _subregionEntries.Count && i < layout.SubregionYs.Length; i++)
        {
            SetY(_subregionEntries[i], layout.SubregionYs[i]);
        }
    }

    private static void SetY(RectangularMenuObject item, float y)
    {
        item.pos.y = y;
        item.lastPos.y = y;
    }

    public override void Update()
    {
        base.Update();
        var preferences = WarpState.Preferences;
        Highlight(_sortButtons, preferences.Sort);
        Highlight(_viewButtons, preferences.View);
    }

    private static void Highlight(List<WarpButton> buttons, Grouping chosen)
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].Tint = i == (int)chosen ? UiColors.Selected : UiColors.Unselected;
        }
    }

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        var index = Array.IndexOf(ButtonCodes, message.Length > 1 ? message.Substring(1) : "");
        if (index < 0 || (message[0] != 'S' && message[0] != 'V'))
        {
            return;
        }
        var grouping = (Grouping)index;
        // Sorting by something also colors by it; coloring by something leaves the sort alone
        WarpState.UpdatePreferences(p => message[0] == 'S' ? p with { Sort = grouping, View = grouping } : p with { View = grouping });
        Changed?.Invoke();
    }
}
