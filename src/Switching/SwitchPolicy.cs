using System.Globalization;

namespace Warp.Switching;

/// <summary>
/// The decisions of a region switch, kept apart from the game so they can be tested without it. Given where a
/// switch is and what the game looks like this frame, it says what to do next. It never changes its arguments.
/// </summary>
internal static class SwitchPolicy
{
    /// <summary>
    /// How long a stage may take before the switch gives up on it. Regions normally load in a few seconds.
    /// </summary>
    public const float DefaultTimeoutSeconds = 60f;

    /// <summary>
    /// What to do on this frame, and the state the switch is in afterwards. An idle switch does nothing and a switch
    /// that no longer applies is dropped. Otherwise the stage decides: loading waits for the loader, preparing waits
    /// for the rooms and then for the players. Each wait gives up once it has lasted longer than timeoutSeconds,
    /// except that work which has finished by then still counts as finished.
    /// </summary>
    public static SwitchStep Next(SwitchState state, SwitchFacts facts, float timeoutSeconds)
    {
        if (state.Stage == SwitchStage.Idle)
        {
            return new SwitchStep(state, SwitchAction.None);
        }
        if (!facts.StillApplies)
        {
            return new SwitchStep(SwitchState.Idle, SwitchAction.Drop);
        }
        var timedOut = facts.Now - state.StageStartTime > timeoutSeconds;

        if (state.Stage == SwitchStage.LoadingWorld)
        {
            //A load that finishes on the same frame it runs out of time has still finished
            return facts.LoaderFinished
                ? new SwitchStep(state.MoveTo(SwitchStage.PreparingRoom, facts.Now), SwitchAction.RealizeRoom)
                : Waiting(state, timedOut, SwitchAction.LoadTimedOut);
        }
        if (!facts.RoomsPrepared)
        {
            return Waiting(state, timedOut, SwitchAction.RoomTimedOut);
        }
        return !facts.PlayersCanMove
            ? Waiting(state, timedOut, SwitchAction.MoveTimedOut)
            : new SwitchStep(SwitchState.Idle, SwitchAction.MovePlayers);
    }

    /// <summary>
    /// Keep waiting, or, once the stage has run out of time, give up with that stage's own timeout action.
    /// </summary>
    private static SwitchStep Waiting(SwitchState state, bool timedOut, SwitchAction timeout)
    {
        return timedOut ? new SwitchStep(SwitchState.Idle, timeout) : new SwitchStep(state, SwitchAction.Wait);
    }

    /// <summary>
    /// How long a switch has been going, for the log, for example "2.4 seconds, 96 frames". Always uses a decimal
    /// point, whatever language the player's computer is set to.
    /// </summary>
    public static string Elapsed(SwitchState state, float now, int frame)
    {
        var seconds = now - state.StartTime;
        var frames = frame - state.StartFrame;
        return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds, " + frames + " frames";
    }
}
