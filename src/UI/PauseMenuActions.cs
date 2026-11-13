using Menu;
using Warp.Game;
using Warp.Model;
using Warp.Util;

namespace Warp.UI;

/// <summary> Things Warp asks of the game's pause menu. </summary>
internal static class PauseMenuActions
{
    /// <summary> The pause menu's message that closes it, which lets the game run again. </summary>
    public const string ContinueSignal = "CONTINUE";

    /// <summary>
    /// Asks for a warp and closes the pause menu, which lets the game run and the driver carry the warp out. The one way
    /// a warp starts, whether from a room button or a click in the room preview.
    /// </summary>
    public static void StartWarp(PauseMenu menu, WarpRequest request)
    {
        WarpState.RequestWarp(request);
        WarpLog.Info("Room warp initiated for: " + request.Room + (request.Tile is { } tile ? " at " + tile.X + "," + tile.Y : ""));
        menu.Singal(null, ContinueSignal);
    }
}
