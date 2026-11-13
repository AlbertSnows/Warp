using System;
using Warp.Model;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Switches to a room in another region without holding the game still while the region loads. Each loop of the
/// old, blocking switch is a stage that advances one step per frame instead. SwitchPolicy decides what to do on
/// a frame; this class gathers the facts the policy needs from the game and carries out its answer. Moving the
/// players once the new region is ready is PlayerTransfer's job.
/// <para>
/// Everything the switch holds is in one <see cref="Running"/> value, or none when it is idle, so ending or dropping it
/// is setting that to null.
/// </para>
/// </summary>
internal sealed class SwitchStepper
{
    /// <summary>
    /// What a switch was asked to do, and the world it started in. Fixed when the switch begins and gone when it ends.
    /// </summary>
    private sealed record Request(RainWorldGame Game, string DestRoom, TileCoord? DestTile, World OldWorld);

    /// <summary> What the switch holds in the stage it is in. Only the stage that has a thing holds it. </summary>
    private abstract record Held;

    /// <summary> While the region loads: the loader. </summary>
    private sealed record Loading(WorldLoader Loader) : Held;

    /// <summary> While the destination room is prepared: the new world and the room in it. </summary>
    private sealed record Preparing(World NewWorld, AbstractRoom NewRoom) : Held;

    /// <summary> A switch that is running: what it was asked, where it is, what it holds, and the last frame it saw. </summary>
    private sealed record Running(Request Request, SwitchState State, Held Held, FrameClock Seen)
    {
        /// <summary>
        /// This switch, after checking that what it holds is what its stage should hold: the loader while loading, the new
        /// world and room while preparing. Called wherever a switch is stored, so a mismatch fails where it is made, with both
        /// named, and not later as a bad cast.
        /// </summary>
        public Running Validated()
        {
            bool matches = (State.Stage, Held) switch
            {
                (SwitchStage.LoadingWorld, Loading) => true,
                (SwitchStage.PreparingRoom, Preparing) => true,
                _ => false
            };
            return matches
                ? this
                : throw new InvalidOperationException("The switch is in stage " + State.Stage + " but holds " + Held.GetType().Name);
        }

        /// <summary> What the switch holds, as the type the stage it is in should hold. </summary>
        public T HeldAs<T>() where T : Held
        {
            return Held as T ?? throw new InvalidOperationException(
                "The switch holds " + Held.GetType().Name + " but " + typeof(T).Name + " was needed");
        }
    }

    private Running _running;

    /// <summary>
    /// The game this switch belongs to, so a switch left over from an earlier game can be recognized and dropped.
    /// Null when no switch is running.
    /// </summary>
    public RainWorldGame Game => _running?.Request.Game;

    /// <summary>
    /// Starts switching to a room in another region and returns straight away; Update carries it out over the next
    /// frames. The caller has to know there is a player to move and that every player has a body that is out of a
    /// shortcut: WarpWaitPolicy only lets a warp start when that is so, and WorldLoaded moves every player's body.
    /// </summary>
    public void BeginSwitch(RainWorldGame game, WarpRequest request, FrameClock clock)
    {
        WarpLog.Info("Loading room " + request.Room + " from region " + request.Region + "!");
        var asked = new Request(game, request.Room, request.Tile, game.overWorld.activeWorld);

        // This is the first half of OverWorld.LoadWorld. The second half, which waits for the loader in a loop, is what
        // Update() does one step at a time instead.
        WorldLoader loader = Staged.Run(WarpStage.LoadWorld, () =>
        {
            WarpLog.Info("Starting to load the new region.");
            OverWorld overWorld = game.overWorld;
            overWorld.worldLoadedFromWarp = false;
            var created = new WorldLoader(
                game, overWorld.PlayerCharacterNumber, overWorld.PlayerTimelinePosition, false, request.Region,
                overWorld.GetRegion(request.Region, false), game.setupValues);
            created.NextActivity();
            return created;
        });
        _running = new Running(asked, SwitchState.Begin(clock.Now, clock.Frame), new Loading(loader), clock).Validated();
    }

    /// <summary>
    /// Moves the switch along by one frame. Call it once per frame, from the game's update, until it returns true.
    /// Returns true when the switch has finished or was dropped because it no longer applies, and false while it is
    /// still working. If something goes wrong it throws a <see cref="WarpStageException"/> that names the part that failed.
    /// </summary>
    public bool Update(FrameClock clock)
    {
        if (_running == null)
        {
            return true;
        }
        Running running = _running with
        {
            Seen = clock
        };
        try
        {
            bool applies = StillApplies(running.Request);
            if (applies)
            {
                Advance(running);
            }
            SwitchStep step = SwitchPolicy.Next(running.State, Observe(running, applies, clock), WarpTimeout.Seconds);
            running = running with
            {
                State = step.State
            };
            switch (step.Action)
            {
                case SwitchAction.Drop:
                    WarpLog.Info("Region switch dropped after " + Elapsed(running)
                                 + ", the player or the world changed while it was loading.");
                    _running = null;
                    return true;
                case SwitchAction.RealizeRoom:
                    _running = RealizeDestinationRoom(running).Validated();
                    return false;
                case SwitchAction.MovePlayers:
                    Preparing preparing = running.HeldAs<Preparing>();
                    WarpLog.Info("Moving player to new region.");
                    PlayerTransfer.Run(
                        running.Request.Game, running.Request.OldWorld, preparing.NewWorld, preparing.NewRoom,
                        running.Request.DestTile);
                    WarpLog.Info("Region switch finished after " + Elapsed(running) + ".");
                    _running = null;
                    return true;
                case SwitchAction.LoadTimedOut:
                    throw WarpTimeout.Exception(WarpStage.LoadWorld);
                case SwitchAction.RoomTimedOut:
                    throw WarpTimeout.Exception(WarpStage.RoomRealiser);
                case SwitchAction.MoveTimedOut:
                    throw WarpTimeout.Exception(WarpStage.MovePlayer);
                case SwitchAction.Wait:
                    _running = running.Validated();
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch
        {
            _running = null;
            throw;
        }
    }

    /// <summary>
    /// Gives up on a switch that is in progress. The world loaded so far is simply left behind.
    /// </summary>
    public void Cancel()
    {
        if (_running != null)
        {
            WarpLog.Info("Region switch cancelled after " + Elapsed(_running) + ".");
        }
        _running = null;
    }

    /// <summary>
    /// Does this frame's work for the stage the switch is in: steps the loader while the region loads, and the room
    /// preparers while the room is prepared. This is the part of a frame that changes the game.
    /// </summary>
    private static void Advance(Running running)
    {
        switch (running.Held)
        {
            case Loading loading:
                Staged.Run(WarpStage.LoadWorld, loading.Loader.Update);
                break;
            case Preparing preparing:
                Staged.Run(WarpStage.RoomRealiser, () => PrepareRooms(running.Request, preparing));
                break;
        }
    }

    /// <summary>
    /// What the game looks like after this frame's work, as the facts SwitchPolicy decides from. Only reads the game.
    /// </summary>
    private static SwitchFacts Observe(Running running, bool applies, FrameClock clock)
    {
        if (!applies)
        {
            return new SwitchFacts(StillApplies: false, Now: clock.Now);
        }
        if (running.Held is Loading loading)
        {
            return new SwitchFacts(StillApplies: true, LoaderFinished: loading.Loader.Finished, Now: clock.Now);
        }
        Preparing preparing = running.HeldAs<Preparing>();
        bool prepared = preparing.NewWorld.loadingRooms.Count == 0 && PlayerOps.RoomReady(preparing.NewRoom);
        return new SwitchFacts(
            StillApplies: true, RoomsPrepared: prepared,
            PlayersCanMove: prepared && PlayerOps.PlayersCanMove(running.Request.Game), Now: clock.Now);
    }

    /// <summary> How long the switch had been going at the last frame it saw, for the log. </summary>
    private static string Elapsed(Running running) =>
        SwitchPolicy.Elapsed(running.State, running.Seen.Now, running.Seen.Frame);

    /// <summary>
    /// Whether the switch still fits the game. The game carries on while the region loads, so the player could die,
    /// or start a gate transition, in the meantime. Either way the switch is dropped.
    /// </summary>
    private static bool StillApplies(Request request)
    {
        RainWorldGame game = request.Game;
        OverWorld overWorld = game.overWorld;
        return overWorld.activeWorld == request.OldWorld
               && overWorld.worldLoader == null
               && game.AlivePlayers.Count > 0;
    }

    /// <summary>
    /// The new world has finished loading: realize the destination room in it. The room is prepared over the next
    /// frames, and the world only becomes the active one once the room is ready, so until then the game carries on
    /// in the old world.
    /// </summary>
    private static Running RealizeDestinationRoom(Running running)
    {
        Loading loading = running.HeldAs<Loading>();
        World newWorld = loading.Loader.ReturnWorld();
        // A room can be listed in the world file and still be missing from the world that was loaded
        AbstractRoom newRoom = Staged.Run(WarpStage.AbstractRoom, () =>
            newWorld.GetAbstractRoom(running.Request.DestRoom)
            ?? throw new InvalidOperationException("The new region has no room named " + running.Request.DestRoom));
        var preparing = new Preparing(newWorld, newRoom);
        Staged.Run(WarpStage.RealiseRoom, () =>
            WithNewWorldActive(running.Request, preparing, () => newRoom.RealizeRoom(newWorld, running.Request.Game)));
        return running with
        {
            Held = preparing
        };
    }

    /// <summary>
    /// Does the same pass over the new world's rooms that the game makes over the active world's rooms every frame.
    /// The rooms are all prepared once none are left in the world's loadingRooms.
    /// </summary>
    private static void PrepareRooms(Request request, Preparing preparing)
    {
        WithNewWorldActive(request, preparing, () =>
        {
            for (int i = preparing.NewWorld.loadingRooms.Count - 1; i >= 0; i--)
            {
                if (preparing.NewWorld.loadingRooms[i].done)
                {
                    preparing.NewWorld.loadingRooms.RemoveAt(i);
                }
                else
                {
                    preparing.NewWorld.loadingRooms[i].Update();
                }
            }
        });
    }

    /// <summary>
    /// Runs the action with the new world as the game's active world, then puts the old one back, even if the action
    /// throws. Rooms read the active world while they are realized and prepared (to place echoes, for example).
    /// </summary>
    private static void WithNewWorldActive(Request request, Preparing preparing, Action action)
    {
        OverWorld overWorld = request.Game.overWorld;
        World active = overWorld.activeWorld;
        overWorld.activeWorld = preparing.NewWorld;
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
