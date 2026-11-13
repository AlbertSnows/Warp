using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary>
/// The room buttons on the right of the screen, with the headings over each group. Rooms that do not fit are on a
/// second page, reached with the LEFT and RIGHT buttons.
/// </summary>
internal sealed class RoomGrid(Menu.Menu menu, MenuObject owner, RainWorldGame game)
    : RectangularMenuObject(menu, owner, Vector2.zero, Vector2.zero)
{
    private const string ButtonSuffix = "warp";
    private const string PageLeft = "PAGELEFT";
    private const string PageRight = "PAGERIGHT";

    private readonly List<WarpButton> _buttons = [];
    private readonly List<string> _roomNames = [];
    private readonly List<MenuLabel> _headings = [];
    private RoomGridLayout _layout;
    private WarpButton _pageLeft;
    private WarpButton _pageRight;
    private string _region = "";
    private int _ticks;

    /// <summary>Whether the pointer is over a room button or one is selected.</summary>
    public bool IsHot => _buttons.Any(button => button.Selected || button.IsMouseOverMe);

    /// <summary>The name of the room whose button the pointer is over, or null.</summary>
    public string HoveredRoom
    {
        get
        {
            int index = _buttons.FindIndex(button => button.IsMouseOverMe);
            return index < 0 ? null : _roomNames[index];
        }
    }

    /// <summary>The room name a room button's signal stands for, or null if the signal is another button's.</summary>
    public static string RoomOfSignal(string signal) => signal.EndsWith(ButtonSuffix)
        ? signal.Substring(0, signal.Length - ButtonSuffix.Length)
        : null;

    /// <summary>Replaces the buttons and headings with those for this region, grouped and colored as asked.</summary>
    public void Show(RegionData region, Grouping sort, Grouping view, ColorPalette palette)
    {
        Clear();
        _region = region.Region;
        RoomInfo[] rooms = RoomSorting.Sort(region.Rooms, sort);
        float width = game.rainWorld.options.ScreenSize.x - 20f;
        float height = game.rainWorld.options.ScreenSize.y;
        _layout = RoomGridLayout.Compute(rooms, sort, width, height);

        foreach (PlacedButton placed in _layout.Buttons)
        {
            RoomInfo room = rooms[placed.RoomIndex];
            var button = new WarpButton(menu, this, room.ShortName, room.Name + ButtonSuffix, Vector2.zero,
                new Vector2(60f, 25f), UiColors.ToColor(palette.For(room, view, region.Region)));
            _buttons.Add(button);
            _roomNames.Add(room.Name);
            subObjects.Add(button);
        }
        foreach (PlacedLabel placed in _layout.Labels)
        {
            var heading = new MenuLabel(menu, this, HeadingText(sort, placed.GroupKey), Vector2.zero, Vector2.zero, false);
            _headings.Add(heading);
            subObjects.Add(heading);
        }
        if (_layout.HasSecondPage)
        {
            AddPageButtons(width);
        }
        ShowPage(0);
    }

    /// <summary>Removes everything the grid shows.</summary>
    public void Clear()
    {
        foreach (MenuObject item in _buttons.Cast<MenuObject>().Concat(_headings).Concat([_pageLeft, _pageRight]))
        {
            if (item == null)
            {
                continue;
            }

            item.RemoveSprites();
            RemoveSubObject(item);
        }
        _buttons.Clear();
        _roomNames.Clear();
        _headings.Clear();
        _pageLeft = null;
        _pageRight = null;
        _layout = null;
    }

    /// <summary> Adds the LEFT and RIGHT buttons, for a region with more columns than fit. </summary>
    private void AddPageButtons(float width)
    {
        _pageLeft = new WarpButton(menu, this, Translator.Translate("LEFT"), PageLeft, new Vector2(width - 135f, 55f),
            new Vector2(50f, 25f), UiColors.Disabled);
        _pageRight = new WarpButton(menu, this, Translator.Translate("RIGHT"), PageRight, new Vector2(width - 80f, 55f),
            new Vector2(50f, 25f), new Color(1f, 0.9f, 0f));
        subObjects.Add(_pageLeft);
        subObjects.Add(_pageRight);
    }

    /// <summary> The heading for a group of rooms: a subregion number, a camera count or a room type. </summary>
    private static string HeadingText(Grouping sort, int key)
    {
        return sort switch
        {
            Grouping.Subregion => Translator.Translate("SUB ") + key,
            Grouping.Size => Translator.Translate("CAMS ") + key,
            _ => Translator.Translate(TypeHeading(((RoomType)key).ToString().ToUpper()))
        };
    }

    /// <summary>
    /// The heading for a room type. The longest type names have shorter ones that fit over a column.
    /// </summary>
    private static string TypeHeading(string typeName)
    {
        return typeName switch
        {
            "SCAVTRADER" => "TRADER",
            "SWARMROOM" => "SWARM",
            "SCAVOUTPOST" => "OUTPOST",
            _ => typeName
        };
    }

    /// <summary> Handles LEFT and RIGHT, which turn the page. </summary>
    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        switch (message)
        {
            case PageLeft:
                ShowPage(0);
                break;
            case PageRight:
                ShowPage(1);
                break;
        }
    }

    /// <summary>Slides the columns of the chosen page into view and the other page out of it.</summary>
    private void ShowPage(int selectedPage)
    {
        if (_layout == null)
        {
            return;
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            PlacedButton placed = _layout.Buttons[i];
            Move(_buttons[i], placed.X + RoomGridLayout.ShiftFor(placed.Page, selectedPage), placed.Y);
        }
        for (int i = 0; i < _headings.Count; i++)
        {
            PlacedLabel placed = _layout.Labels[i];
            Move(_headings[i], placed.X + RoomGridLayout.ShiftFor(placed.Page, selectedPage), placed.Y);
        }

        if (_pageLeft == null)
        {
            return;
        }

        SetPageButton(_pageLeft, enabled: selectedPage == 1, UiColors.Active);
        SetPageButton(_pageRight, enabled: selectedPage == 0, UiColors.Active);
    }

    /// <summary> Moves a control to a place, without sliding. </summary>
    private static void Move(RectangularMenuObject item, float x, float y)
    {
        item.pos = new Vector2(x, y);
        item.lastPos = item.pos;
    }

    /// <summary> Enables a page button in its color, or greys it out. </summary>
    private static void SetPageButton(WarpButton button, bool enabled, Color enabledTint)
    {
        button.buttonBehav.greyedOut = !enabled;
        button.Tint = enabled ? enabledTint : UiColors.Disabled;
    }

    /// <summary>
    /// Makes the button of the room the players are in pulse, when the grid shows the players' own region.
    /// </summary>
    public override void Update()
    {
        base.Update();
        _ticks++;
        AbstractRoom current = game.cameras[0].room?.abstractRoom;
        if (current == null || current.world.name != _region)
        {
            return;
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Tint = _roomNames[i] == current.name
                ? new HSLColor(1f, 0f, 0.5f + Mathf.Sin(_ticks / 10f) * 0.4f).rgb
                : _buttons[i].DefaultTint;
        }
    }
}
