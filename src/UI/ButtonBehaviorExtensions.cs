using Menu;
using UnityEngine;

namespace Warp.UI;

/// <summary> How lit a button is, worked out from the game's button behaviour. </summary>
internal static class ButtonBehaviorExtensions
{
    /// <summary> How bright the button's flash is at this point between two frames, from 0 to 1. </summary>
    public static float Flash(this ButtonBehavior behavior, float timeStacker) => Mathf.Lerp(behavior.lastFlash, behavior.flash, timeStacker);

    /// <summary> How lit the button is: the larger of the pointer's glow and the flash, at this point between two frames. </summary>
    public static float Glow(this ButtonBehavior behavior, float timeStacker) => Mathf.Max(Mathf.Lerp(behavior.lastCol, behavior.col, timeStacker), behavior.Flash(timeStacker));
}
