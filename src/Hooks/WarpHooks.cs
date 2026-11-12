using Menu;
using Warp.Game;
using Warp.Switching;
using Warp.UI;

namespace Warp.Hooks;

/// <summary>
/// The hooks into the game. Each one is a thin wrapper that calls <see cref="PauseMenuGlue"/> or the warp driver.
/// </summary>
internal static class WarpHooks
{
    public static void Install()
    {
        On.Menu.PauseMenu.ctor += PauseMenuCtor;
        On.Menu.PauseMenu.Singal += PauseMenuSignal;
        On.Menu.PauseMenu.Update += PauseMenuUpdate;
        On.OverWorld.Update += OverWorldUpdate;
        On.SaveState.LoadGame += SaveStateLoadGame;
        On.Menu.TutorialControlsPage.GrafUpdate += TutorialControlsPageGrafUpdate;
        On.RainWorldGame.ContinuePaused += RainWorldGameContinuePaused;
    }

    private static void PauseMenuCtor(On.Menu.PauseMenu.orig_ctor orig, PauseMenu self, ProcessManager manager, RainWorldGame game)
    {
        PauseMenuGlue.Prepare(game);
        orig(self, manager, game);
        PauseMenuGlue.Attach(game, self);
    }

    private static void PauseMenuSignal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
    {
        PauseMenuGlue.Signal(self, self.game, sender, message);
        orig(self, sender, message);
    }

    /// <summary>The pause menu stands still while a room preview is open on top of it.</summary>
    private static void PauseMenuUpdate(On.Menu.PauseMenu.orig_Update orig, PauseMenu self)
    {
        if (RoomPreviewDialog.Current == null)
        {
            orig(self);
        }
        PauseMenuGlue.Update(self.game, self);
    }

    private static void OverWorldUpdate(On.OverWorld.orig_Update orig, OverWorld self)
    {
        orig(self);
        if (PauseMenuGlue.Enabled(self.game))
        {
            WarpDriver.Update(self);
        }
    }

    /// <summary>A den chosen in the Warp menu is where the next story session starts.</summary>
    private static void SaveStateLoadGame(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
    {
        orig(self, str, game);
        var den = WarpState.DenPosition;
        if (den != null)
        {
            self.denPosition = den;
            self.lastVanillaDen = den;
        }
    }

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
