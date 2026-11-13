using System;
using Menu;
using UnityEngine;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Carries out the warp the player asked for. The game calls <see cref="Update"/> once a frame after its own world
/// update. A warp inside the region the players are in is done by <see cref="RoomWarp"/>. A warp to another region
/// starts a <see cref="SwitchStepper"/>, which loads the region a step at a time over the next frames. A failure is
/// logged, shown to the player in the pause menu, and ends the warp. The whole warp is one <see cref="ActiveWarp"/>,
/// or null when there is none.
/// </summary>
internal static class WarpDriver
{
    private static ActiveWarp _active;

    /// <summary> Does one frame of work for the pending warp, if there is one. </summary>
    public static void Update(OverWorld overWorld)
    {
        WarpRequest request = WarpState.Pending;
        if (request == null)
        {
            // A warp only lives while one is requested, so one that is left over is dropped. Checked first because this
            // runs every frame of a story session and there is usually nothing to drop
            if (_active != null)
            {
                End();
            }
            return;
        }
        // The one read of the game's clock this frame
        var clock = new FrameClock(Time.realtimeSinceStartup, Time.frameCount);
        if (_active == null || !ReferenceEquals(_active.Request, request))
        {
            _active?.Switch?.Cancel();
            _active = new ActiveWarp(request, clock.Now);
        }

        try
        {
            if (_active.Switch != null)
            {
                StepRegionSwitch(overWorld, clock);
            }
            else if (request.Region == overWorld.activeWorld.region.name)
            {
                UpdateRoomWarp(overWorld, clock);
            }
            else
            {
                UpdateStart(overWorld, clock);
            }
        }
        catch (Exception error)
        {
            Fail(overWorld, error);
        }
    }

    /// <summary>
    /// A warp inside the region. It is asked every frame, whether or not the players can move, because the policy it
    /// runs decides when to wait, to give up and to drop the request.
    /// </summary>
    private static void UpdateRoomWarp(OverWorld overWorld, FrameClock clock)
    {
        (ActiveWarp warp, bool done) = RoomWarp.Step(overWorld.game, _active, clock);
        _active = warp;
        if (done)
        {
            End();
        }
    }

    /// <summary>
    /// A warp to another region that has not started. It waits until the players can move, and gives up after
    /// <see cref="WarpTimeout.Seconds"/> or when none is alive, as <see cref="WarpWaitPolicy"/> decides, with nothing else
    /// to wait for.
    /// </summary>
    private static void UpdateStart(OverWorld overWorld, FrameClock clock)
    {
        WarpWaitFacts facts = PlayerOps.Facts(overWorld.game, ready: true, clock);
        switch (WarpWaitPolicy.Next(_active.FirstSeen, facts, WarpTimeout.Seconds))
        {
            case WarpWaitAction.Go:
                BeginRegionSwitch(overWorld, clock);
                break;
            case WarpWaitAction.Drop:
                End();
                break;
            case WarpWaitAction.TimedOut:
                throw WarpTimeout.Exception(WarpStage.MovePlayer);
            case WarpWaitAction.Wait:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Starts a warp to another region. The policy has already seen that there are players and that they can move, so
    /// the stepper does not check again.
    /// </summary>
    private static void BeginRegionSwitch(OverWorld overWorld, FrameClock clock)
    {
        WarpState.ClearError();
        var stepper = new SwitchStepper();
        stepper.BeginSwitch(overWorld.game, _active.Request, clock);
        _active = _active with
        {
            Switch = stepper
        };
    }

    /// <summary>
    /// Moves the running region switch along by one frame. A switch left over from an earlier game is dropped.
    /// </summary>
    private static void StepRegionSwitch(OverWorld overWorld, FrameClock clock)
    {
        if (_active.Switch.Game != overWorld.game)
        {
            End();
            return;
        }
        if (_active.Switch.Update(clock))
        {
            End();
        }
    }

    /// <summary>
    /// The one way to finish or abandon a warp, for example because the pause menu was opened: stops a region switch
    /// that is part way through, drops the running warp and ends the request that started it. Safe when none is running.
    /// </summary>
    public static void End()
    {
        _active?.Switch?.Cancel();
        _active = null;
        WarpState.EndWarp();
    }

    /// <summary> Reports a failed warp: logs it, shows the text in a new pause menu, and ends the warp. </summary>
    private static void Fail(OverWorld overWorld, Exception error)
    {
        string text = WarpStageText.ErrorTextFor(error);
        WarpLog.Exception("warping", error);
        WarpLog.Error(text);
        WarpState.ReportError(text);
        // End first: building the pause menu runs the hook that prepares Warp, which also ends any warp
        End();
        // A new pause menu is the one that shows the error
        overWorld.game.pauseMenu = new PauseMenu(overWorld.game.manager, overWorld.game);
    }
}
