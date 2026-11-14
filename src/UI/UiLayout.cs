namespace Warp.UI;

/// <summary>
/// Sizes and places the menu shares. A file of its own so that every class reads the screen size the same way (one
/// used to read it differently) and the off-screen height is written once.
/// </summary>
internal static class UiLayout
{
    /// <summary>
    /// The size of the screen in menu units. This is the only way the menu reads it. The game's own
    /// <c>RainWorld.screenSize</c> returns <c>options.ScreenSize</c> (read from the game's code), so the two cannot differ.
    /// </summary>
    public static UnityEngine.Vector2 Screen(RainWorld rainWorld) => rainWorld.options.ScreenSize;

    /// <summary> A height far enough below the screen that nothing there can be seen, used to hide a part of the menu. </summary>
    public const float OffScreenY = -2000f;
}
