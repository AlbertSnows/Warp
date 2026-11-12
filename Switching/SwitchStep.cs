namespace Warp.Switching;

/// <summary>
/// What the game looks like to a region switch on one frame. These are the only things its decisions depend on.
/// Every fact starts as the ordinary case (the switch applies, nothing is finished yet), so a caller names only
/// what differs.
/// </summary>
/// <param name="StillApplies">
/// The player is alive and in the same world the switch started in, and no gate load has begun since.
/// </param>
/// <param name="LoaderFinished">The new region has finished loading.</param>
/// <param name="RoomsPrepared">The destination room, and any others in the new world, have finished preparing.</param>
/// <param name="PlayersCanMove">Every player has a body that is out of the shortcut system.</param>
/// <param name="Now">Seconds, from a clock that keeps running while the game is paused.</param>
internal readonly record struct SwitchFacts(
    bool StillApplies = true,
    bool LoaderFinished = false,
    bool RoomsPrepared = false,
    bool PlayersCanMove = false,
    float Now = 0f);

/// <summary>
/// What a region switch should do about the frame it has just looked at.
/// </summary>
internal enum SwitchAction
{
    /// <summary>
    /// Nothing is going on
    /// </summary>
    None,
    /// <summary>
    /// Keep waiting, nothing to do this frame
    /// </summary>
    Wait,
    /// <summary>
    /// The switch no longer fits the game, give it up quietly
    /// </summary>
    Drop,
    /// <summary>
    /// The new world has loaded, realize the destination room in it
    /// </summary>
    RealizeRoom,
    /// <summary>
    /// Everything is ready, move the players to the new world
    /// </summary>
    MovePlayers,
    /// <summary>
    /// A stage took too long. Which one decides what the error says.
    /// </summary>
    LoadTimedOut,
    RoomTimedOut,
    MoveTimedOut
}

/// <summary>
/// The answer for one frame: what to do, and the state the switch is in afterwards.
/// </summary>
internal readonly record struct SwitchStep(SwitchState State, SwitchAction Action);
