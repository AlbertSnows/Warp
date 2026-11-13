#nullable enable
using System;
using Warp.Switching;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary> Checks for carrying the stage a warp failed in on the exception (src/Switching/Staged.cs). </summary>
internal static class StagedTests
{
    public static void Run() => Group(nameof(RunTests), RunTests);

    private static void RunTests()
    {
        CheckEqual("a step that works gives its value", 7, Staged.Run(WarpStage.FindNode, () => 7));

        var original = new InvalidOperationException("boom");
        WarpStageException? caught = Catch(() => Staged.Run(WarpStage.MovePlayer, () => throw original));
        CheckEqual("a failure carries the stage it happened in", WarpStage.MovePlayer, caught?.Stage);
        CheckEqual("and the original is the inner exception", true, ReferenceEquals(original, caught?.InnerException));
        CheckEqual("with the original's message", "boom", caught?.Message);

        WarpStageException? nested = Catch(() =>
            Staged.Run(WarpStage.MovePlayer, () => Staged.Run(WarpStage.MoveObjects, () => throw original)));
        CheckEqual("the innermost stage wins, because it was wrapped first", WarpStage.MoveObjects, nested?.Stage);
        CheckEqual("and the original is still the inner exception, not another wrapper", true,
            ReferenceEquals(original, nested?.InnerException));

        var already = new WarpStageException(WarpStage.LoadWorld, original);
        WarpStageException? passed = Catch(() => Staged.Run(WarpStage.MoveCamera, () => throw already));
        CheckEqual("a failure that already has a stage passes through as the same object", true, ReferenceEquals(already, passed));

        WarpStageException timeout = WarpTimeout.Exception(WarpStage.RoomRealiser);
        CheckEqual("a timeout carries its stage", WarpStage.RoomRealiser, timeout.Stage);
        CheckEqual("and says it was a timeout", true, timeout.InnerException is TimeoutException);
        CheckEqual("naming the limit", true, timeout.Message.Contains(WarpTimeout.Seconds.ToString()));
        CheckEqual("the limit is the one the policies were tested with", 60f, WarpTimeout.Seconds);
    }

    private static WarpStageException? Catch(Action action)
    {
        try
        {
            action();
        }
        catch (WarpStageException error)
        {
            return error;
        }
        return null;
    }
}
