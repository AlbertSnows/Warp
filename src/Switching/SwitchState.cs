namespace Warp.Switching;

/// <summary>
/// Where a region switch is. A switch moves through these in order and ends back at Idle.
/// </summary>
internal enum SwitchStage
{
    Idle,
    LoadingWorld,
    PreparingRoom
}

/// <summary>
/// A region switch at one moment: which stage it is in, and when it and that stage began. Never changed once
/// made; a new state is made for each change.
/// </summary>
/// <param name="Stage">Which stage the switch is in.</param>
/// <param name="StartTime">The time when the switch began, for the log.</param>
/// <param name="StartFrame">The frame number when the switch began, for the log.</param>
/// <param name="StageStartTime">
/// The time when the current stage began, which is what the timeout is measured from.
/// </param>
internal readonly record struct SwitchState(SwitchStage Stage, float StartTime, int StartFrame, float StageStartTime)
{
    /// <summary>
    /// The state of a switch that is not running.
    /// </summary>
    public static readonly SwitchState Idle = new(SwitchStage.Idle, 0f, 0, 0f);

    /// <summary>
    /// A switch that has just started loading its world, at this time and frame.
    /// </summary>
    public static SwitchState Begin(float now, int frame) => new(SwitchStage.LoadingWorld, now, frame, now);

    /// <summary>
    /// The same switch, in another stage that began now. The switch keeps its own start time and frame, so the log
    /// can report how long the whole switch took; the stage clock, which the timeout uses, starts again.
    /// </summary>
    public SwitchState MoveTo(SwitchStage nextStage, float now) =>
        this with
        {
            Stage = nextStage,
            StageStartTime = now
        };
}
