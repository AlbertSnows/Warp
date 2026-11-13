using System;

namespace Warp.Switching;

/// <summary>
/// A failure of a warp, with the <see cref="WarpStage"/> it happened in. The original exception is the inner one.
/// </summary>
internal sealed class WarpStageException(WarpStage stage, Exception inner) : Exception(inner.Message, inner)
{
    public WarpStage Stage { get; } = stage;
}

/// <summary>
/// Runs a step of a warp as a stage. If the step throws, the exception leaves as a <see cref="WarpStageException"/>
/// that names the stage. A failure that already carries a stage passes through unchanged, so the innermost stage is the
/// one reported: it was wrapped first. There is no "current stage" held anywhere to forget to update.
/// </summary>
internal static class Staged
{
    /// <summary> Runs the step as the stage. </summary>
    public static void Run(WarpStage stage, Action step)
    {
        Run(stage, () =>
        {
            step();
            return 0;
        });
    }

    /// <summary> Runs the step as the stage and returns what it gives. </summary>
    public static T Run<T>(WarpStage stage, Func<T> step)
    {
        try
        {
            return step();
        }
        catch (Exception error) when (error is not WarpStageException)
        {
            throw new WarpStageException(stage, error);
        }
    }
}
