using UnityEngine;

namespace Warp.UI;

/// <summary>
/// Reads the keyboard for the menu. A file of its own because the region picker, the container, the preview and the
/// pause menu hook all read the shift key, and none of them is the place that owns keyboard input.
/// </summary>
internal static class UiInput
{
    /// <summary> Whether either shift key is held down. </summary>
    public static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
}
