using System;
using Menu;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Carries out the warp the player asked for. The game calls <see cref="Update"/> once a frame after its own world
/// update. A warp inside the region the players are in is done by <see cref="_roomWarp"/>. A warp to another region
/// starts a <see cref="SwitchStepper"/>, which loads the region a step at a time over the next frames. A failure is
/// logged, shown to the player in the pause menu, and ends the warp.
/// </summary>
internal static class WarpDriver
{
    private static readonly RoomWarp _roomWarp = new();
    private static SwitchStepper _regionSwitch;
    private static float? _firstSeen;

    /// <summary> Does one frame of work for the pending warp, if there is one. </summary>
    public static void Update(OverWorld overWorld)
    {
        WarpRequest request = WarpState.Pending;
        if (request == null)
        {
            // A region switch only lives while a warp is requested, so one that is left over is dropped
            Cancel();
            return;
        }
        float now = Time.realtimeSinceStartup;
        _firstSeen ??= now;
        if (_regionSwitch != null)
        {
            StepRegionSwitch(overWorld);
        }
        else if (request.Region == overWorld.activeWorld.region.name)
        {
            UpdateRoomWarp(overWorld, request);
        }
        else
        {
            UpdateStart(overWorld, request, _firstSeen.Value, now);
        }
    }

    /// <summary>
    /// A warp inside the region. It is asked every frame, whether or not the players can move, because the policy it
    /// runs decides when to wait, to give up and to drop the request.
    /// </summary>
    private static void UpdateRoomWarp(OverWorld overWorld, WarpRequest request)
    {
        try
        {
            if (_roomWarp.Update(overWorld, request))
            {
                End();
            }
        }
        catch (Exception error)
        {
            _roomWarp.Finish();
            Fail(overWorld, error, WarpStageText.ErrorText(_roomWarp.Stage));
        }
    }

    /// <summary>
    /// A warp to another region that has not started. It waits until the players can move, and gives up after
    /// <see cref="RoomWarpPolicy.DefaultTimeoutSeconds"/> or when none is alive, as decided by
    /// <see cref="WarpStartPolicy"/>.
    /// </summary>
    private static void UpdateStart(OverWorld overWorld, WarpRequest request, float firstSeen, float now)
    {
        RainWorldGame game = overWorld.game;
        var facts = new WarpStartFacts(game.AlivePlayers.Count > 0, PlayerOps.PlayersCanMove(game), now);
        switch (WarpStartPolicy.Next(firstSeen, facts, RoomWarpPolicy.DefaultTimeoutSeconds))
        {
            case WarpStartAction.Begin:
                BeginRegionSwitch(overWorld, request);
                break;
            case WarpStartAction.Drop:
                End();
                break;
            case WarpStartAction.TimedOut:
                Fail(overWorld, new TimeoutException("The players could not move within "
                                                     + RoomWarpPolicy.DefaultTimeoutSeconds + " seconds."),
                    WarpStageText.ErrorText(WarpStage.MovePlayer));
                break;
            case WarpStartAction.Wait:
                break;
            default:
                throw new ArgumentOutOfRangeException();
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
        _firstSeen = null;
        _roomWarp.Finish();
    }

    /// <summary>
    /// Starts a warp to another region. Having no player to move is not an error; there is simply nothing to do.
    /// </summary>
    private static void BeginRegionSwitch(OverWorld overWorld, WarpRequest request)
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

    /// <summary> Ends the region switch and the request that started it. </summary>
    private static void End()
    {
        _regionSwitch = null;
        _firstSeen = null;
        WarpState.EndWarp();
    }

    /// <summary> Reports a failed warp: logs it, shows the text in a new pause menu, and ends the warp. </summary>
    private static void Fail(OverWorld overWorld, Exception error, string text)
    {
        WarpLog.Exception("warping", error);
        WarpLog.Error(text);
        WarpState.ReportError(text);
        // End first: building the pause menu runs the hook that prepares Warp, which also ends any warp
        End();
        // A new pause menu is the one that shows the error
        overWorld.game.pauseMenu = new PauseMenu(overWorld.game.manager, overWorld.game);
    }
}
