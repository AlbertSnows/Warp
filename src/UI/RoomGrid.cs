using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;
using Warp.Game;
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
    private readonly List<ButtonRow> _buttonRows = [];
    private readonly List<HeadingRow> _headingRows = [];
    private WarpButton _pageLeft;
    private WarpButton _pageRight;
    private string _region = "";
    private int _ticks;

    /// <summary> A room button with where it goes and which room it is for. </summary>
    private sealed record ButtonRow(WarpButton Button, PlacedButton Placed, string Room);

    /// <summary> A heading over a group of rooms, with where it goes. </summary>
    private sealed record HeadingRow(MenuLabel Label, PlacedLabel Placed);

    /// <summary>Whether the pointer is over a room button or one is selected.</summary>
    public bool IsHot => _buttonRows.Any(row => row.Button.IsHot());

    /// <summary>The name of the room whose button the pointer is over, or null.</summary>
    public string HoveredRoom => _buttonRows.FirstOrDefault(row => row.Button.IsMouseOverMe)?.Room;

    /// <summary>Replaces the buttons and headings with those for this region, grouped and colored as asked.</summary>
    public void Show(RegionData region, Grouping sort, Grouping view, ColorPalette palette)
    {
        Clear();
        _region = region.Region;
        RoomInfo[] rooms = RoomSorting.Sort(region.Rooms, sort);
        float width = UiLayout.Screen(game.rainWorld).x - 20f;
        float height = UiLayout.Screen(game.rainWorld).y;
        var layout = RoomGridLayout.Compute(rooms, sort, width, height);

        foreach (PlacedButton placed in layout.Buttons)
        {
            RoomInfo room = rooms[placed.RoomIndex];
            var button = new WarpButton(menu, this, room.ShortName, WarpSignals.ForRoom(room.Name), Vector2.zero,
                new Vector2(60f, 25f), HslColors.ToColor(palette.For(room, view, region.Region)));
            _buttonRows.Add(new ButtonRow(button, placed, room.Name));
            subObjects.Add(button);
        }
        foreach (PlacedLabel placed in layout.Labels)
        {
            var heading = new MenuLabel(menu, this, HeadingText(sort, placed.GroupKey), Vector2.zero, Vector2.zero, false);
            _headingRows.Add(new HeadingRow(heading, placed));
            subObjects.Add(heading);
        }
        if (layout.HasSecondPage)
        {
            AddPageButtons(width);
        }
        ShowPage(0);
    }

    /// <summary>Removes everything the grid shows.</summary>
    public void Clear()
    {
        this.RemoveAll([
            .. _buttonRows.Select(row => row.Button), .. _headingRows.Select(row => row.Label), _pageLeft, _pageRight
        ]);
        _buttonRows.Clear();
        _headingRows.Clear();
        _pageLeft = null;
        _pageRight = null;
    }

    /// <summary> Adds the LEFT and RIGHT buttons, for a region with more columns than fit. </summary>
    private void AddPageButtons(float width)
    {
        _pageLeft = new WarpButton(menu, this, Translator.Translate("LEFT"), WarpSignals.PageLeft, new Vector2(width - 135f, 55f),
            new Vector2(50f, 25f), UiColors.Disabled);
        _pageRight = new WarpButton(menu, this, Translator.Translate("RIGHT"), WarpSignals.PageRight, new Vector2(width - 80f, 55f),
            new Vector2(50f, 25f), UiColors.Active);
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
            _ => Translator.Translate(RoomTypes.Of((RoomType)key).Heading)
        };
    }

    /// <summary> Handles LEFT and RIGHT, which turn the page. </summary>
    public override void Singal(MenuObject sender, string message)
    {
        switch (message)
        {
            case WarpSignals.PageLeft:
                ShowPage(0);
                break;
            case WarpSignals.PageRight:
                ShowPage(1);
                break;
            default:
                // Not ours: a room button's message, which the pause menu hook has to see
                base.Singal(sender, message);
                break;
        }
    }

    /// <summary>Slides the columns of the chosen page into view and the other page out of it.</summary>
    private void ShowPage(int selectedPage)
    {
        IEnumerable<(RectangularMenuObject Item, GridSpot Spot)> placed =
        [
            .. _buttonRows.Select(row => ((RectangularMenuObject)row.Button, row.Placed.Spot)),
            .. _headingRows.Select(row => ((RectangularMenuObject)row.Label, row.Placed.Spot))
        ];
        foreach ((RectangularMenuObject item, GridSpot spot) in placed)
        {
            item.MoveTo(new Vector2(spot.X + RoomGridLayout.ShiftFor(spot.Page, selectedPage), spot.Y));
        }

        if (_pageLeft == null)
        {
            return;
        }

        SetPageButton(_pageLeft, enabled: selectedPage == 1, UiColors.Active);
        SetPageButton(_pageRight, enabled: selectedPage == 0, UiColors.Active);
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
        foreach (ButtonRow row in _buttonRows)
        {
            row.Button.Tint = row.Room == current.name
                ? new HSLColor(1f, 0f, 0.5f + Mathf.Sin(_ticks / 10f) * 0.4f).rgb
                : row.Button.DefaultTint;
        }
    }
}
