using System;
using RWCustom;
using UnityEngine;

namespace Warp.Switching;

/// <summary>
/// Runs one region switch at a time on behalf of the Warp menu. The menu's per-frame update calls Begin when a
/// room in another region is chosen and Step on every frame after that, until the switch is done.
/// </summary>
public static class RegionSwitchController
{
    private static SwitchStepper _current;

    /// <summary>
    /// Whether a switch has started and is still going.
    /// </summary>
    public static bool Active => _current != null;

    /// <summary>
    /// Starts the switch to the region and room chosen in the Warp menu. A failure is reported to the player
    /// instead of thrown.
    /// </summary>
    public static void Begin(OverWorld overWorld)
    {
        WarpModMenu.warpError = "";
        _current = new SwitchStepper();
        try
        {
            _current.BeginSwitch(overWorld.game, WarpModMenu.newRegion, WarpModMenu.newRoom, new IntVector2(0, 0));
            if (!_current.Busy)
            {
                //There is no player to move, so there is nothing to switch
                Finish();
            }
        }
        catch (Exception e)
        {
            Fail(overWorld, e);
        }
    }

    /// <summary>
    /// Moves the running switch along by one frame, and ends it when it is done or has failed. A switch left over
    /// from an earlier game is dropped.
    /// </summary>
    public static void Step(OverWorld overWorld)
    {
        //A switch from an earlier game has nothing to carry on with
        if (_current.Game != overWorld.game)
        {
            Cancel();
            WarpModMenu.warpActive = false;
            return;
        }
        try
        {
            if (_current.Update())
            {
                Finish();
            }
        }
        catch (Exception e)
        {
            Fail(overWorld, e);
        }
    }

    /// <summary>
    /// Drops a switch that is part way through, for example because the pause menu was opened. Does nothing if
    /// none is running.
    /// </summary>
    public static void Cancel()
    {
        if (_current == null) return;
        _current.Cancel();
        _current = null;
    }

    /// <summary>
    /// Ends the switch, finished or not, and tells the Warp menu the warp is over.
    /// </summary>
    private static void Finish()
    {
        _current = null;
        WarpModMenu.warpActive = false;
    }

    /// <summary>
    /// Reports a failed switch: logs the error, shows it to the player in the pause menu, and ends the switch.
    /// </summary>
    private static void Fail(OverWorld overWorld, Exception e)
    {
        Debug.LogException(e);
        Debug.Log("WARP ERROR: " + _current.ErrorText);
        WarpModMenu.warpError = _current.ErrorText;
        overWorld.game.pauseMenu = new Menu.PauseMenu(overWorld.game.manager, overWorld.game);
        Finish();
    }
}
