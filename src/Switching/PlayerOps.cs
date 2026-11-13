using System.Collections.Generic;
using System.Linq;

namespace Warp.Switching;

/// <summary>
/// Small things done to the players during a warp, shared by the warp within a region and the warp to another one.
/// </summary>
internal static class PlayerOps
{
    /// <summary>
    /// Whether the players can be moved: each alive player has a body, and none is in the shortcut system. A player in
    /// a shortcut that is placed in a room is left stuck there.
    /// </summary>
    public static bool PlayersCanMove(RainWorldGame game) => game.AlivePlayers.All(CanMove);

    /// <summary>Whether the room is realized and the game says a player can be put in it.</summary>
    public static bool RoomReady(AbstractRoom room) => room.realizedRoom is { ReadyForPlayer: true };

    private static bool CanMove(AbstractCreature player) => player.realizedCreature is Player { inShortcut: false };

    /// <summary>
    /// What a warp that waits for the players and for something else (the room, or nothing) decides from. The alive
    /// players are listed once.
    /// </summary>
    public static WarpWaitFacts Facts(RainWorldGame game, bool ready, FrameClock clock)
    {
        List<AbstractCreature> players = game.AlivePlayers;
        return new WarpWaitFacts(
            StillApplies: players.Count > 0, Ready: ready, PlayersCanMove: players.All(CanMove), Now: clock.Now);
    }

    /// <summary>
    /// Lets go of any creature the creature is holding, so it is not dragged along into the new room. A slugpup is
    /// kept, because it is meant to come along. Another player is let go too: every alive player is moved by the
    /// warp anyway, so none is left behind, but a grasp on one does not survive the move.
    /// </summary>
    public static void ReleaseHeldCreatures(Creature creature)
    {
        if (creature.grasps == null)
        {
            return;
        }
        for (int i = 0; i < creature.grasps.Length; i++)
        {
            Creature.Grasp grasp = creature.grasps[i];
            if (grasp is { grabbed: Creature and not Player { isSlugpup: true }, discontinued: false })
            {
                creature.ReleaseGrasp(i);
            }
        }
    }
}
