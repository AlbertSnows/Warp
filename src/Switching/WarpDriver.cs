using System;
using Menu;
using Warp.Game;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Carries out the warp the player asked for. The game calls <see cref="Update"/> once a frame after its own world
/// update. A warp inside the region the players are in is done by <see cref="RoomWarp"/>. A warp to another region
/// starts a <see cref="SwitchStepper"/>, which loads the region a step at a time over the next frames. A failure is
/// logged, shown to the player in the pause menu, and ends the warp.
/// </summary>
internal static class WarpDriver
{
    private static readonly RoomWarp RoomWarp = new();
    private static SwitchStepper _regionSwitch;

    public static void Update(OverWorld overWorld)
    {
        var request = WarpState.Pending;
        if (request == null)
        {
            // A region switch only lives while a warp is requested, so one that is left over is dropped
            Cancel();
            return;
        }
        if (_regionSwitch != null)
        {
            StepRegionSwitch(overWorld);
            return;
        }
        var lead = overWorld.game.AlivePlayers.Count > 0 ? overWorld.game.AlivePlayers[0].realizedCreature as Player : null;
        if (lead == null || lead.inShortcut)
        {
            return;
        }
        if (request.Region != overWorld.activeWorld.region.name)
        {
            BeginRegionSwitch(overWorld, request);
            return;
        }
        try
        {
            if (RoomWarp.Update(overWorld, request, lead))
            {
                WarpState.EndWarp();
            }
        }
        catch (Exception error)
        {
            RoomWarp.Finish();
            Fail(overWorld, error, WarpStageText.ErrorText(RoomWarp.Stage));
        }
    }

    /// <summary>
    /// Drops a warp that is part way through, for example because the pause menu was opened. Does nothing if none is
    /// running.
    /// </summary>
    public static void Cancel()
    {
        _regionSwitch?.Cancel();
        _regionSwitch = null;
        RoomWarp.Finish();
    }

    private static void BeginRegionSwitch(OverWorld overWorld, Model.WarpRequest request)
    {
        WarpState.ClearError();
        _regionSwitch = new SwitchStepper();
        try
        {
            _regionSwitch.BeginSwitch(overWorld.game, request);
            if (!_regionSwitch.Busy)
            {
                // There is no player to move, so there is nothing to switch
                End();
            }
        }
        catch (Exception error)
        {
            Fail(overWorld, error, _regionSwitch.ErrorText);
        }
    }

    /// <summary>
    /// Moves the running region switch along by one frame. A switch left over from an earlier game is dropped.
    /// </summary>
    private static void StepRegionSwitch(OverWorld overWorld)
    {
        if (_regionSwitch.Game != overWorld.game)
        {
            Cancel();
            WarpState.EndWarp();
            return;
        }
        try
        {
            if (_regionSwitch.Update())
            {
                End();
            }
        }
        catch (Exception error)
        {
            Fail(overWorld, error, _regionSwitch.ErrorText);
        }
    }

    private static void End()
    {
        _regionSwitch = null;
        WarpState.EndWarp();
    }

    private static void Fail(OverWorld overWorld, Exception error, string text)
    {
        WarpLog.Exception("warping", error);
        WarpLog.Error(text);
        WarpState.ReportError(text);
        // A new pause menu is the one that shows the error
        overWorld.game.pauseMenu = new PauseMenu(overWorld.game.manager, overWorld.game);
        End();
    }
}
