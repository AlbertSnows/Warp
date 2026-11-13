using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Warp.Model;

namespace Warp.Switching;

/// <summary>
/// Warps the players to another room of the region they are in. The room is realized first, which takes a few frames,
/// so <see cref="Step"/> is called once a frame until it says the warp is done. It holds nothing between frames: what it
/// has to remember is in the <see cref="ActiveWarp"/>.
/// </summary>
internal static class RoomWarp
{
    /// <summary>
    /// Does one frame's work towards the request. Returns the warp as it is afterwards (with its room looked up) and
    /// whether it is done: the players are in the room and the camera has followed, or the warp no longer applies. Throws
    /// a <see cref="WarpStageException"/> if the room does not exist or the warp runs out of
    /// <see cref="WarpTimeout.Seconds"/> from <see cref="ActiveWarp.FirstSeen"/>.
    /// </summary>
    public static (ActiveWarp Warp, bool Done) Step(RainWorldGame game, ActiveWarp warp, FrameClock clock)
    {
        WarpRequest request = warp.Request;
        AbstractRoom room = warp.Room
                            ?? Staged.Run(WarpStage.AbstractRoom, () => game.world.GetAbstractRoom(request.Room)
                                                                ?? throw new InvalidOperationException("The region has no room named " + request.Room));
        warp = warp with
        {
            Room = room
        };
        Staged.Run(WarpStage.RealiseRoom, () =>
        {
            if (room.realizedRoom == null)
            {
                room.RealizeRoom(game.world, game);
            }
        });

        Room realized = room.realizedRoom;
        WarpWaitFacts facts = PlayerOps.Facts(game, PlayerOps.RoomReady(room), clock);
        switch (WarpWaitPolicy.Next(warp.FirstSeen, facts, WarpTimeout.Seconds))
        {
            case WarpWaitAction.Drop:
                return (warp, true);
            case WarpWaitAction.TimedOut:
                throw WarpTimeout.Exception(facts.Ready ? WarpStage.MovePlayer : WarpStage.RealiseRoom);
            case WarpWaitAction.Wait:
                return (warp, false);
            case WarpWaitAction.Go:
                // The policy said Go, so every alive player has a body that is not in a shortcut
                bool done = Staged.Run(WarpStage.MovePlayer, () => MoveIn(game, realized, request.Tile));
                return (warp, done);
            default:
                // An action added to the policy later must not fall into moving the players
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Moves the players into the room if they are not in it, and then, once they are, moves the camera. Returns whether
    /// that is all done.
    /// </summary>
    private static bool MoveIn(RainWorldGame game, Room realized, TileCoord? tile)
    {
        List<AbstractCreature> players = game.AlivePlayers;
        var lead = (Player)players[0].realizedCreature;
        if (lead.room != realized)
        {
            MovePlayers(players, lead, realized, tile);
        }
        else if (tile is { } spot)
        {
            // Already in the room; the player only wanted a spot in it
            PutBodiesAt(players, realized, spot);
            return true;
        }

        BringBackSpears(players, realized);
        if (lead.room != realized)
        {
            return false;
        }
        game.cameras[0].virtualMicrophone.AllQuiet();
        game.cameras[0].MoveCamera(lead.room, 0);
        return true;
    }

    /// <summary>
    /// Moves every player into the room, at the chosen tile if there is one and at the room's first entrance if
    /// not.
    /// </summary>
    private static void MovePlayers(List<AbstractCreature> players, Player lead, Room room, TileCoord? tile)
    {
        foreach (AbstractCreature abstractPlayer in players)
        {
            Creature creature = abstractPlayer.realizedCreature;
            PlayerOps.ReleaseHeldCreatures(creature);
            abstractPlayer.Move(tile is { } spot
                ? new WorldCoordinate(room.abstractRoom.index, spot.X, spot.Y, -1)
                : room.LocalCoordinateOfNode(0));
            creature.PlaceInRoom(room);
            abstractPlayer.ChangeRooms(lead.room.GetWorldCoordinate(lead.mainBodyChunk.pos));
        }
    }

    /// <summary> Moves each player's body to a tile of the room they are already in. </summary>
    private static void PutBodiesAt(List<AbstractCreature> players, Room room, TileCoord tile)
    {
        Vector2 position = room.MiddleOfTile(tile.X, tile.Y);
        foreach (BodyChunk chunk in players.SelectMany(player => player.realizedCreature.bodyChunks))
        {
            chunk.pos = position;
            chunk.lastPos = position;
        }
    }

    /// <summary>A spear on a player's back stays in the old room unless it is placed in the new one too.</summary>
    private static void BringBackSpears(List<AbstractCreature> players, Room room)
    {
        foreach (AbstractCreature abstractPlayer in players)
        {
            var player = (Player)abstractPlayer.realizedCreature;
            Spear spear = player.spearOnBack?.spear;
            if (spear == null)
            {
                continue;
            }

            spear.PlaceInRoom(room);
            spear.room = player.room;
        }
    }
}
