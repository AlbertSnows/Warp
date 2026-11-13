using System;
using System.Linq;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Switches to a room in another region without holding the game still while the region loads. Each loop of the
/// old, blocking switch is a stage that advances one step per frame instead. SwitchPolicy decides what to do on
/// a frame; this class gathers the facts the policy needs from the game and carries out its answer. Moving the
/// players once the new region is ready is PlayerTransfer's job.
/// </summary>
internal sealed class SwitchStepper
{
    /// <summary>
    /// Moves the players once the new region is ready, and holds the stage a failure is reported against
    /// </summary>
    private readonly PlayerTransfer _transfer = new();

    /// <summary>
    /// What a switch was asked to do, and the world it started in. Fixed when the switch begins and gone when it ends.
    /// </summary>
    private sealed record Request(RainWorldGame Game, string DestRoom, TileCoord? DestTile, World OldWorld);

    private SwitchState _state = SwitchState.Idle;
    private Request _request;
    private WorldLoader _worldLoader;
    private World _newWorld;
    private AbstractRoom _newRoom;

    /// <summary>
    /// The game this switch belongs to, so a switch left over from an earlier game can be recognized and dropped.
    /// Null when no switch is running.
    /// </summary>
    public RainWorldGame Game => _request?.Game;

    /// <summary>
    /// What went wrong, for the player and the log, after BeginSwitch or Update throws.
    /// </summary>
    public string ErrorText => WarpStageText.ErrorText(_transfer.Stage);

    /// <summary>
    /// Whether a switch has started and has not yet finished or been dropped.
    /// </summary>
    public bool Busy => _state.Stage != SwitchStage.Idle;

    /// <summary>
    /// Starts switching to a room in another region and returns straight away; Update carries it out over the next
    /// frames. Does nothing if there is no player to move. Throws if a switch is already in progress, or if a
    /// player has no body to move.
    /// </summary>
    public void BeginSwitch(RainWorldGame game, WarpRequest request)
    {
        if (Busy)
        {
            throw new InvalidOperationException("A region switch is already in progress.");
        }
        if (game.AlivePlayers.Count == 0)
        {
            return;
        }

        bool anyPlayerWithoutBody = game.AlivePlayers.Any(player => player.realizedCreature is not Player);
        if (anyPlayerWithoutBody)
        {
            //WorldLoaded moves every player's body, so a player without one (for example in a room that is not
            //realized) cannot be moved. Say so now instead of waiting for it to time out.
            _transfer.Stage = WarpStage.MovePlayer;
            throw new InvalidOperationException("A player has no body in the world, so the players cannot be moved.");
        }
        _transfer.Stage = WarpStage.LoadWorld;
        WarpLog.Info("Loading room " + request.Room + " from region " + request.Region + "!");
        _request = new Request(game, request.Room, request.Tile, game.overWorld.activeWorld);

        //This is the first half of OverWorld.LoadWorld.
        //The second half, which waits for the loader in a loop, is what
        //Update() does one step at a time instead.
        WarpLog.Info("Starting to load the new region.");
        OverWorld overWorld = game.overWorld;
        overWorld.worldLoadedFromWarp = false;
        _worldLoader = new WorldLoader(
            game, overWorld.PlayerCharacterNumber, overWorld.PlayerTimelinePosition, false, request.Region,
            overWorld.GetRegion(request.Region, false), game.setupValues);
        _worldLoader.NextActivity();
        _state = SwitchState.Begin(Time.realtimeSinceStartup, Time.frameCount);
    }

    /// <summary>
    /// Moves the switch along by one frame. Call it once per frame, from the game's update, until it returns true.
    /// Returns true when the switch has finished or was dropped because it no longer applies, and false while it is
    /// still working. If something goes wrong it throws, and ErrorText says which part failed.
    /// </summary>
    public bool Update()
    {
        if (!Busy)
        {
            return true;
        }
        SwitchState before = _state;
        try
        {
            bool applies = StillApplies();
            if (applies)
            {
                AdvanceStage();
            }
            SwitchStep step = SwitchPolicy.Next(before, ObserveGame(applies), SwitchPolicy.DefaultTimeoutSeconds);
            _state = step.State;
            switch (step.Action)
            {
                case SwitchAction.Drop:
                    WarpLog.Info("Region switch dropped after " + Elapsed(before)
                                 + ", the player or the world changed while it was loading.");
                    Reset();
                    return true;
                case SwitchAction.RealizeRoom:
                    RealizeDestinationRoom();
                    return false;
                case SwitchAction.MovePlayers:
                    WarpLog.Info("Moving player to new region.");
                    _transfer.Run(_request.Game, _request.OldWorld, _newWorld, _newRoom, _request.DestTile);
                    WarpLog.Info("Region switch finished after " + Elapsed(before) + ".");
                    Reset();
                    return true;
                case SwitchAction.LoadTimedOut:
                    throw TimedOut(WarpStage.LoadWorld);
                case SwitchAction.RoomTimedOut:
                    throw TimedOut(WarpStage.RoomRealiser);
                case SwitchAction.MoveTimedOut:
                    throw TimedOut(WarpStage.MovePlayer);
                case SwitchAction.None:
                case SwitchAction.Wait:
                default:
                    return false;
            }
        }
        catch
        {
            Reset();
            throw;
        }
    }

    /// <summary>
    /// Gives up on a switch that is in progress. The world loaded so far is simply left behind.
    /// </summary>
    public void Cancel()
    {
        if (Busy)
        {
            WarpLog.Info("Region switch cancelled after " + Elapsed(_state) + ".");
        }
        Reset();
    }

    /// <summary>
    /// Does this frame's work for the stage the switch is in: steps the loader while the region loads, and the room
    /// preparers while the room is prepared. This is the part of a frame that changes the game.
    /// </summary>
    private void AdvanceStage()
    {
        if (_state.Stage == SwitchStage.LoadingWorld)
        {
            _transfer.Stage = WarpStage.LoadWorld;
            _worldLoader.Update();
            return;
        }
        _transfer.Stage = WarpStage.RoomRealiser;
        PrepareRooms();
    }

    /// <summary>
    /// What the game looks like after this frame's work, as the facts SwitchPolicy decides from. Only reads the game.
    /// </summary>
    private SwitchFacts ObserveGame(bool applies)
    {
        float now = Time.realtimeSinceStartup;
        if (!applies)
        {
            return new SwitchFacts(StillApplies: false, Now: now);
        }
        if (_state.Stage == SwitchStage.LoadingWorld)
        {
            return new SwitchFacts(StillApplies: true, LoaderFinished: _worldLoader.Finished, Now: now);
        }
        bool prepared = _newWorld.loadingRooms.Count == 0;
        return new SwitchFacts(
            StillApplies: true, RoomsPrepared: prepared, PlayersCanMove: prepared && PlayersCanMove(), Now: now);
    }

    /// <summary>
    /// Forgets everything about the switch, leaving it idle.
    /// </summary>
    private void Reset()
    {
        _state = SwitchState.Idle;
        _worldLoader = null;
        _newWorld = null;
        _newRoom = null;
        _request = null;
    }

    /// <summary>
    /// How long the switch has been going, for the log.
    /// </summary>
    private static string Elapsed(SwitchState switchState) =>
        SwitchPolicy.Elapsed(switchState, Time.realtimeSinceStartup, Time.frameCount);

    /// <summary>
    /// The exception for a stage that took too long. Records which stage, so ErrorText names the right one.
    /// </summary>
    private TimeoutException TimedOut(WarpStage stage)
    {
        _transfer.Stage = stage;
        return new TimeoutException(
            "The region switch did not finish within " + SwitchPolicy.DefaultTimeoutSeconds + " seconds.");
    }

    /// <summary>
    /// Whether the switch still fits the game. The game carries on while the region loads, so the player could die,
    /// or start a gate transition, in the meantime. Either way the switch is dropped.
    /// </summary>
    private bool StillApplies()
    {
        RainWorldGame game = _request.Game;
        OverWorld overWorld = game.overWorld;
        return overWorld.activeWorld == _request.OldWorld
               && overWorld.worldLoader == null
               && game.AlivePlayers.Count > 0;
    }

    /// <summary>
    /// The players can only be moved once each has a body that is out of the shortcut system.
    /// </summary>
    private bool PlayersCanMove()
    {
        return _request.Game.AlivePlayers
            .Select(t => t.realizedCreature as Player)
            .All(player => player is { inShortcut: false });
    }

    /// <summary>
    /// The new world has finished loading: realize the destination room in it. The room is prepared over the next
    /// frames, and the world only becomes the active one once the room is ready, so until then the game carries on
    /// in the old world.
    /// </summary>
    private void RealizeDestinationRoom()
    {
        _transfer.Stage = WarpStage.AbstractRoom;
        _newWorld = _worldLoader.ReturnWorld();
        _worldLoader = null;
        _newRoom = _newWorld.GetAbstractRoom(_request.DestRoom);
        _transfer.Stage = WarpStage.RealiseRoom;
        WithNewWorldActive(() => _newRoom.RealizeRoom(_newWorld, _request.Game));
    }

    /// <summary>
    /// Does the same pass over the new world's rooms that the game makes over the active world's rooms every frame.
    /// The rooms are all prepared once none are left in the world's loadingRooms.
    /// </summary>
    private void PrepareRooms()
    {
        WithNewWorldActive(() =>
        {
            for (int i = _newWorld.loadingRooms.Count - 1; i >= 0; i--)
            {
                if (_newWorld.loadingRooms[i].done)
                {
                    _newWorld.loadingRooms.RemoveAt(i);
                }
                else
                {
                    _newWorld.loadingRooms[i].Update();
                }
            }
        });
    }

    /// <summary>
    /// Runs the action with the new world as the game's active world, then puts the old one back, even if the action
    /// throws. Rooms read the active world while they are realized and prepared (to place echoes, for example).
    /// </summary>
    private void WithNewWorldActive(Action action)
    {
        OverWorld overWorld = _request.Game.overWorld;
        World active = overWorld.activeWorld;
        overWorld.activeWorld = _newWorld;
        try
        {
            action();
        }
        finally
        {
            overWorld.activeWorld = active;
        }
    }
}
