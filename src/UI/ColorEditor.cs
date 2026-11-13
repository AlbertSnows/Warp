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
/// The color customizer: pick room types, sizes or a region's subregions, then adjust the hue, saturation and
/// lightness of the chosen color. Changes go straight into the palette; SAVE writes the palette to disk.
/// </summary>
internal sealed class ColorEditor : RectangularMenuObject, Slider.ISliderOwner
{
    private const float SliderMax = 0.99f;

    private static readonly Color _chosen = new(0.8f, 0.8f, 0.8f);

    private readonly Vector2 _anchor;
    private readonly WarpButton[] _categoryButtons;
    private readonly MenuLabel _categoryLabel;
    private readonly MenuLabel _hoverLabel;
    private readonly string _prompt;
    private readonly (WarpButton Button, string Key)[] _hovers;
    private string _hoverKey;
    private readonly HorizontalSlider _hueSlider;
    private readonly HorizontalSlider _satSlider;
    private readonly HorizontalSlider _litSlider;
    private readonly List<WarpButton> _swatches = [];
    private Grouping _category;
    private string _region;
    private int _selected = -1;
    private string _selectedName = "";
    private Hsl _current = new(0.2f, 1f, 0.5f);

    public ColorEditor(Menu.Menu menu, MenuObject owner, Grouping start) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _anchor = new Vector2(160f, menu.manager.rainWorld.screenSize.y - 350f);
        _region = WarpState.SelectedRegion;

        WarpButton saveButton = AddButton("SAVE", WarpSignals.EditorSave, new Vector2(_anchor.x + 20f + 95f, _anchor.y - 20f), new Color(0.4f, 1f, 0.4f));
        WarpButton doneButton = AddButton("DONE", WarpSignals.EditorDone, new Vector2(saveButton.pos.x + 95f, _anchor.y - 20f), _chosen);

        _categoryButtons =
        [
            .. Groupings.All
                .Select((grouping, i) => AddButton(grouping.Code(), WarpSignals.ForCategory(grouping),
                    new Vector2(_anchor.x + 20f + 95f * i, _anchor.y + 200f), _chosen))
        ];

        float middle = _categoryButtons[1].pos.x + 40f;
        subObjects.Add(new MenuLabel(menu, this,
            Translator.Translate("COLOR CUSTOMISER") + Environment.NewLine
            + Translator.Translate("Adjust global room Type and Size button colors") + Environment.NewLine
            + Translator.Translate("and individual subregion colors per region"),
            new Vector2(middle, _anchor.y + 265f), Vector2.zero, false));
        _prompt = Translator.Translate("Select a button above to configure colors");
        _categoryLabel = AddLabel(_prompt, middle, _anchor.y + 147f);
        _hoverLabel = AddLabel("", middle, _anchor.y - 45f);

        _hueSlider = AddSlider("HUE", 100f);
        _satSlider = AddSlider("SAT", 60f);
        _litSlider = AddSlider("LIT", 20f);

        // What the pointer can be over, and the text for each, as translation keys
        _hovers =
        [
            (_categoryButtons[0], "Configure button colors when viewing by room type"),
            (_categoryButtons[1], "Configure button colors when viewing by room size"),
            (_categoryButtons[2], "Configure button colors when viewing by subregion"),
            (saveButton, "Save the configured colors to a file"),
            (doneButton, "Return to the main Warp menu")
        ];

        SetCategory(start);
    }

    /// <summary>Called when DONE is pressed.</summary>
    public event Action Closed;

    /// <summary> Adds one of the editor's 80 by 30 buttons. </summary>
    private WarpButton AddButton(string text, string signal, Vector2 buttonPos, Color tint)
    {
        var button = new WarpButton(menu, this, Translator.Translate(text), signal, buttonPos, new Vector2(80f, 30f), tint);
        subObjects.Add(button);
        return button;
    }

    /// <summary> Adds a text label at a place in the editor. </summary>
    private MenuLabel AddLabel(string text, float x, float y)
    {
        var label = new MenuLabel(menu, this, text, new Vector2(x, y), Vector2.zero, false);
        subObjects.Add(label);
        return label;
    }

    /// <summary> Adds a slider that is the given height above the editor's anchor. </summary>
    private HorizontalSlider AddSlider(string text, float offset)
    {
        var slider = new HorizontalSlider(menu, this, Translator.Translate(text),
            new Vector2(_anchor.x + 30f, _anchor.y + offset),
            new Vector2(185f, 0f), Slider.SliderID.LevelsListScroll, false);
        subObjects.Add(slider);
        return slider;
    }

    /// <summary> Switches to another category of colors, with nothing selected, and redraws the swatches. </summary>
    private void SetCategory(Grouping category)
    {
        _category = category;
        Select(-1);
        RebuildSwatches();
    }

    /// <summary>One small button for each color in the category, in that color.</summary>
    private void RebuildSwatches()
    {
        this.RemoveAll(_swatches);
        _swatches.Clear();

        if (_category == Grouping.Subregion)
        {
            EnsureSubregionColors();
        }
        int count = ColorsInCategory;
        for (int i = 0; i < count; i++)
        {
            var swatch = new WarpButton(menu, this, (i + 1).ToString(), WarpSignals.ForSwatch(i),
                new Vector2(_categoryButtons[0].pos.x + 3f + 30f * i, _categoryButtons[0].pos.y - 35f), new Vector2(25f, 25f),
                UiColors.ToColor(ColorAt(i)));
            _swatches.Add(swatch);
            subObjects.Add(swatch);
        }
    }

    /// <summary> How many swatches the current category has. </summary>
    private int ColorsInCategory => _category switch
    {
        Grouping.Type => ColorPalette.TypeCount,
        Grouping.Size => ColorPalette.SizeCount,
        _ => WarpState.Palette.Subregions[_region].Count
    };

    /// <summary>
    /// Makes sure the palette has a color for each of the region's subregions, so there is a swatch for each. Called
    /// when the category or the region changes, before the swatches are made.
    /// </summary>
    private void EnsureSubregionColors()
    {
        int count = RegionCatalog.Get(_region).SubregionNames.Length;
        WarpState.UpdatePalette(palette => palette.WithSubregionCount(_region, count));
    }

    /// <summary> The palette's color for a swatch of the current category. </summary>
    private Hsl ColorAt(int index)
    {
        ColorPalette palette = WarpState.Palette;
        return _category switch
        {
            Grouping.Type => palette.Type((RoomType)index),
            Grouping.Size => palette.Sizes[index],
            _ => palette.Subregion(_region, index)
        };
    }

    /// <summary> Puts a new color for a swatch of the current category into the palette. </summary>
    private void SetColor(int index, Hsl color)
    {
        WarpState.UpdatePalette(palette =>
        {
            return _category switch
            {
                Grouping.Type => palette.WithType((RoomType)index, color),
                Grouping.Size => palette.WithSize(index, color),
                _ => palette.WithSubregion(_region, index, color)
            };
        });
    }

    /// <summary> Redraws the swatches if the region was changed from the list while the editor is open. </summary>
    public override void Update()
    {
        base.Update();
        if (_region == WarpState.SelectedRegion || !RegionCatalog.IsLoaded(WarpState.SelectedRegion))
        {
            return;
        }
        // The region was changed from the list while the editor was open
        _region = WarpState.SelectedRegion;
        Select(-1);
        RebuildSwatches();
    }

    /// <summary>
    /// The game has a typo that we have to match, hence the typo here
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    public override void Singal(MenuObject sender, string message)
    {
        switch (message)
        {
            case WarpSignals.EditorDone:
                Closed?.Invoke();
                break;
            case WarpSignals.EditorSave:
                menu.PlaySound(SoundID.MENU_Player_Join_Game);
                WarpState.SavePalette();
                break;
            default:
            {
                if (WarpSignals.CategoryOf(message) is { } category)
                {
                    menu.PlaySound(SoundID.MENU_Add_Level);
                    SetCategory(category);
                }
                else if (WarpSignals.SwatchOf(message) is { } index)
                {
                    menu.PlaySound(SoundID.MENU_Add_Level);
                    Select(index);
                    _current = ColorAt(index);
                }
                else
                {
                    // Not ours, so it goes on up; the editor's own messages stop here
                    base.Singal(sender, message);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Keeps the buttons, swatches and labels in step with the palette and what is under the pointer.
    /// </summary>
    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        for (int i = 0; i < _categoryButtons.Length; i++)
        {
            _categoryButtons[i].Tint = i == (int)_category ? _chosen : UiColors.Unselected;
        }
        for (int i = 0; i < _swatches.Count; i++)
        {
            _swatches[i].Tint = UiColors.ToColor(ColorAt(i));
        }
        ShowHoverText();
        UpdateCategoryLabel();
    }

    /// <summary> Shows the help text for the button the pointer is over, or nothing. Translated only when it changes. </summary>
    private void ShowHoverText()
    {
        string key = _hovers.FirstOrDefault(hover => hover.Button.IsMouseOverMe).Key;
        if (key == _hoverKey)
        {
            return;
        }

        _hoverKey = key;
        _hoverLabel.label.text = key == null ? "" : Translator.Translate(key);
    }

    /// <summary>
    /// Selects a swatch, or none with -1, and works out the text that names it once, so it is not translated on every
    /// frame.
    /// </summary>
    private void Select(int index)
    {
        _selected = index;
        _selectedName = index < 0 ? _prompt : SelectedName();
    }

    /// <summary> Shows the name of the selected color in its own color, or the instruction to select one. </summary>
    private void UpdateCategoryLabel()
    {
        _categoryLabel.label.text = _selectedName;
        _categoryLabel.label.color = _selected < 0 ? Color.white : UiColors.ToColor(_current);
    }

    /// <summary> The name of the selected color: a room type, a camera count or a subregion. </summary>
    private string SelectedName()
    {
        switch (_category)
        {
            case Grouping.Size:
                return Translator.Translate("Cameras: ") + (_selected + 1);
            case Grouping.Subregion:
                string[] names = RegionCatalog.Get(_region).SubregionNames;
                return _selected < names.Length ? Translator.Translate(names[_selected]) : "";
            case Grouping.Type:
            default:
                return Translator.Translate(RoomTypes.Of((RoomType)Math.Min(_selected, RoomTypes.All.Count - 1)).EditorName);
        }
    }

    /// <summary>Sliders run from 0 to 1 while colors stop at 0.99, so the thumb sits at the color's share of 0.99.</summary>
    public float ValueOfSlider(Slider slider)
    {
        if (slider == _hueSlider)
        {
            return _current.Hue / SliderMax;
        }
        if (slider == _satSlider)
        {
            return _current.Saturation / SliderMax;
        }
        return slider == _litSlider ? _current.Lightness / SliderMax : 0f;
    }

    /// <summary>
    /// Called by a slider that moves. Changes the selected color's channel and puts the result in the palette.
    /// </summary>
    public void SliderSetValue(Slider slider, float value)
    {
        if (_selected < 0)
        {
            return;
        }
        float level = Mathf.Lerp(0f, SliderMax, value);
        if (slider == _hueSlider)
        {
            _current = _current with
            {
                Hue = level
            };
        }
        else if (slider == _satSlider)
        {
            _current = _current with
            {
                Saturation = level
            };
        }
        else if (slider == _litSlider)
        {
            _current = _current with
            {
                Lightness = level
            };
        }
        SetColor(_selected, _current);
    }
}
