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

    private readonly MenuLabel _filterLabel;
    private readonly List<WarpButton> _sortButtons;
    private readonly List<WarpButton> _viewButtons;
    private readonly List<MenuLabel> _keyEntries = [];
    private readonly List<MenuLabel> _subregionEntries = [];
    private MenuLabel _keyLabel;
    private MenuLabel _subregionLabel;
    private bool _keyIsRow;

    public Legend(Menu.Menu menu, MenuObject owner) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _filterLabel = new MenuLabel(menu, this, Translator.TranslateWithSeparator("SORT     #SEPARATOR#     VIEW"),
            new Vector2(72f, 0f), Vector2.zero, false);
        subObjects.Add(_filterLabel);
        _sortButtons = MakeButtons(WarpSignals.ForSort, 22f);
        _viewButtons = MakeButtons(WarpSignals.ForView, 77f);
    }

    /// <summary>Called after the player picks another sort or view, once the preferences have been saved.</summary>
    public event Action Changed;

    /// <summary>How many lines the color key has.</summary>
    public int KeyEntryCount => _keyEntries.Count;

    /// <summary>Whether the key lists its entries side by side, as it does for room sizes.</summary>
    public bool KeyIsRow => _keyIsRow;

    /// <summary>How many lines the separate subregion list has.</summary>
    public int SubregionEntryCount => _subregionEntries.Count;

    /// <summary>
    /// Adds a column of three buttons, one for each grouping. The prefix says whether they sort or view.
    /// </summary>
    private List<WarpButton> MakeButtons(Func<Grouping, string> signalFor, float x)
    {
        var buttons = Groupings.All
            .Select(grouping => new WarpButton(menu, this, Translator.Translate(grouping.Code()), signalFor(grouping),
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
        foreach ((string text, Hsl color) in KeyEntries(region, view, palette))
        {
            MenuLabel entry = AddLabel(text, 25f);
            entry.label.color = UiColors.ToColor(color);
            _keyEntries.Add(entry);
        }
        if (_keyIsRow)
        {
            // Sizes sit next to each other, 10 apart
            for (int i = 0; i < _keyEntries.Count; i++)
            {
                _keyEntries[i].SetX(25f + 10f * i);
            }
        }

        if (sort == Grouping.Subregion && view != Grouping.Subregion)
        {
            AddSubregionList(region);
        }
    }

    /// <summary> Adds the list of the region's subregions by number, which names the groups when sorting by subregion. </summary>
    private void AddSubregionList(RegionData region)
    {
        _subregionLabel = AddLabel(Translator.Translate("SUBREGION"), 22f);
        for (int i = 0; i < region.SubregionNames.Length; i++)
        {
            MenuLabel entry = AddLabel(i + " - " + region.SubregionNames[i], 25f);
            entry.label.color = UiColors.ToColor(ColorPalette.DefaultSubregion(i));
            _subregionEntries.Add(entry);
        }
    }

    /// <summary> The heading of the key for a view. </summary>
    private static string KeyTitle(Grouping view)
    {
        return view switch
        {
            Grouping.Type => Translator.Translate("ROOM TYPE"),
            Grouping.Size => Translator.Translate("ROOM SIZE"),
            _ => Translator.Translate("SUBREGION")
        };
    }

    /// <summary>
    /// The lines of the key for a view, each with its color: room types, camera counts or the region's subregions.
    /// </summary>
    private static IEnumerable<(string Text, Hsl Color)> KeyEntries(RegionData region, Grouping view, ColorPalette palette)
    {
        return view switch
        {
            Grouping.Type => RoomTypes.All.Select(info => (Translator.Translate(info.KeyLabel), palette.Type(info.Type))),
            Grouping.Size => Enumerable.Range(1, ColorPalette.SizeCount)
                .Select(i => (i < ColorPalette.SizeCount ? i.ToString() : i + "+", palette.Size(i))),
            _ => region.SubregionNames.Select((name, i) => (name, palette.Subregion(region.Region, i)))
        };
    }

    /// <summary> Adds a left-aligned label at the given x. Its height is set later, by <see cref="Place"/>. </summary>
    private MenuLabel AddLabel(string text, float x)
    {
        var label = new MenuLabel(menu, this, text, new Vector2(x, 0f), Vector2.zero, false)
        {
            label = {
                alignment = FLabelAlignment.Left
            }
        };
        subObjects.Add(label);
        return label;
    }

    /// <summary>Removes the key and the subregion list, as when the color editor takes over the screen.</summary>
    public void ClearKey()
    {
        this.RemoveAll([.. _keyEntries, .. _subregionEntries, _keyLabel, _subregionLabel]);
        _keyEntries.Clear();
        _subregionEntries.Clear();
        _keyLabel = null;
        _subregionLabel = null;
    }

    /// <summary>Puts every part at the height the panel layout gives it.</summary>
    public void Place(LeftPanelLayout layout)
    {
        _filterLabel.SetY(layout.FilterLabelY);
        for (int i = 0; i < _sortButtons.Count; i++)
        {
            _sortButtons[i].SetY(layout.ButtonRowYs[i]);
            _viewButtons[i].SetY(layout.ButtonRowYs[i]);
        }

        _keyLabel?.SetY(layout.KeyLabelY);
        for (int i = 0; i < _keyEntries.Count && i < layout.ColorKeyYs.Length; i++)
        {
            _keyEntries[i].SetY(layout.ColorKeyYs[i]);
        }
        if (_subregionLabel != null && layout.SubregionLabelY is { } subregionY)
        {
            _subregionLabel.SetY(subregionY);
        }
        for (int i = 0; i < _subregionEntries.Count && i < layout.SubregionYs.Length; i++)
        {
            _subregionEntries[i].SetY(layout.SubregionYs[i]);
        }
    }

    /// <summary> Colors the buttons for the chosen sort and view green, and the others grey. </summary>
    public override void Update()
    {
        base.Update();
        Preferences preferences = WarpState.Preferences;
        Highlight(_sortButtons, preferences.Sort);
        Highlight(_viewButtons, preferences.View);
    }

    /// <summary> Colors the button for the chosen grouping green, and the rest grey. </summary>
    private static void Highlight(List<WarpButton> buttons, Grouping chosen)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].Tint = i == (int)chosen ? UiColors.Selected : UiColors.Unselected;
        }
    }

    /// <summary>
    /// Handles the sort and view buttons ("S" or "V" followed by TYPE, SIZE or SUB). Sorting also sets the view;
    /// viewing leaves the sort alone.
    /// </summary>
    public override void Singal(MenuObject sender, string message)
    {
        if (WarpSignals.GroupingChoiceOf(message) is not { } choice)
        {
            base.Singal(sender, message);
            return;
        }

        // Handled here, so it goes no further than this
        WarpState.UpdatePreferences(p => choice.Sorting ? p.WithSort(choice.Grouping) : p.WithView(choice.Grouping));
        Changed?.Invoke();
    }
}
