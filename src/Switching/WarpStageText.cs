using System;
using Warp.Util;

namespace Warp.Switching;

/// <summary>The text shown to the player when a warp fails, for the stage it failed in.</summary>
internal static class WarpStageText
{
    private const string UnknownError = "I have no idea how you got this error";

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
            _ => UnknownError
        });
    }

    /// <summary>
    /// What to tell the player about a failure: the text for the stage it carries, or the general one for a failure that
    /// was not run as a stage.
    /// </summary>
    public static string ErrorTextFor(Exception error)
    {
        return error is WarpStageException staged
            ? ErrorText(staged.Stage)
            : Translator.Translate(UnknownError);
    }
}
