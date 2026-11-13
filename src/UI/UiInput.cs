using UnityEngine;

namespace Warp.UI;

/// <summary> Reads the keyboard for the menu. </summary>
internal static class UiInput
{
    /// <summary> Whether either shift key is held down. </summary>
    public static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
}
