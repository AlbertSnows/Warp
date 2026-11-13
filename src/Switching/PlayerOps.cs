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
    public static bool PlayersCanMove(RainWorldGame game)
        => game.AlivePlayers.All(player => player.realizedCreature is Player { inShortcut: false });

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
