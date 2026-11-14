using System;

namespace Warp.Switching;

/// <summary>
/// How long a warp may wait or work before it gives up, and the failure it gives up with.
/// A file of its own so the limit is written once, and every wait (the warp inside a region, the wait before a region
/// switch and the switch itself) gives up after the same time and fails in the same way.
/// </summary>
internal static class WarpTimeout
{
    /// <summary> The limit for each wait or stage of a warp. A region normally loads in a few seconds. </summary>
    public const float Seconds = 60f;

    /// <summary> The failure of a warp that ran out of time in this stage. </summary>
    public static WarpStageException Exception(WarpStage stage) => new(stage, new TimeoutException("The warp did not finish within " + Seconds + " seconds."));
}
