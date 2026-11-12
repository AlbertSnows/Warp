using Warp.Util;

namespace Warp.Switching;

/// <summary>
/// The part of a warp that was being done, kept up to date as the warp runs so that a failure can say where it
/// happened.
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

internal static class WarpStageText
{
    /// <summary>What to tell the player when a warp fails during this stage.</summary>
    public static string ErrorText(WarpStage stage)
    {
        return Translator.Translate(stage switch
        {
            WarpStage.LoadWorld => "An error occurred while loading the new region, check your room connections",
            WarpStage.AbstractRoom => "An error occurred while loading the destination AbstractRoom",
            WarpStage.RealiseRoom => "An error occurred while realising the destination room",
            WarpStage.RoomRealiser => "An error occurred while loading rooms in the new region",
            WarpStage.FindNode => "An error occurred while finding a node to place the player",
            WarpStage.MovePlayer => "An error occurred while moving the player to the new region",
            WarpStage.MoveObjects => "An error occurred while moving the player's items",
            WarpStage.MoveCamera => "An error occurred while moving the RoomCamera to the new room",
            _ => "I have no idea how you got this error"
        });
    }
}
