namespace Warp.Switching;

/// <summary>What the game looks like to a warp inside a region on one frame. The facts its decisions depend on.</summary>
/// <param name="StillApplies">There is still a player to move.</param>
/// <param name="RoomReady">The destination room is realized and ready for a player.</param>
/// <param name="PlayersCanMove">Every player has a body that is out of the shortcut system.</param>
/// <param name="Now">Seconds, from a clock that keeps running while the game is paused.</param>
internal readonly record struct RoomWarpFacts(
    bool StillApplies = true,
    bool RoomReady = false,
    bool PlayersCanMove = false,
    float Now = 0f);

/// <summary> What a warp inside a region should do about the frame it has just looked at. </summary>
internal enum RoomWarpAction
{
    /// <summary> Keep waiting, nothing to do this frame. </summary>
    Wait,

    /// <summary> The room is ready and the players can move, so move them. </summary>
    Move,

    /// <summary> The warp no longer applies, so give it up quietly. </summary>
    Drop,

    /// <summary> It has waited too long. </summary>
    TimedOut
}

/// <summary>
/// The decisions of a warp inside a region, kept apart from the game so they can be tested without it, in the same way
/// as <see cref="SwitchPolicy"/> does for a warp to another region. It waits for the room and for every player to be out
/// of a shortcut, and gives up after a time limit, so a room that never becomes ready cannot leave a warp pending for
/// ever.
/// </summary>
internal static class RoomWarpPolicy
{
    /// <summary> How long the warp may wait before it gives up. A room normally takes a second or two. </summary>
    public const float DefaultTimeoutSeconds = 60f;

    /// <summary>
    /// What to do on this frame. A warp that no longer applies is dropped first. Work that is ready counts as done even
    /// if the time is also up, so a room that becomes ready on the last frame is still used.
    /// </summary>
    public static RoomWarpAction Next(float startTime, RoomWarpFacts facts, float timeoutSeconds)
    {
        if (!facts.StillApplies)
        {
            return RoomWarpAction.Drop;
        }
        if (facts is { RoomReady: true, PlayersCanMove: true })
        {
            return RoomWarpAction.Move;
        }
        return facts.Now - startTime > timeoutSeconds ? RoomWarpAction.TimedOut : RoomWarpAction.Wait;
    }
}
