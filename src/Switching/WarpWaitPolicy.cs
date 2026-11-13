namespace Warp.Switching;

/// <summary>What the game looks like to a warp that is waiting, on one frame. The facts its decisions depend on.</summary>
/// <param name="StillApplies">There is still a player to move.</param>
/// <param name="Ready">
/// Whatever the warp waits for besides the players is ready: the destination room, for a warp inside a region, and
/// nothing, so always true, for a warp to another region that has not started loading.
/// </param>
/// <param name="PlayersCanMove">Every player has a body that is out of the shortcut system.</param>
/// <param name="Now">Seconds, from a clock that keeps running while the game is paused.</param>
internal readonly record struct WarpWaitFacts(
    bool StillApplies = true,
    bool Ready = false,
    bool PlayersCanMove = false,
    float Now = 0f);

/// <summary> What a waiting warp should do about the frame it has just looked at. </summary>
internal enum WarpWaitAction
{
    /// <summary> Keep waiting, nothing to do this frame. </summary>
    Wait,

    /// <summary> Everything is ready and the players can move, so go ahead: move them, or start loading the region. </summary>
    Go,

    /// <summary> The warp no longer applies, so give it up quietly. </summary>
    Drop,

    /// <summary> It has waited too long. </summary>
    TimedOut
}

/// <summary>
/// The decisions of a warp that is waiting, kept apart from the game so they can be tested without it, in the same way
/// as <see cref="SwitchPolicy"/> does for the loading of a region. A warp inside a region waits for its room and for every
/// player to be out of a shortcut; a warp to another region waits for the players only, before it starts loading. Both
/// give up after a time limit and drop when no player is alive, so a request cannot stay pending for ever.
/// </summary>
internal static class WarpWaitPolicy
{
    /// <summary>
    /// What to do on this frame. A warp that no longer applies is dropped first. Work that is ready counts as done even
    /// if the time is also up, so a room that becomes ready on the last frame is still used.
    /// </summary>
    public static WarpWaitAction Next(float startTime, WarpWaitFacts facts, float timeoutSeconds)
    {
        if (!facts.StillApplies)
        {
            return WarpWaitAction.Drop;
        }
        if (facts is { Ready: true, PlayersCanMove: true })
        {
            return WarpWaitAction.Go;
        }
        return facts.Now - startTime > timeoutSeconds ? WarpWaitAction.TimedOut : WarpWaitAction.Wait;
    }
}
