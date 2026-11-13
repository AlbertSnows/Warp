using System.Collections.Generic;

namespace Warp.Switching;

/// <summary>
/// Small things done to the players during a warp, shared by the warp within a region and the warp to another one.
/// </summary>
internal static class PlayerOps
{
    /// <summary>
    /// Let go of any creature the creature is holding, so it is not dragged along into the new room. Another
    /// player or a slugpup is kept, because it is meant to come along.
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
            if (grasp?.grabbed is Creature held && !grasp.discontinued && !(held is Player { isSlugpup: true }))
            {
                creature.ReleaseGrasp(i);
            }
        }
    }

    /// <summary>
    /// Removes the later copies of each object from the list, keeping the first. An object that was added to a room
    /// twice would otherwise be updated twice a frame.
    /// </summary>
    public static void RemoveDuplicates(List<UpdatableAndDeletable> updateList, IEnumerable<UpdatableAndDeletable> objects)
    {
        foreach (UpdatableAndDeletable item in objects)
        {
            int first = updateList.IndexOf(item);
            for (int i = updateList.Count - 1; first >= 0 && i > first; i--)
            {
                if (updateList[i] == item)
                {
                    updateList.RemoveAt(i);
                }
            }
        }
    }
}
