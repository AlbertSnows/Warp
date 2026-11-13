using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private static readonly MethodInfo _killPlayerGuideInNewWorld =
        typeof(OverWorld).GetMethod("KillPlayerGuideInNewWorld", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>The step the transfer is at, so a failure can say what it was doing.</summary>
    public WarpStage Stage { get; set; } = WarpStage.LoadWorld;

    /// <summary>
    /// Moves all the players, with what they carry, into the new world and room, then points the camera at it.
    /// </summary>
    public void Run(RainWorldGame game, World oldWorld, World newWorld, AbstractRoom newRoom, TileCoord? tile)
    {
        AdoptWorld(game, newWorld);

        Stage = WarpStage.FindNode;
        int entranceNode = FindEntranceNode(newRoom);

        ClearHud(game);
        List<AbstractCreature> players = game.AlivePlayers;
        for (int i = 0; i < players.Count; i++)
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

    /// <summary> Makes the new world the one the game is running. </summary>
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
        for (int i = 0; i < room.nodes.Length; i++)
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
        RoomCamera camera = game.cameras[0];
        camera.virtualMicrophone.AllQuiet();
        foreach (FContainer container in camera.hud.fContainers)
        {
            container.RemoveAllChildren();
        }
        camera.hud = null;
    }

    /// <summary>
    /// Moves one player and everything attached to it into the new room. The first player also becomes the one the
    /// room realizer follows.
    /// </summary>
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

        List<AbstractPhysicalObject> carried = abstractPlayer.GetAllConnectedObjects();
        MoveCarriedObjects(abstractPlayer, carried, newWorld, newRoom);
        AbstractPhysicalObject stomachObject = player.objectInStomach;
        if (stomachObject != null)
        {
            stomachObject.world = newWorld;
        }
        Spear backSpear = player.spearOnBack?.spear;

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
        foreach (AbstractPhysicalObject item in carried)
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
        AbstractCreatureAI ai = creature.abstractAI;
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
            List<WorldCoordinate> path = ai.RealAI.pathFinder.CreatePathForAbstractreature(ai.destination);
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
        if (spear == null || player.spearOnBack == null || player.spearOnBack.spear == spear)
        {
            return;
        }

        player.spearOnBack.SpearToBack(spear);
        player.abstractPhysicalObject.stuckObjects.Add(player.spearOnBack.abstractStick);
    }

    /// <summary>
    /// Starts the creature's AI again in the new world. An overseer that guides the player is retired, as the game
    /// does when a world changes.
    /// </summary>
    private static void RestartCreatureAi(RainWorldGame game, AbstractCreature creature, World newWorld, AbstractRoom newRoom)
    {
        if (!creature.creatureTemplate.AI)
        {
            return;
        }
        creature.abstractAI.NewWorld(newWorld);
        creature.InitiateAI();
        creature.abstractAI.RealAI.NewRoom(newRoom.realizedRoom);
        bool isGuide = creature.creatureTemplate.type == CreatureTemplate.Type.Overseer
                       && ((OverseerAbstractAI)creature.abstractAI).playerGuide;
        if (isGuide)
        {
            _killPlayerGuideInNewWorld.Invoke(game.overWorld, [newWorld, creature]);
        }
    }

    /// <summary>Shortcut traffic that belongs to the old region cannot be carried on in the new one.</summary>
    private static void DropVesselsOutsideRegion(RainWorldGame game)
    {
        Region region = game.overWorld.activeWorld.region;
        game.shortcuts.transportVessels.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
        game.shortcuts.betweenRoomsWaitingLobby.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
        game.shortcuts.borderTravelVessels.RemoveAll(vessel => !region.IsRoomInRegion(vessel.room.index));
    }

    /// <summary> Points the camera at the new room and rebuilds the HUD and map for the new world. </summary>
    private static void RefocusCamera(RainWorldGame game, World newWorld, AbstractRoom newRoom)
    {
        RoomCamera camera = game.cameras[0];
        camera.MoveCamera(newRoom.realizedRoom, 0);
        camera.FireUpSinglePlayerHUD(game.AlivePlayers[0].realizedCreature as Player);
        camera.hud.ResetMap(new HUD.Map.MapData(newWorld, game.rainWorld));
        foreach (RoomCamera other in game.cameras.Where(c => c.hud?.textPrompt.subregionTracker != null))
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
