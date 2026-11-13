namespace Warp.Switching;

/// <summary>
/// The parts of a warp where something can go wrong. A step is run as one of these with <see cref="Staged"/>, so a
/// failure carries the stage it happened in, which is what the error text for the player is chosen from.
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
