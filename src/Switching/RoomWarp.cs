using System;
using System.Linq;
using UnityEngine;
using Warp.Model;

namespace Warp.Switching;

/// <summary>
/// Warps the players to another room of the region they are in. The room is realized first, which takes a few frames,
/// so this is asked once a frame until it says it is done.
/// </summary>
internal sealed class RoomWarp
{
    private AbstractRoom _room;

    /// <summary>The step the warp is at, so a failure can say what it was doing.</summary>
    public WarpStage Stage { get; private set; } = WarpStage.AbstractRoom;

    /// <summary>
    /// Does one frame's work towards the request. Returns true once the players are in the room and the camera has
    /// followed, and false while it is still waiting. Throws if the room does not exist.
    /// </summary>
    public bool Update(OverWorld overWorld, WarpRequest request, Player lead)
    {
        RainWorldGame game = overWorld.game;
        Stage = WarpStage.AbstractRoom;
        if (_room == null || _room.name != request.Room)
        {
            _room = game.world.GetAbstractRoom(request.Room)
                    ?? throw new InvalidOperationException("The region has no room named " + request.Room);
        }
        Stage = WarpStage.RealiseRoom;
        if (_room.realizedRoom == null)
        {
            _room.RealizeRoom(game.world, game);
        }

        Room realized = _room.realizedRoom;
        if (realized != null && realized.ReadyForPlayer)
        {
            Stage = WarpStage.MovePlayer;
            if (lead.room != realized)
            {
                MovePlayers(game, lead, realized, request.Tile);
            }
            else if (request.Tile is { } tile)
            {
                // Already in the room; the player only wanted a spot in it
                PutBodiesAt(game, realized, tile);
                Finish();
                return true;
            }
        }
        if (realized == null)
        {
            return false;
        }

        BringBackSpears(game, realized);
        if (lead.room != realized)
        {
            return false;
        }
        game.cameras[0].virtualMicrophone.AllQuiet();
        game.cameras[0].MoveCamera(lead.room, 0);
        Finish();
        return true;
    }

    /// <summary>Forgets the room, so the next warp looks its destination up afresh.</summary>
    public void Finish() => _room = null;

    /// <summary>
    /// Moves every player into the room, at the chosen tile if there is one and at the room's first entrance if
    /// not.
    /// </summary>
    private static void MovePlayers(RainWorldGame game, Player lead, Room room, TileCoord? tile)
    {
        foreach (AbstractCreature abstractPlayer in game.AlivePlayers.Where(player => player.realizedCreature != null))
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
    private static void PutBodiesAt(RainWorldGame game, Room room, TileCoord tile)
    {
        Vector2 position = room.MiddleOfTile(tile.X, tile.Y);
        foreach (BodyChunk chunk in game.AlivePlayers.Where(player => player.realizedCreature != null)
                     .SelectMany(player => player.realizedCreature.bodyChunks))
        {
            chunk.pos = position;
            chunk.lastPos = position;
        }
    }

    /// <summary>A spear on a player's back stays in the old room unless it is placed in the new one too.</summary>
    private static void BringBackSpears(RainWorldGame game, Room room)
    {
        foreach (Player player in game.AlivePlayers.Select(abstractPlayer => abstractPlayer.realizedCreature as Player))
        {
            Spear spear = player?.spearOnBack?.spear;
            if (spear == null)
            {
                continue;
            }

            spear.PlaceInRoom(room);
            spear.room = player.room;
        }
    }
}
