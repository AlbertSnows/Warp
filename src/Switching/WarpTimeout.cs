using System;

namespace Warp.Switching;

/// <summary> How long a warp may wait or work before it gives up, and the failure it gives up with. </summary>
internal static class WarpTimeout
{
    /// <summary> The limit for each wait or stage of a warp. A region normally loads in a few seconds. </summary>
    public const float Seconds = 60f;

    /// <summary> The failure of a warp that ran out of time in this stage. </summary>
    public static WarpStageException Exception(WarpStage stage) => new(stage, new TimeoutException("The warp did not finish within " + Seconds + " seconds."));
}
