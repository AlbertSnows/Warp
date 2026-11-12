using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// Moves the players, and what they carry, from the world they are in to a new world whose destination room is
/// ready. It follows OverWorld.WorldLoaded in the game, which does the same when the players walk through a gate,
/// with the room and position chosen by the player and not by a gate. The destination room has to be realized and
/// prepared already; see SwitchStepper.
/// </summary>
internal sealed class PlayerTransfer
{
    private static readonly MethodInfo KillPlayerGuideInNewWorld =
        typeof(OverWorld).GetMethod("KillPlayerGuideInNewWorld", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>The step the transfer is at, so a failure can say what it was doing.</summary>
    public WarpStage Stage { get; set; } = WarpStage.LoadWorld;

    public void Run(RainWorldGame game, World oldWorld, World newWorld, AbstractRoom newRoom, TileCoord? tile)
    {
        AdoptWorld(game, newWorld);

        Stage = WarpStage.FindNode;
        var entranceNode = FindEntranceNode(newRoom);

        ClearHud(game);
        var players = game.AlivePlayers;
        for (var i = 0; i < players.Count; i++)
        {
            Stage = WarpStage.MovePlayer;
            MovePlayer(game, players[i], newWorld, newRoom, entranceNode, tile, first: i == 0);
            WarpLog.Info("Player " + i + " moved to the new region");
        }

        DropVesselsOutsideRegion(game);

        Stage = WarpStage.MoveCamera;
        RefocusCamera(game, newWorld, newRoom);
        CarryOverRegionState(oldWorld, newWorld, newRoom);
    }

    private static void AdoptWorld(RainWorldGame game, World newWorld)
    {
        if (game.roomRealizer != null)
        {
            game.roomRealizer = new RoomRealizer(game.roomRealizer.followCreature, newWorld);
        }
        game.overWorld.activeWorld = newWorld;
    }

    /// <summary>
    /// The first node of the room that is an exit with a connection, which is where creatures are put when no spot
    /// was chosen. Node 0 if there is none.
    /// </summary>
    private static int FindEntranceNode(AbstractRoom room)
    {
        for (var i = 0; i < room.nodes.Length; i++)
        {
            if (room.nodes[i].type == AbstractRoomNode.Type.Exit && i < room.connections.Length && room.connections[i] > -1)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>
    /// Silences the old room and discards the HUD, which belongs to the old world. A new one is made once the
    /// camera is in the new room.
    /// </summary>
    private static void ClearHud(RainWorldGame game)
    {
        var camera = game.cameras[0];
        camera.virtualMicrophone.AllQuiet();
        foreach (var container in camera.hud.fContainers)
        {
            container.RemoveAllChildren();
        }
        camera.hud = null;
    }

    private void MovePlayer(
        RainWorldGame game, AbstractCreature abstractPlayer, World newWorld, AbstractRoom newRoom, int entranceNode,
        TileCoord? tile, bool first)
    {
        var player = (Player)abstractPlayer.realizedCreature;
        PlayerOps.ReleaseHeldCreatures(player);

        abstractPlayer.world = newWorld;
        abstractPlayer.pos.room = newRoom.index;
        abstractPlayer.pos.abstractNode = entranceNode;
        abstractPlayer.pos.x = tile?.X ?? 0;
        abstractPlayer.pos.y = tile?.Y ?? 0;
        if (first)
        {
            newRoom.realizedRoom.aimap.NewWorld(newRoom.index);
        }
        player.enteringShortCut = null;

        var carried = abstractPlayer.GetAllConnectedObjects();
        MoveCarriedObjects(abstractPlayer, carried, newWorld, newRoom);
        var stomachObject = player.objectInStomach;
        if (stomachObject != null)
        {
            stomachObject.world = newWorld;
        }
        var backSpear = player.spearOnBack?.spear;

        abstractPlayer.timeSpentHere = 0;
        abstractPlayer.distanceToMyNode = 0;
        // Out of the old room before RealizeInRoom puts the player in the new one
        player.room.RemoveObject(player);
        abstractPlayer.Move(tile is { } spot
            ? new WorldCoordinate(newRoom.index, spot.X, spot.Y, -1)
            : newRoom.realizedRoom.LocalCoordinateOfNode(0));
        ResetPathfinding(abstractPlayer);
        abstractPlayer.RealizeInRoom();

        if (first)
        {
            PlayerOps.RemoveDuplicates(
                newRoom.realizedRoom.updateList,
                carried.Select(item => item.realizedObject).Where(item => item != null));
        }

        Stage = WarpStage.MoveObjects;
        RestoreBackSpear(player, backSpear);
        if (stomachObject != null && player.objectInStomach == null)
        {
            player.objectInStomach = stomachObject;
        }
        RestartCreatureAi(game, abstractPlayer, newWorld, newRoom);
        if (first)
        {
            newRoom.world.game.roomRealizer.followCreature = abstractPlayer;
        }
    }

    /// <summary>Everything the player holds or is stuck to moves to the new world and room with them.</summary>
    private static void MoveCarriedObjects(
        AbstractCreature player, List<AbstractPhysicalObject> carried, World newWorld, AbstractRoom newRoom)
    {
        foreach (var item in carried)
        {
            item.world = newWorld;
            item.pos = player.pos;
            item.Room.RemoveEntity(item);
            newRoom.AddEntity(item);
            if (item.realizedObject != null)
            {
                item.realizedObject.sticksRespawned = true;
            }
        }
    }

    /// <summary>
    /// A creature with AI has a path to a destination in the old world. Give it one in the new world, or none.
    /// </summary>
    private static void ResetPathfinding(AbstractCreature creature)
    {
        var ai = creature.abstractAI;
        if (!creature.creatureTemplate.AI || ai.RealAI?.pathFinder == null)
        {
            return;
        }
        ai.SetDestination(QuickConnectivity.DefineNodeOfLocalCoordinate(ai.destination, creature.world, creature.creatureTemplate));
        ai.timeBuffer = 0;
        if (ai.destination.room == creature.pos.room && ai.destination.abstractNode == creature.pos.abstractNode)
        {
            ai.path.Clear();
        }
        else
        {
            var path = ai.RealAI.pathFinder.CreatePathForAbstractreature(ai.destination);
            if (path != null)
            {
                ai.path = path;
            }
            else
            {
                ai.FindPath(ai.destination);
            }
        }
        ai.RealAI = null;
    }

    /// <summary>A spear on the back is dropped when the player moves, so put it back.</summary>
    private static void RestoreBackSpear(Player player, Spear spear)
    {
        if (spear != null && player.spearOnBack != null && player.spearOnBack.spear != spear)
        {
            player.spearOnBack.SpearToBack(spear);
            player.abstractPhysicalObject.stuckObjects.Add(player.spearOnBack.abstractStick);
        }
    }

    private static void RestartCreatureAi(RainWorldGame game, AbstractCreature creature, World newWorld, AbstractRoom newRoom)
    {
        if (!creature.creatureTemplate.AI)
        {
            return;
        }
        creature.abstractAI.NewWorld(newWorld);
        creature.InitiateAI();
        creature.abstractAI.RealAI.NewRoom(newRoom.realizedRoom);
        var isGuide = creature.creatureTemplate.type == CreatureTemplate.Type.Overseer
                      && ((OverseerAbstractAI)creature.abstractAI).playerGuide;
        if (isGuide)
        {
            KillPlayerGuideInNewWorld.Invoke(game.overWorld, new object[] { newWorld, creature });
        }
    }

    /// <summary>Shortcut traffic that belongs to the old region cannot be carried on in the new one.</summary>
    private static void DropVesselsOutsideRegion(RainWorldGame game)
    {
        var region = game.overWorld.activeWorld.region;
        game.shortcuts.transportVessels.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
        game.shortcuts.betweenRoomsWaitingLobby.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
        game.shortcuts.borderTravelVessels.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
    }

    private static void RefocusCamera(RainWorldGame game, World newWorld, AbstractRoom newRoom)
    {
        var camera = game.cameras[0];
        camera.MoveCamera(newRoom.realizedRoom, 0);
        camera.FireUpSinglePlayerHUD(game.AlivePlayers[0].realizedCreature as Player);
        camera.hud.ResetMap(new HUD.Map.MapData(newWorld, game.rainWorld));
        foreach (var other in game.cameras.Where(c => c.hud?.textPrompt.subregionTracker != null))
        {
            other.hud.textPrompt.subregionTracker.lastShownRegion = 0;
        }
        camera.virtualMicrophone.NewRoom(camera.room);
    }

    /// <summary>The save state and the rain cycle carry on from the old world into the new one.</summary>
    private static void CarryOverRegionState(World oldWorld, World newWorld, AbstractRoom newRoom)
    {
        oldWorld.regionState.AdaptRegionStateToWorld(-1, newRoom.index);
        oldWorld.regionState.world = null;
        newWorld.rainCycle.cycleLength = oldWorld.rainCycle.cycleLength;
        newWorld.rainCycle.timer = oldWorld.rainCycle.timer;
    }
}
