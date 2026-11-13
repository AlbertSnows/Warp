using Menu;
using UnityEngine;
using Warp.Game;
using Warp.UI;
using Warp.Util;

namespace Warp.Hooks;

/// <summary>
/// What Warp adds to the game's pause menu: the Warp menu and the HIDE / WARP button, and the handling of the keys and
/// signals that go with them. The hooks in <see cref="WarpHooks"/> call these at the right moments.
/// </summary>
internal static class PauseMenuGlue
{
    private const string ToggleSignal = "toggleWarp";
    private const float HiddenY = -2000f;

    /// <summary>Warp works in story sessions, and not in safari mode.</summary>
    public static bool Enabled(RainWorldGame game) => game.IsStorySession && (!ModManager.MSC || !game.rainWorld.safariMode);

    /// <summary>Runs each time a pause menu is about to be made. A warp that was under way ends.</summary>
    public static void Prepare(RainWorldGame game)
    {
        if (!Enabled(game))
        {
            return;
        }
        WarpState.Load();
        WarpState.EndWarp();
        Switching.WarpDriver.Cancel();
    }

    /// <summary>Adds the Warp menu and its toggle button to a pause menu that was just made.</summary>
    public static void Attach(RainWorldGame game, PauseMenu menu)
    {
        if (!Enabled(game))
        {
            return;
        }
        bool show = WarpState.Preferences.ShowMenu;
        if (show && menu.controlMap != null)
        {
            SetControlMapY(menu, HiddenY, HiddenY);
        }
        AbstractCreature player = game.Players.Count > 0 ? game.Players[0] : null;
        if (player?.realizedCreature == null || player.realizedCreature.inShortcut)
        {
            return;
        }
        Page page = menu.pages[0];
        page.subObjects.Add(new WarpContainer(menu, page, page.pos + new Vector2(20f, show ? 0f : HiddenY)));

        var toggle = new WarpButton(menu, page, "", ToggleSignal,
            new Vector2(41f, game.rainWorld.options.ScreenSize.y - 72f), new Vector2(45f, 20f), Color.red);
        ShowToggleState(toggle, show);
        page.subObjects.Add(toggle);
    }

    /// <summary>Runs every frame the pause menu is open.</summary>
    public static void Update(RainWorldGame game, PauseMenu menu)
    {
        if (!Enabled(game))
        {
            return;
        }
        menu.pages[0].pos.x = 0f;
        if (Input.GetKey(KeyCode.C) && WarpState.DenPosition != null)
        {
            WarpState.SetDenPosition(null);
        }
        RoomListScan.Update(game);
    }

    /// <summary>Handles signals sent to the pause menu: the toggle button and the room buttons.</summary>
    public static void Signal(PauseMenu menu, RainWorldGame game, MenuObject sender, string message)
    {
        if (message == "CONTINUE")
        {
            RoomPreviewDialog.Current?.RemoveSprites();
        }
        if (WarpState.Pending != null || !Enabled(game))
        {
            return;
        }
        if (message == ToggleSignal)
        {
            ToggleVisibility(menu, (WarpButton)sender);
        }
        else if (RoomGrid.RoomOfSignal(message) is { } room)
        {
            ChooseRoom(menu, room);
        }
    }

    /// <summary>
    /// A click on a room button warps there. With shift held it instead sets the room the next story session starts in.
    /// </summary>
    private static void ChooseRoom(PauseMenu menu, string room)
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            WarpState.SetDenPosition(room);
            menu.PlaySound(SoundID.MENU_Player_Join_Game);
            return;
        }
        WarpState.RequestWarp(new Model.WarpRequest(WarpState.SelectedRegion, room));
        WarpLog.Info("Room warp initiated for: " + room);
        menu.Singal(null, "CONTINUE");
    }

    /// <summary>
    /// Hides or shows the Warp menu, saves the choice, and swaps the game's controls picture in or out of its
    /// place.
    /// </summary>
    private static void ToggleVisibility(PauseMenu menu, WarpButton button)
    {
        bool show = !WarpState.Preferences.ShowMenu;
        WarpState.UpdatePreferences(p => p with { ShowMenu = show });
        ShowToggleState(button, show);
        if (menu.controlMap == null)
        {
            return;
        }

        // The game's own controls picture takes the Warp menu's place while it is hidden
        float y = show ? HiddenY : 380f;
        SetControlMapY(menu, y, y);
    }

    /// <summary> Labels and colors the toggle button for the menu being shown (HIDE) or hidden (WARP). </summary>
    private static void ShowToggleState(WarpButton button, bool menuShown)
    {
        button.Text = Translator.Translate(menuShown ? "HIDE" : "WARP");
        button.Tint = Menu.Menu.MenuRGB(menuShown ? Menu.Menu.MenuColors.MediumGrey : Menu.Menu.MenuColors.VeryDarkGrey);
    }

    /// <summary> Moves the game's controls picture up or down, which is how it is shown and hidden. </summary>
    private static void SetControlMapY(PauseMenu menu, float y, float lastY)
    {
        menu.controlMap.pos.y = y;
        menu.controlMap.lastPos.y = lastY;
    }
}
