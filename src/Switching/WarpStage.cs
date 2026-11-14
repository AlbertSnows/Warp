namespace Warp.Switching;

/// <summary>
/// The parts of a warp where something can go wrong. A step is run as one of these with <see cref="Staged"/>, so a
/// failure carries the stage it happened in, which is what the error text for the player is chosen from.
/// A file of its own because <c>Staged</c>, <c>WarpStageText</c>, <c>WarpTimeout</c> and the tests all name stages, and
/// the pure part of the code has to use it without the game.
/// </summary>
internal enum WarpStage
{
    LoadWorld,
    AbstractRoom,
    RealiseRoom,
    RoomRealiser,
    FindNode,
    MovePlayer,
    MoveObjects,
    MoveCamera
}
