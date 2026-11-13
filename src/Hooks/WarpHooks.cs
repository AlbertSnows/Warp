using System;
using Menu;
using Warp.Game;
using Warp.Switching;
using Warp.UI;
using Warp.Util;

namespace Warp.Hooks;

/// <summary>
/// The hooks into the game. Each one is a thin wrapper that calls <see cref="PauseMenuGlue"/> or the warp driver. The
/// Warp part of each hook is wrapped so that a fault in it is logged and the game's own behaviour carries on; the
/// game's original method is never inside the wrapper.
/// </summary>
internal static class WarpHooks
{
    /// <summary>
    /// Hooks the game methods Warp needs. Call it once, when the mods start. Each is installed on its own, so one that
    /// fails (a game update that renames a method) is logged by name and the others still go in.
    /// </summary>
    public static void Install()
    {
        (string Name, Action Install)[] hooks =
        [
            ("PauseMenu.ctor", () => On.Menu.PauseMenu.ctor += PauseMenuCtor),
            ("PauseMenu.Singal", () => On.Menu.PauseMenu.Singal += PauseMenuSignal),
            ("PauseMenu.Update", () => On.Menu.PauseMenu.Update += PauseMenuUpdate),
            ("OverWorld.Update", () => On.OverWorld.Update += OverWorldUpdate),
            ("SaveState.LoadGame", () => On.SaveState.LoadGame += SaveStateLoadGame),
            ("TutorialControlsPage.GrafUpdate", () => On.Menu.TutorialControlsPage.GrafUpdate += TutorialControlsPageGrafUpdate),
            ("RainWorldGame.ContinuePaused", () => On.RainWorldGame.ContinuePaused += RainWorldGameContinuePaused)
        ];
        foreach ((string name, Action install) in hooks)
        {
            try
            {
                install();
            }
            catch (Exception error)
            {
                WarpLog.Exception("installing the " + name + " hook", error);
            }
        }
    }

    /// <summary> Prepares Warp before a pause menu is built and adds the Warp menu to it afterwards. </summary>
    private static void PauseMenuCtor(On.Menu.PauseMenu.orig_ctor orig, PauseMenu self, ProcessManager manager, RainWorldGame game)
    {
        try
        {
            PauseMenuGlue.Prepare(game);
        }
        catch (Exception error)
        {
            Guard.Report("getting the pause menu ready", error);
        }
        orig(self, manager, game);
        try
        {
            PauseMenuGlue.Attach(game, self);
        }
        catch (Exception error)
        {
            Guard.Report("adding the Warp menu to the pause menu", error);
        }
    }

    /// <summary> Lets Warp see each pause menu signal before the game does. </summary>
    private static void PauseMenuSignal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
    {
        try
        {
            PauseMenuGlue.Signal(self, self.game, sender, message);
        }
        catch (Exception error)
        {
            Guard.Report("handling a pause menu signal", error);
        }
        orig(self, sender, message);
    }

    /// <summary>The pause menu stands still while a room preview is open on top of it.</summary>
    private static void PauseMenuUpdate(On.Menu.PauseMenu.orig_Update orig, PauseMenu self)
    {
        if (RoomPreviewDialog.Current == null)
        {
            orig(self);
        }
        try
        {
            PauseMenuGlue.Update(self.game, self);
        }
        catch (Exception error)
        {
            Guard.Report("updating the pause menu", error);
        }
    }

    /// <summary> Carries a pending warp on by one frame after the game's own world update. </summary>
    private static void OverWorldUpdate(On.OverWorld.orig_Update orig, OverWorld self)
    {
        orig(self);
        try
        {
            if (PauseMenuGlue.Enabled(self.game))
            {
                WarpDriver.Update(self);
            }
        }
        catch (Exception error)
        {
            // The driver handles the faults it expects. This is for one it does not, and ending the warp stops the
            // same fault from coming back every frame
            Guard.Report("carrying out a warp", error);
            WarpDriver.End();
        }
    }

    /// <summary>A den chosen in the Warp menu is where the next story session starts.</summary>
    private static void SaveStateLoadGame(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
    {
        orig(self, str, game);
        string den = WarpState.DenPosition;
        if (den == null)
        {
            return;
        }

        self.denPosition = den;
        self.lastVanillaDen = den;
    }

    /// <summary>
    /// Shuts down the game's controls page while a warp is pending.
    /// </summary>
    private static void TutorialControlsPageGrafUpdate(
        On.Menu.TutorialControlsPage.orig_GrafUpdate orig, TutorialControlsPage self, float timeStacker)
    {
        orig(self, timeStacker);
        if (WarpState.Pending != null)
        {
            self.ShutDownProcess();
        }
    }

    /// <summary>Unpausing is refused while a room preview is open.</summary>
    private static void RainWorldGameContinuePaused(On.RainWorldGame.orig_ContinuePaused orig, RainWorldGame self)
    {
        if (RoomPreviewDialog.Current == null)
        {
            orig(self);
        }
        else if (self.pauseMenu != null)
        {
            self.pauseMenu.wantToContinue = false;
        }
    }
}
