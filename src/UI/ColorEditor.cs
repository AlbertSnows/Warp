using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using RWCustom;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The color customiser: pick room types, sizes or a region's subregions, then adjust the hue, saturation and
/// lightness of the chosen color. Changes go straight into the palette; SAVE writes the palette to disk.
/// </summary>
internal sealed class ColorEditor : RectangularMenuObject, Slider.ISliderOwner
{
    private const string Done = "DONE";
    private const string Save = "SAVE";
    private const string CategoryPrefix = "C";
    private const string ColorPrefix = "X";
    private const float SliderMax = 0.99f;

    private static readonly Color Chosen = new(0.8f, 0.8f, 0.8f);

    private readonly Vector2 _anchor;
    private readonly WarpButton[] _categoryButtons;
    private readonly WarpButton _saveButton;
    private readonly WarpButton _doneButton;
    private readonly MenuLabel _categoryLabel;
    private readonly MenuLabel _hoverLabel;
    private readonly HorizontalSlider _hueSlider;
    private readonly HorizontalSlider _satSlider;
    private readonly HorizontalSlider _litSlider;
    private readonly List<WarpButton> _swatches = new();
    private Grouping _category;
    private string _region;
    private int _selected = -1;
    private Hsl _current = new(0.2f, 1f, 0.5f);

    public ColorEditor(Menu.Menu menu, MenuObject owner, Grouping start) : base(menu, owner, Vector2.zero, Vector2.zero)
    {
        _anchor = new Vector2(160f, menu.manager.rainWorld.screenSize.y - 350f);
        _region = WarpState.SelectedRegion;

        _saveButton = AddButton("SAVE", Save, new Vector2(_anchor.x + 20f + 95f, _anchor.y - 20f), new Color(0.4f, 1f, 0.4f));
        _doneButton = AddButton("DONE", Done, new Vector2(_saveButton.pos.x + 95f, _anchor.y - 20f), Chosen);

        _categoryButtons = new[] { "TYPE", "SIZE", "SUB" }
            .Select((name, i) => AddButton(name, CategoryPrefix + name,
                new Vector2(_anchor.x + 20f + 95f * i, _anchor.y + 200f), Chosen))
            .ToArray();

        var middle = _categoryButtons[1].pos.x + 40f;
        subObjects.Add(new MenuLabel(menu, this,
            Translator.Translate("COLOR CUSTOMISER") + Environment.NewLine
            + Translator.Translate("Adjust global room Type and Size button colors") + Environment.NewLine
            + Translator.Translate("and individual subregion colors per region"),
            new Vector2(middle, _anchor.y + 265f), Vector2.zero, false));
        _categoryLabel = AddLabel(Translator.Translate("Select a button above to configure colors"), middle, _anchor.y + 147f);
        _hoverLabel = AddLabel("", middle, _anchor.y - 45f);

        _hueSlider = AddSlider("HUE", 100f);
        _satSlider = AddSlider("SAT", 60f);
        _litSlider = AddSlider("LIT", 20f);

        SetCategory(start);
    }

    /// <summary>Called when DONE is pressed.</summary>
    public event Action Closed;

    private WarpButton AddButton(string text, string signal, Vector2 pos, Color tint)
    {
        var button = new WarpButton(menu, this, Translator.Translate(text), signal, pos, new Vector2(80f, 30f), tint);
        subObjects.Add(button);
        return button;
    }

    private MenuLabel AddLabel(string text, float x, float y)
    {
        var label = new MenuLabel(menu, this, text, new Vector2(x, y), Vector2.zero, false);
        subObjects.Add(label);
        return label;
    }

    private HorizontalSlider AddSlider(string text, float offset)
    {
        var slider = new HorizontalSlider(menu, this, Translator.Translate(text),
            new Vector2(_anchor.x + 30f, _anchor.y + offset), new Vector2(185f, 0f), Slider.SliderID.LevelsListScroll, false);
        subObjects.Add(slider);
        return slider;
    }

    private void SetCategory(Grouping category)
    {
        _category = category;
        _selected = -1;
        RebuildSwatches();
    }

    /// <summary>One small button for each color in the category, in that color.</summary>
    private void RebuildSwatches()
    {
        foreach (var swatch in _swatches)
        {
            swatch.RemoveSprites();
            RemoveSubObject(swatch);
        }
        _swatches.Clear();

        var count = ColorsInCategory;
        for (var i = 0; i < count; i++)
        {
            var swatch = new WarpButton(menu, this, (i + 1).ToString(), ColorPrefix + i,
                new Vector2(_categoryButtons[0].pos.x + 3f + 30f * i, _categoryButtons[0].pos.y - 35f), new Vector2(25f, 25f),
                UiColors.ToColor(ColorAt(i)));
            _swatches.Add(swatch);
            subObjects.Add(swatch);
        }
    }

    private int ColorsInCategory
    {
        get
        {
            switch (_category)
            {
                case Grouping.Type:
                    return ColorPalette.TypeCount;
                case Grouping.Size:
                    return ColorPalette.SizeCount;
                default:
                    WarpState.UpdatePalette(p => p.WithSubregionCount(_region, RegionCatalog.Get(_region).SubregionNames.Length));
                    return WarpState.Palette.Subregions[_region].Length;
            }
        }
    }

    private Hsl ColorAt(int index)
    {
        var palette = WarpState.Palette;
        switch (_category)
        {
            case Grouping.Type:
                return palette.Type(index);
            case Grouping.Size:
                return palette.Sizes[index];
            default:
                return palette.Subregion(_region, index);
        }
    }

    private void SetColor(int index, Hsl color)
    {
        WarpState.UpdatePalette(palette =>
        {
            switch (_category)
            {
                case Grouping.Type:
                    return palette.WithType(index, color);
                case Grouping.Size:
                    return palette.WithSize(index, color);
                default:
                    return palette.WithSubregion(_region, index, color);
            }
        });
    }

    public override void Update()
    {
        base.Update();
        // The region was changed from the list while the editor was open
        if (_region != WarpState.SelectedRegion && RegionCatalog.IsLoaded(WarpState.SelectedRegion))
        {
            _region = WarpState.SelectedRegion;
            _selected = -1;
            RebuildSwatches();
        }
    }

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        switch (message)
        {
            case Done:
                Closed?.Invoke();
                return;
            case Save:
                menu.PlaySound(SoundID.MENU_Player_Join_Game);
                WarpState.SavePalette();
                return;
        }
        if (message.StartsWith(CategoryPrefix))
        {
            var category = Array.IndexOf(new[] { "TYPE", "SIZE", "SUB" }, message.Substring(CategoryPrefix.Length));
            if (category >= 0)
            {
                menu.PlaySound(SoundID.MENU_Add_Level);
                SetCategory((Grouping)category);
            }
        }
        else if (message.StartsWith(ColorPrefix) && int.TryParse(message.Substring(ColorPrefix.Length), out var index))
        {
            menu.PlaySound(SoundID.MENU_Add_Level);
            _selected = index;
            _current = ColorAt(index);
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        for (var i = 0; i < _categoryButtons.Length; i++)
        {
            _categoryButtons[i].Tint = i == (int)_category ? Chosen : UiColors.Unselected;
        }
        for (var i = 0; i < _swatches.Count; i++)
        {
            _swatches[i].Tint = UiColors.ToColor(ColorAt(i));
        }
        _hoverLabel.label.text = HoverText();
        UpdateCategoryLabel();
    }

    private string HoverText()
    {
        if (_categoryButtons[0].IsMouseOverMe)
        {
            return Translator.Translate("Configure button colors when viewing by room type");
        }
        if (_categoryButtons[1].IsMouseOverMe)
        {
            return Translator.Translate("Configure button colors when viewing by room size");
        }
        if (_categoryButtons[2].IsMouseOverMe)
        {
            return Translator.Translate("Configure button colors when viewing by subregion");
        }
        if (_saveButton.IsMouseOverMe)
        {
            return Translator.Translate("Save the configured colors to a file");
        }
        return _doneButton.IsMouseOverMe ? Translator.Translate("Return to the main Warp menu") : "";
    }

    private void UpdateCategoryLabel()
    {
        if (_selected < 0)
        {
            _categoryLabel.label.text = Translator.Translate("Select a button above to configure colors");
            _categoryLabel.label.color = Color.white;
            return;
        }
        _categoryLabel.label.text = SelectedName();
        _categoryLabel.label.color = UiColors.ToColor(_current);
    }

    private string SelectedName()
    {
        switch (_category)
        {
            case Grouping.Size:
                return Translator.Translate("Cameras: ") + (_selected + 1);
            case Grouping.Subregion:
                var names = RegionCatalog.Get(_region).SubregionNames;
                return _selected < names.Length ? Translator.Translate(names[_selected]) : "";
            default:
                return Translator.Translate(TypeNames[Math.Min(_selected, TypeNames.Length - 1)]);
        }
    }

    private static readonly string[] TypeNames =
    {
        "Room", "Gate", "Shelter", "Swarmroom", "Scavenger Trader", "Scavenger Outpost"
    };

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

    public void SliderSetValue(Slider slider, float value)
    {
        if (_selected < 0)
        {
            return;
        }
        var level = Mathf.Lerp(0f, SliderMax, value);
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
